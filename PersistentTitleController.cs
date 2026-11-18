using System;
using System.Collections.Generic;
using System.Linq;

namespace VipNameChecker;

internal sealed record TitleWritePlan(PlateIdentity Owner, byte[] Base, byte[] Desired, byte[] Upstream, bool ApplyVip);

// All retained state is managed copied bytes and scalar owner identity. Writes
// happen only through the public Title setter in the pre-native callback.
internal sealed class PersistentTitleController
{
    private readonly Func<byte[], byte[]?>? transform;
    public PersistentTitleController(Func<byte[], byte[]?>? transform = null) { this.transform = transform; }
    private sealed record Owned(PlateIdentity Owner, byte[] Base, byte[] Written, byte[] Upstream);
    private sealed record ExpectedClean(PlateIdentity Owner, byte[] Value);
    private readonly Dictionary<int, Owned> owned = new();
    private readonly Dictionary<int, ExpectedClean> awaitingPost = new();
    private readonly HashSet<int> conflicts = new();
    public int OwnedCount => owned.Count;
    public int PendingCount => awaitingPost.Count;
    public int ConflictCount => conflicts.Count;
    public bool HasWork => owned.Count != 0 || awaitingPost.Count != 0;
    public bool IsClean => !HasWork && conflicts.Count == 0;
    public bool TouchesIndex(int index) => owned.ContainsKey(index) || awaitingPost.ContainsKey(index);
    public bool NeedsPostCheck(int index) => awaitingPost.ContainsKey(index);

    public void ReconcileCurrentObjectId(int index, ulong currentObjectId)
    {
        // Invalid/transitional IDs are not proof that ownership changed. A
        // definite NPC/other-object ID is enough; no player reference is needed.
        if (currentObjectId is 0 or 0xE0000000 or ulong.MaxValue) return;
        if ((owned.TryGetValue(index, out var old) && old.Owner.GameObjectId != currentObjectId) ||
            (awaitingPost.TryGetValue(index, out var pending) && pending.Owner.GameObjectId != currentObjectId)) Retire(index);
    }

    public void PruneMissing(ISet<int> activeIndices)
    {
        foreach (int index in owned.Keys.Concat(awaitingPost.Keys).Concat(conflicts).Distinct().ToArray())
            if (!activeIndices.Contains(index)) Retire(index);
    }

    private void Retire(int index)
    {
        owned.Remove(index);
        awaitingPost.Remove(index);
        conflicts.Remove(index);
    }

    public TitleWritePlan? Plan(PlateIdentity owner, ReadOnlySpan<byte> current, ReadOnlySpan<byte> upstream, bool applyVip)
    {
        int index = owner.ArrayIndex;
        owned.TryGetValue(index, out var old);
        if (old != null && old.Owner != owner)
        {
            Retire(index); // Native slot now belongs to another object; never restore the old title into it.
            old = null;
        }

        byte[] baseline;
        if (old != null)
        {
            if (current.SequenceEqual(upstream))
            {
                // A native/upstream refresh has already replaced our rendered input.
                Retire(index);
                baseline = current.ToArray();
                old = null;
                if (!applyVip)
                {
                    awaitingPost[index] = new(owner, baseline);
                    return null;
                }
            }
            else if (current.SequenceEqual(old.Written))
            {
                baseline = upstream.SequenceEqual(old.Upstream) ? old.Base.ToArray() : upstream.ToArray();
                if (applyVip && upstream.SequenceEqual(old.Upstream)) return null; // Stable UI storage: no rewrite.
            }
            else
            {
                var recovery = new TitleRenderTransaction();
                recovery.Record(owner, old.Base, old.Written);
                if (!upstream.SequenceEqual(old.Upstream) || !recovery.TryRestore(owner, current, out baseline))
                {
                    conflicts.Add(index);
                    return null; // Never replace arbitrary text, altered payloads or an uncertain source.
                }
                if (applyVip) return null; // Keep supported later formatting additions and their storage intact.
            }
        }
        else
        {
            // First acquisition requires the render input to be exactly the latest
            // native/Honorific display source; this avoids claiming another writer's title.
            if (!applyVip || !current.SequenceEqual(upstream)) return null;
            baseline = current.ToArray();
        }

        conflicts.Remove(index);
        if (applyVip)
        {
            byte[]? desired;
            bool transformed = transform != null ? (desired = transform(baseline)) != null : NativeTitleSuffix.TryAppend(baseline, out desired);
            if (!transformed)
            {
                // An upstream title may itself now contain VIP. Retire our old
                // ownership and clean to that source instead of stripping its text.
                if (old == null) return null;
                return new(owner, baseline, baseline.ToArray(), upstream.ToArray(), false);
            }
            return new(owner, baseline, desired!, upstream.ToArray(), true);
        }
        return old == null ? null : new(owner, baseline, baseline.ToArray(), upstream.ToArray(), false);
    }

    public bool Commit(TitleWritePlan plan, ReadOnlySpan<byte> actual)
    {
        if (!actual.SequenceEqual(plan.Desired))
        {
            if (owned.ContainsKey(plan.Owner.ArrayIndex)) conflicts.Add(plan.Owner.ArrayIndex);
            return false; // For example a weak setter refusing a RemoveTitle sentinel.
        }
        int index = plan.Owner.ArrayIndex;
        conflicts.Remove(index);
        if (plan.ApplyVip)
        {
            owned[index] = new(plan.Owner, plan.Base.ToArray(), actual.ToArray(), plan.Upstream.ToArray());
            awaitingPost.Remove(index);
        }
        else
        {
            owned.Remove(index);
            awaitingPost[index] = new(plan.Owner, actual.ToArray());
        }
        return true;
    }

    // Post-native OBSERVATION only: acknowledge backing-field cleanup, never set it.
    public bool ObserveCleanup(PlateIdentity owner, ReadOnlySpan<byte> current, ReadOnlySpan<byte> upstream)
    {
        if (!awaitingPost.TryGetValue(owner.ArrayIndex, out var pending)) return true;
        if (pending.Owner != owner)
        {
            Retire(owner.ArrayIndex);
            return true;
        }
        if (!current.SequenceEqual(upstream) && !TitleRenderTransaction.IsFormattingOnlyAddition(current, pending.Value))
        {
            conflicts.Add(owner.ArrayIndex);
            return false;
        }
        awaitingPost.Remove(owner.ArrayIndex);
        conflicts.Remove(owner.ArrayIndex);
        return true;
    }

    public void ClearManagedState() { owned.Clear(); awaitingPost.Clear(); conflicts.Clear(); }
}
