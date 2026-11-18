using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using VipNameChecker;

int passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    passed++;
    Console.WriteLine($"PASS {label}");
}
byte[] Utf(string text) => Encoding.UTF8.GetBytes(text);
byte[] Join(params byte[][] parts) => parts.SelectMany(x => x).ToArray();
void Transform(string input, string expected)
{
    Check(NativeTitleSuffix.TryAppend(Utf(input), out var result) && result.SequenceEqual(Utf(expected)), $"title: {input} -> {expected}");
    Check(!NativeTitleSuffix.TryAppend(result, out _), $"idempotence: {expected}");
}
Transform("《The Dancer》", "《The Dancer VIP》");
Transform("Synced custom title", "Synced custom title VIP");
Transform("", "《VIP》");
Transform("《》", "《VIP》");
Transform("«Custom»", "«Custom VIP»");
Transform("“Custom”", "“Custom VIP”");
Transform("\"Custom\"", "\"Custom VIP\"");
Transform("《星の踊り子》", "《星の踊り子 VIP》");
Transform("VeryVIP", "VeryVIP VIP");
Transform("VIP Club", "VIP Club VIP");
foreach (string title in new[] { "VIP", "《VIP》", "《Custom VIP》", "Custom vip", "Custom [VIP]", "《Custom VIP 》" })
    Check(!NativeTitleSuffix.TryAppend(Utf(title), out _), $"existing suffix: {title}");
byte[] color = [0x02, 0x13, 0x02, 0x06, 0x03];
byte[] reset = [0x02, 0x13, 0x02, 0x01, 0x03];
byte[] original = Join(color, Utf("《Synced title》"), reset);
byte[] expected = Join(color, Utf("《Synced title VIP》"), reset);
Check(NativeTitleSuffix.TryAppend(original, out var formatted) && formatted.SequenceEqual(expected), "outer payloads preserved byte-for-byte");
Check(SeString.Parse(formatted).Encode().SequenceEqual(formatted), "installed Dalamud weak-setter SeString round-trip retains bytes");
original = Join(Utf("《"), color, Utf("Synced title"), reset, Utf("》"));
expected = Join(Utf("《"), color, Utf("Synced title"), reset, Utf(" VIP》"));
Check(NativeTitleSuffix.TryAppend(original, out formatted) && formatted.SequenceEqual(expected), "inner payloads and quote delimiters preserved");
Check(!NativeTitleSuffix.TryAppend(Join(Utf("《Title V"), color, Utf("IP》"), reset), out _), "duplicate split across formatted payloads");
Check(!NativeTitleSuffix.TryAppend(new byte[] {0x02, 0x13}, out _), "malformed payload left unchanged");
Check(!NativeTitleSuffix.TryAppend(new byte[] {0xff}, out _), "invalid UTF-8 left unchanged");

var transaction = new TitleRenderTransaction();
var alice = new PlateIdentity(0, 100, (nint)1234);
byte[] before = Utf("《Alice》");
byte[] after = Utf("《Alice VIP》");
transaction.Record(alice, before, after);
Check(transaction.TryRestore(alice, after, out var restored) && restored.SequenceEqual(before), "render input restored exactly after render");
Check(!transaction.TryRestore(alice, after, out _), "transaction restoration is one-shot");
transaction.Record(alice, before, after);
Check(!transaction.TryRestore(alice, Utf("《New Honorific title》"), out _), "concurrent title update is not overwritten by cleanup");
transaction.Record(alice, before, after);
Check(!transaction.TryRestore(alice with { GameObjectId = 101 }, after, out _), "slot reuse with new object ID does not restore stale title");
Check(!transaction.TryRestore(alice with { PlayerAddress = (nint)9999 }, after, out _), "slot reuse with new object address does not restore stale title");
Check(!transaction.TryRestore(alice with { ArrayIndex = 1 }, after, out _), "different backing array slot does not restore stale title");
transaction.Clear();
Check(!transaction.TryRestore(alice, after, out _), "clearing frame state discards edits");
transaction.Record(alice, before, after);
before[0] = 1;
after[0] = 2;
Check(transaction.TryRestore(alice, Utf("《Alice VIP》"), out restored) && restored.SequenceEqual(Utf("《Alice》")), "snapshots own copies independent of later mutations");
// Model repeated renders, removal/disable and a changed upstream title. Native
// title nodes consume rendered bytes before the post event restores their input.
for (int i = 0; i < 100; i++)
{
    var source = Utf(i < 50 ? "《Title A》" : "《Title B》");
    Check(NativeTitleSuffix.TryAppend(source, out var render), $"frame {i}: fresh upstream title transformed");
    transaction.Record(alice, source, render);
    Check(transaction.TryRestore(alice, render, out restored) && restored.SequenceEqual(source), $"frame {i}: source restored without accumulation");
}
Check(transaction.TryRestore(alice, Utf(""), out _) == false, "title removal has no lingering owned suffix");
// Later title-part builders must not leave this plugin's suffix in backing data.
var baseline = Utf("\u300ATitle\u300B");
NativeTitleSuffix.TryAppend(baseline, out var owned);
Check(!transaction.HasEdits && !transaction.TouchesIndex(alice.ArrayIndex), "empty transaction exposes callback fast path");
transaction.Record(alice, baseline, owned);
Check(transaction.HasEdits && transaction.TouchesIndex(alice.ArrayIndex) && !transaction.TouchesIndex(1), "only touched index requires owner/field resolution");
var outerWrapped = Join(color, owned, reset);
Check(transaction.TryRestore(alice, outerWrapped, out restored) && restored.SequenceEqual(Join(color, baseline, reset)), "OuterWrap: remove owned insertion while preserving outer payloads");
Check(!transaction.HasEdits, "wrap-then-disable has no pending edit or suffix in backing text");
var innerWrapped = Join(Utf("\u300A"), color, Utf("Title VIP"), reset, Utf("\u300B"));
var innerExpected = Join(Utf("\u300A"), color, Utf("Title"), reset, Utf("\u300B"));
transaction.Record(alice, baseline, owned);
Check(transaction.TryRestore(alice, innerWrapped, out restored) && restored.SequenceEqual(innerExpected), "TextWrap: payload changes inside quotes survive suffix removal");
Check(!transaction.HasEdits && restored.SequenceEqual(innerExpected), "wrap-then-unload leaves clean source text for a later native redraw");
var splitWrapped = Join(Utf("\u300ATitle V"), color, Utf("I"), reset, Utf("P\u300B"));
var splitExpected = Join(Utf("\u300ATitle"), color, reset, Utf("\u300B"));
transaction.Record(alice, baseline, owned);
Check(transaction.TryRestore(alice, splitWrapped, out restored) && restored.SequenceEqual(splitExpected), "payloads interleaved through owned suffix remain byte-identical");
transaction.Record(alice, baseline, owned);
var textWrapped = Join(Utf("prefix "), color, owned, reset, Utf(" suffix"));
Check(!transaction.TryRestore(alice, textWrapped, out _), "visible additions around a written title fail closed; substring is not provenance");
transaction.Record(alice, baseline, owned);
var concurrent = Utf("\u300AOther writer VIP\u300B");
var concurrentCopy = concurrent.ToArray();
Check(!transaction.TryRestore(alice, concurrent, out _) && concurrent.SequenceEqual(concurrentCopy), "concurrent replacement with its own VIP is not stripped or overwritten");
transaction.Record(alice, baseline, owned);
var repeated = Join(owned, owned);
Check(!transaction.TryRestore(alice, repeated, out _), "ambiguous repeated written title fails closed without rewriting text");
transaction.Record(alice, baseline, owned);
Check(!transaction.TryRestore(alice, new byte[] {0x02, 0x13}, out _), "malformed concurrent field fails closed");
NativeTitleSuffix.TryAppend(Array.Empty<byte>(), out var standalone);
transaction.Record(alice, Array.Empty<byte>(), standalone);
Check(transaction.TryRestore(alice, Join(color, standalone, reset), out restored) && restored.SequenceEqual(Join(color, reset)), "wrapped empty-title VIP removed without removing another writer's payloads");
transaction.Record(alice, baseline, owned);
Check(!transaction.TryRestore(alice with {GameObjectId = 999}, outerWrapped, out _), "wrapped title still requires exact owner identity");
transaction.Clear();
// Exact independently reported regression: preserve a later writer's title.
var dancer = Utf("Dancer");
NativeTitleSuffix.TryAppend(dancer, out var dancerVip);
transaction.Record(alice, dancer, dancerVip);
var coordinator = Utf("Former Dancer VIP Coordinator");
var coordinatorCopy = coordinator.ToArray();
Check(!transaction.TryRestore(alice, coordinator, out restored) && restored.Length == 0 && coordinator.SequenceEqual(coordinatorCopy), "regression: Former Dancer VIP Coordinator must remain untouched");
transaction.Record(alice, dancer, dancerVip);
Check(!transaction.TryRestore(alice, Join(color, coordinator, reset), out _), "regression with style wrappers still declines changed whole visible text");
transaction.Record(alice, dancer, dancerVip);
Check(!transaction.TryRestore(alice, Join(dancerVip, Utf(" / "), dancerVip), out _), "ambiguous repeated phrase is not ownership");
byte[] Macro(byte code, byte[] body) => Join(new byte[] {0x02, code, checked((byte)(body.Length + 1))}, body, new byte[] {0x03});
byte[] icon = Macro(0x12, new byte[] {0x06});
byte[] link = Macro(0x27, new byte[] {0x06});
byte[] nestedText = Utf("Dancer VIP");
byte[] stringMacro = Macro(0x29, Join(new byte[] {0xff, checked((byte)(nestedText.Length + 1))}, nestedText));
foreach (var semanticMacro in new[] {icon, link, stringMacro, Macro(0x7e, new byte[] {0x06})})
{
    transaction.Record(alice, dancer, dancerVip);
    var changedByMacro = Join(semanticMacro, dancerVip);
    var unchangedCopy = changedByMacro.ToArray();
    Check(!transaction.TryRestore(alice, changedByMacro, out _) && changedByMacro.SequenceEqual(unchangedCopy), "semantic/unknown macro addition is not a formatting wrapper");
}
transaction.Record(alice, dancer, dancerVip);
Check(!transaction.TryRestore(alice, stringMacro, out _), "written bytes embedded inside a macro are not an owned visible insertion");
var existingMacroTitle = Join(icon, dancer);
NativeTitleSuffix.TryAppend(existingMacroTitle, out var existingMacroOwned);
transaction.Record(alice, existingMacroTitle, existingMacroOwned);
Check(transaction.TryRestore(alice, Join(color, existingMacroOwned, reset), out restored) && restored.SequenceEqual(Join(color, existingMacroTitle, reset)), "existing semantic macro retained verbatim while new formatting wrappers are preserved");
transaction.Record(alice, existingMacroTitle, existingMacroOwned);
Check(!transaction.TryRestore(alice, Join(color, dancerVip, reset), out _), "deleting an existing semantic macro is not wrapper insertion");
var existingColorTitle = Join(color, dancer, reset);
NativeTitleSuffix.TryAppend(existingColorTitle, out var existingColorOwned);
transaction.Record(alice, existingColorTitle, existingColorOwned);
byte[] otherColor = Macro(0x13, new byte[] {0x07});
Check(!transaction.TryRestore(alice, Join(otherColor, dancerVip, reset), out _), "modifying an existing formatting macro is not wrapper insertion");
transaction.Record(alice, dancer, dancerVip);
Check(transaction.TryRestore(alice, Join(Macro(0x19, new byte[] {0x02}), dancerVip, Macro(0x19, new byte[] {0x01})), out restored) && restored.SequenceEqual(Join(Macro(0x19, new byte[] {0x02}), dancer, Macro(0x19, new byte[] {0x01}))), "explicit bold style wrappers preserve whole-title ownership");
transaction.Clear();
// Numeric-only observer: tests never dereference these synthetic addresses.
Check(NativePointerProbe.Classify(1000, 9, 1000, 2000, 10, 64) == PointerRelation.TitleStorageAlias, "probe detects exact title source alias");
Check(NativePointerProbe.Classify(1000, 9, 1009, 2000, 10, 64) == PointerRelation.TitleStorageAlias, "probe detects retained terminator/interior title address");
Check(NativePointerProbe.Classify(1000, 9, 2000, 2000, 10, 64) == PointerRelation.SeparateNodeStorage, "probe identifies source inside known separate node storage");
Check(NativePointerProbe.Classify(1000, 9, 2009, 2000, 10, 64) == PointerRelation.SeparateNodeStorage, "probe includes last used node byte");
Check(NativePointerProbe.Classify(1000, 9, 2010, 2000, 10, 64) == PointerRelation.Unknown, "probe does not treat spare capacity as a known live source");
Check(NativePointerProbe.Classify(1000, 9, 3000, 2000, 10, 64) == PointerRelation.Unknown, "different third-party buffer remains unknown rather than safe");
Check(NativePointerProbe.Classify(1000, 9, 2000, 1005, 10, 64) == PointerRelation.TitleStorageAlias, "overlapping node/title storage is classified as alias");
Check(NativePointerProbe.Classify(1000, 9, 1010, 1010, 10, 64) == PointerRelation.SeparateNodeStorage, "adjacent nonoverlapping ranges remain separate");
Check(NativePointerProbe.Classify(0, 9, 2000, 2000, 10, 64) == PointerRelation.Unknown, "null title buffer stays unknown");
Check(NativePointerProbe.Classify(1000, 9, 0, 2000, 10, 64) == PointerRelation.Unknown, "null retained source stays unknown");
Check(NativePointerProbe.Classify(1000, 9, 2000, 0, 10, 64) == PointerRelation.Unknown, "null node buffer stays unknown");
Check(NativePointerProbe.Classify(1000, -1, 2000, 2000, 10, 64) == PointerRelation.Unknown, "negative title length rejected");
Check(NativePointerProbe.Classify(1000, 9, 2000, 2000, 0, 64) == PointerRelation.Unknown, "empty used node range rejected");
Check(NativePointerProbe.Classify(1000, 9, 2000, 2000, 65, 64) == PointerRelation.Unknown, "invalid native used/capacity relationship rejected");
Check(NativePointerProbe.Classify(1000, 9, 2000, 2000, 10, 16 * 1024 * 1024 + 1) == PointerRelation.Unknown, "implausible native capacity rejected");
Check(NativePointerProbe.Classify(nuint.MaxValue - 4, 9, 2000, 2000, 10, 64) == PointerRelation.Unknown, "overflowing title range rejected");
Check(NativePointerProbe.Classify(1000, 9, 2000, nuint.MaxValue - 4, 10, 64) == PointerRelation.Unknown, "overflowing node range rejected");
var probeBudget = new PointerProbeBudget();
Check(!probeBudget.Active && !probeBudget.CanArm && !probeBudget.Record(ProbePhase.BeforeNative, PointerRelation.Unknown), "observer starts inactive and never authorizes arming");
probeBudget.Start();
Check(probeBudget.Active && !probeBudget.Record(ProbePhase.BeforeNative, PointerRelation.Unknown), "samples require an active bounded frame");
for (int f = 0; f < PointerProbeBudget.FrameLimit; f++)
{
    if (!probeBudget.BeginFrame()) throw new InvalidOperationException("probe frame missing");
    for (int plate = 0; plate < PointerProbeBudget.PlatesPerFrame; plate++)
        foreach (var phase in Enum.GetValues<ProbePhase>())
            if (!probeBudget.Record(phase, PointerRelation.SeparateNodeStorage)) throw new InvalidOperationException("probe sample missing");
    probeBudget.EndFrame();
}
Check(!probeBudget.Active && probeBudget.Samples == 72 && probeBudget.Separate == 24 && probeBudget.Unknown == 0 && !probeBudget.CanArm, "all-separate baseline reaches exact bounds and still cannot establish mutation lifetime");
Check(!probeBudget.BeginFrame() && !probeBudget.Record(ProbePhase.AfterNativeBeforeCleanup, PointerRelation.Unknown), "finished observer cannot consume extra frames or samples");
probeBudget.Start();
probeBudget.BeginFrame();
probeBudget.Record(ProbePhase.BeforeNative, PointerRelation.TitleStorageAlias);
probeBudget.Record(ProbePhase.AfterNativeBeforeCleanup, PointerRelation.Unknown);
probeBudget.Record(ProbePhase.AfterCleanup, PointerRelation.SeparateNodeStorage);
Check(probeBudget.Unknown == 1 && probeBudget.Aliases == 0 && probeBudget.Separate == 0 && !probeBudget.CanArm, "summary separates native-consumption phase from before/after observations");
probeBudget.Stop();
Check(!probeBudget.Active && !probeBudget.CanArm, "stop/timeout leaves renderer unarmed");
probeBudget.Start();
Check(probeBudget.Frames == 0 && probeBudget.Samples == 0 && probeBudget.Unknown == 0, "new explicit observation resets only primitive counters");
probeBudget.Stop();
Check(NativePointerProbe.Classify(1000, 9, 990, 990, 5, 64) == PointerRelation.TitleStorageAlias, "node capacity overlapping title storage cannot be certified separate");
Check(NativePointerProbe.Classify(1000, 9, 2000, nuint.MaxValue - 4, 2, 64) == PointerRelation.Unknown, "overflowing node capacity range rejected even when used range fits");
// Persistent storage lifecycle: all actions below are pure managed plans. The
// actual plugin commits through the pre-update weak setter; post only observes.
PersistentTitleController NewPersistent() => new();
TitleWritePlan MustPlan(PersistentTitleController c, PlateIdentity id, byte[] field, byte[] source, bool apply)
    => c.Plan(id, field, source, apply) ?? throw new InvalidOperationException("missing persistent plan");
var pc = NewPersistent();
var sourceA = Utf("\u300ADancer\u300B");
var sourceB = Utf("\u300ANew synced title\u300B");
var pp = MustPlan(pc, alice, sourceA, sourceA, true);
Check(pp.ApplyVip && pp.Desired.SequenceEqual(Utf("\u300ADancer VIP\u300B")), "persistent first acquisition requires exact upstream and prepares suffix");
Check(pc.OwnedCount == 0, "plan alone does not claim an uncommitted write");
Check(pc.Commit(pp, pp.Desired) && pc.OwnedCount == 1 && !pc.IsClean, "persistent write commits managed ownership");
for (int frame = 0; frame < 100; frame++)
    if (pc.Plan(alice, pp.Desired, sourceA, true) != null) throw new InvalidOperationException("stable persistent title rewritten");
Check(pc.OwnedCount == 1, "100 stable frames cause no rewritten UI buffers or duplicate suffixes");
var cleanPlan = MustPlan(pc, alice, pp.Desired, sourceA, false);
Check(!cleanPlan.ApplyVip && cleanPlan.Desired.SequenceEqual(sourceA), "persistent disarm prepares original bytes before native update");
Check(pc.Commit(cleanPlan, cleanPlan.Desired) && pc.OwnedCount == 0 && pc.PendingCount == 1 && !pc.IsClean, "disarm cleanup is pending until post-native observation");
Check(pc.ObserveCleanup(alice, sourceA, sourceA) && pc.IsClean, "matching post-native field acknowledges controlled cleanup");

pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
var updated = MustPlan(pc, alice, pp.Desired, sourceB, true);
Check(updated.Base.SequenceEqual(sourceB) && updated.Desired.SequenceEqual(Utf("\u300ANew synced title VIP\u300B")), "latest QuotedTitle replaces stale owned input when upstream changes");
pc.Commit(updated, updated.Desired);
cleanPlan = MustPlan(pc, alice, updated.Desired, sourceB, false);
Check(cleanPlan.Desired.SequenceEqual(sourceB), "disarm never resurrects previous Honorific source after update");
pc.Commit(cleanPlan, cleanPlan.Desired);
Check(pc.ObserveCleanup(alice, sourceB, sourceB) && pc.IsClean, "updated source cleanup acknowledged");

pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
cleanPlan = MustPlan(pc, alice, pp.Desired, sourceB, false);
Check(cleanPlan.Desired.SequenceEqual(sourceB), "source update during disarm uses current upstream instead of saved original");
var naturallyVip = Utf("\u300ANative VIP\u300B");
var naturalPlan = MustPlan(pc, alice, pp.Desired, naturallyVip, true);
Check(!naturalPlan.ApplyVip && naturalPlan.Desired.SequenceEqual(naturallyVip), "new upstream VIP text is preserved as source-owned");

pc = NewPersistent();
Check(pc.Plan(alice, Utf("Other writer"), sourceA, true) == null && pc.IsClean, "initial mismatched render input is never acquired or overwritten");
Check(pc.Plan(alice, sourceA, sourceA, false) == null && pc.IsClean, "unarmed persistent path does not write title fields");
pp = MustPlan(pc, alice, sourceA, sourceA, true);
Check(!pc.Commit(pp, Array.Empty<byte>()) && pc.IsClean, "RemoveTitle weak-setter refusal creates no ownership");

pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
var persistentWrapped = Join(color, pp.Desired, reset);
Check(pc.Plan(alice, persistentWrapped, sourceA, true) == null, "later formatting wrappers do not trigger stable storage rewrites");
cleanPlan = MustPlan(pc, alice, persistentWrapped, sourceA, false);
Check(cleanPlan.Desired.SequenceEqual(Join(color, sourceA, reset)), "persistent pre-native cleanup preserves added formatting bytes");
pc.Commit(cleanPlan, cleanPlan.Desired);
Check(pc.ObserveCleanup(alice, Join(Macro(0x19, new byte[] {0x02}), cleanPlan.Desired), sourceA) && pc.IsClean, "post-native observation accepts only proven added formatting on clean input");

pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
Check(pc.Plan(alice, Join(color, pp.Desired, reset), sourceB, false) == null && pc.ConflictCount == 1 && !pc.IsClean, "wrapped owned input plus changed source fails closed instead of merging unknown writers");
Check(pc.Plan(alice, sourceB, sourceB, false) == null && pc.PendingCount == 1, "fresh native source can retire conflicted ownership without any field write");
Check(pc.ObserveCleanup(alice, sourceB, sourceB) && pc.IsClean, "native source refresh resolves a prior conflict");

pc = NewPersistent(); pp = MustPlan(pc, alice, dancer, dancer, true); pc.Commit(pp, pp.Desired);
Check(pc.Plan(alice, coordinator, dancer, false) == null && pc.ConflictCount == 1 && pc.OwnedCount == 1, "persistent Former Dancer VIP Coordinator regression does not overwrite another writer");
Check(!pc.IsClean, "ambiguous cleanup cannot claim readiness for controlled unload");
pc.PruneMissing(new HashSet<int>());
Check(pc.IsClean, "retired inactive slots release only managed ownership records");

pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
var bob = alice with {GameObjectId = 999, PlayerAddress = (nint)9876};
Check(pc.Plan(bob, sourceB, sourceB, false) == null && pc.IsClean, "slot reuse cannot restore an earlier object's source");
pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
Check(pc.Plan(alice, sourceA, sourceA, false) == null && pc.PendingCount == 1 && pc.OwnedCount == 0, "already refreshed native field avoids an unnecessary cleanup write");
Check(pc.ObserveCleanup(alice, sourceA, sourceA) && pc.IsClean, "already refreshed field still requires post-native acknowledgement");

pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
cleanPlan = MustPlan(pc, alice, pp.Desired, sourceA, false); pc.Commit(cleanPlan, cleanPlan.Desired);
Check(!pc.ObserveCleanup(alice, Utf("Concurrent new title"), sourceA) && !pc.IsClean && pc.ConflictCount == 1, "unknown post-native replacement prevents a false cleanup acknowledgement");
Check(pc.ObserveCleanup(alice, sourceB, sourceB) && pc.IsClean, "a later exact native/upstream field can resolve pending acknowledgement");

pc = NewPersistent(); pp = MustPlan(pc, alice, Array.Empty<byte>(), Array.Empty<byte>(), true); pc.Commit(pp, pp.Desired);
cleanPlan = MustPlan(pc, alice, pp.Desired, Array.Empty<byte>(), false);
Check(cleanPlan.Desired.Length == 0, "standalone persistent VIP cleans back to empty title");
pc.Commit(cleanPlan, cleanPlan.Desired);
Check(pc.ObserveCleanup(alice, Array.Empty<byte>(), Array.Empty<byte>()) && pc.IsClean, "empty-title cleanup acknowledges without retaining a native buffer");

pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
pp.Base[0] = 1; pp.Upstream[0] = 2; pp.Desired[0] = 3;
cleanPlan = MustPlan(pc, alice, Utf("\u300ADancer VIP\u300B"), sourceA, false);
Check(cleanPlan.Desired.SequenceEqual(sourceA), "persistent snapshots own copies independent of plan mutation");
pc.ClearManagedState();
Check(pc.IsClean, "uncontrolled disposal clears managed records without any native write/free action");
pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
pc.ReconcileCurrentObjectId(alice.ArrayIndex, 456);
Check(pc.IsClean, "PC slot reused by definite NPC ID retires ownership without resolving a player");
pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
cleanPlan = MustPlan(pc, alice, pp.Desired, sourceA, false); pc.Commit(cleanPlan, cleanPlan.Desired);
pc.ReconcileCurrentObjectId(alice.ArrayIndex, 456);
Check(pc.IsClean, "NPC reuse retires a pending post-cleanup acknowledgement without player resolution");
pc = NewPersistent(); pp = MustPlan(pc, alice, sourceA, sourceA, true); pc.Commit(pp, pp.Desired);
pc.ReconcileCurrentObjectId(alice.ArrayIndex, alice.GameObjectId);
Check(!pc.IsClean && pc.OwnedCount == 1, "same-owner unresolved player remains conservative");
foreach (ulong transitional in new ulong[] {0, 0xE0000000, ulong.MaxValue}) pc.ReconcileCurrentObjectId(alice.ArrayIndex, transitional);
Check(pc.OwnedCount == 1, "transitional invalid IDs cannot falsely prove an owner change");
pc.PruneMissing(new HashSet<int>());
Check(pc.IsClean, "vanished unresolved owner retires managed state only once its slot is absent");
foreach (bool fc in new[] { false, true })
{
    foreach (var colorBase in new[] { Utf(""), Utf("Plain"), Utf("\u300AQuoted\u300B"), Utf(" \u00ABVIP\u00BB"), Join(color, Utf("Styled"), reset), Join(Utf("\u300A"), color, Utf("Inner"), reset, Utf("\u300B")) })
    {
        var c = new PersistentTitleController(bytes => NativeTitleSuffix.AppendColored(bytes, fc));
        var p = c.Plan(alice, colorBase, colorBase, true);
        if (p == null) { Check(!fc && Encoding.UTF8.GetString(colorBase).Contains("VIP"), "existing title VIP not duplicated"); continue; }
        Check(SeString.Parse(p.Desired).Encode().SequenceEqual(p.Desired), "colored field byte roundtrip");
        Check(p.Desired.Length > colorBase.Length, "colored field is an insertion");
        Check(c.Commit(p, p.Desired), "colored owned snapshot committed");
        Check(c.Plan(alice, p.Desired, colorBase, true) == null, "colored stable frame no rewrite/duplicate");
        var clean = c.Plan(alice, p.Desired, colorBase, false)!;
        Check(clean != null && clean.Desired.SequenceEqual(colorBase), "colored cleanup removes VIP AND color macros exactly");
        c.Commit(clean!, clean!.Desired);
        Check(c.ObserveCleanup(alice, colorBase, colorBase) && c.IsClean, "colored cleanup post acknowledgement");
    }
    var old = Utf(fc ? " \u00ABFC\u00BB" : "\u300AOld\u300B");
    var newer = Utf(fc ? " \u00ABNEW\u00BB" : "\u300ANew\u300B");
    var ctl = new PersistentTitleController(bytes => NativeTitleSuffix.AppendColored(bytes, fc));
    var first = ctl.Plan(alice, old, old, true)!; ctl.Commit(first, first.Desired);
    var next = ctl.Plan(alice, first.Desired, newer, true)!;
    Check(next.Base.SequenceEqual(newer), "colored latest upstream wins, not old title/FC");
    ctl.Commit(next, next.Desired);
    var exit = ctl.Plan(alice, next.Desired, newer, false)!;
    Check(exit.Desired.SequenceEqual(newer), "colored cleanup preserves latest upstream");
    var conflict = new PersistentTitleController(bytes => NativeTitleSuffix.AppendColored(bytes, fc));
    first = conflict.Plan(alice, old, old, true)!; conflict.Commit(first, first.Desired);
    Check(conflict.Plan(alice, Join(color, first.Desired, reset), old, false) == null && conflict.ConflictCount == 1, "colored uncertain wrappers fail closed, never orphan owned macros");
}
var fcOriginal = Utf(" \u00ABVIP\u00BB");
var fcColored = NativeTitleSuffix.AppendColored(fcOriginal, true)!;
Check(fcColored.AsSpan(0, fcOriginal.Length).SequenceEqual(fcOriginal), "actual FC named VIP retained unchanged before local marker");
var goldBuilder = new Lumina.Text.SeStringBuilder();
goldBuilder.PushColorRgba(new System.Numerics.Vector4(1f, .75f, .1f, 1f)).Append("VIP").PopColor();
var gold = goldBuilder.GetViewAsSpan().ToArray();
Check(fcColored.AsSpan(fcOriginal.Length + 1).SequenceEqual(gold), "only FC inserted VIP wrapped in fixed gold payload");
Check(NativeTitleSuffix.AppendColored(Utf("\u300ATitle\u300B"), false)!.SequenceEqual(Join(Utf("\u300ATitle "), gold, Utf("\u300B"))), "only title inserted VIP wrapped in gold before quote");
Check(NativeTitleSuffix.AppendColored(Utf("No quotes"), false)!.SequenceEqual(Join(Utf("No quotes "), gold)), "unquoted title remains unquoted and gold scoped");
Check(NativeTitleSuffix.AppendColored(Array.Empty<byte>(), false)!.SequenceEqual(Join(Utf("\u300A"), gold, Utf("\u300B"))), "empty title gold inside new quotes");
Check(NativeTitleSuffix.AppendColored(new byte[] {2,19}, true) == null, "malformed FC payload rejected");
foreach (string hex in new[] { "#1122AA", "1122aa", " #FF0000 " })
{
    Check(NativeTagSettings.TryHex(hex, out var rgb), "valid native hex accepted");
    Check(rgb.W == 1 && NativeTagSettings.TryHex(NativeTagSettings.ToHex(rgb), out var back) && rgb == back, "native hex RGB roundtrip opaque");
}
foreach (string bad in new[] { "", "#FFF", "GG2211", "#11223344", "1234567", "0xFFFF" }) Check(!NativeTagSettings.TryHex(bad, out _), "invalid hex does not overwrite saved color");
var normalized = NativeTagSettings.NormalizeColor(new System.Numerics.Vector4(float.NaN, float.PositiveInfinity, -2, 0));
Check(float.IsFinite(normalized.X) && normalized.Y == 1 && normalized.Z == 0 && normalized.W == 1, "native color clamps malformed configuration and uses opaque RGB");
var cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>("{\"Version\":2,\"ActiveProfileIndex\":0,\"Profiles\":[{\"Name\":\"Existing\",\"ShowHighlightRing\":false,\"VipTagOffsetX\":17,\"VipTagColor\":{\"X\":0.2,\"Y\":0.4,\"Z\":0.6,\"W\":0.5}}]}")!;
var existingProfile = cfg.GetActiveProfile();
Check(existingProfile.TagPlacement == VipTagPlacement.Floating && !existingProfile.ShowHighlightRing && existingProfile.VipTagOffsetX == 17 && existingProfile.VipTagColor.W == .5f, "existing v2 profiles preserve overlay defaults ring offsets and alpha");
existingProfile.TagPlacement = VipTagPlacement.NativeFreeCompany;
var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(Newtonsoft.Json.JsonConvert.SerializeObject(cfg))!;
Check(saved.GetActiveProfile().TagPlacement == VipTagPlacement.NativeFreeCompany && saved.GetActiveProfile().VipTagColor == existingProfile.VipTagColor, "new placement and shared color persist without legacy loss");
Check(!NativeTagSettings.IsNative((VipTagPlacement)99) && !NativeTagSettings.IsNative(VipTagPlacement.Floating), "unknown/floating modes never qualify to arm native");
var snap = NativeArmSelection.From(existingProfile);
existingProfile.VipTagColor = new System.Numerics.Vector4(1,0,0,.2f);
Check(snap != NativeArmSelection.From(existingProfile), "color change invalidates armed selection");
snap = NativeArmSelection.From(existingProfile); existingProfile.TagPlacement = VipTagPlacement.NativeTitle;
Check(snap != NativeArmSelection.From(existingProfile), "placement change invalidates armed selection");
snap = NativeArmSelection.From(existingProfile); existingProfile.Id = Guid.NewGuid();
Check(snap != NativeArmSelection.From(existingProfile), "profile change invalidates armed selection");
snap = NativeArmSelection.From(existingProfile); existingProfile.IsOverlayEnabled = false;
Check(snap != NativeArmSelection.From(existingProfile), "master change invalidates armed selection");
snap = NativeArmSelection.From(existingProfile); existingProfile.ShowVipTag = false;
Check(snap != NativeArmSelection.From(existingProfile), "tag change invalidates armed selection");
snap = NativeArmSelection.From(existingProfile); existingProfile.SpreadsheetId = "different";
Check(snap != NativeArmSelection.From(existingProfile), "sheet change invalidates armed selection");
var red = new System.Numerics.Vector4(1,0,0,1);
var redBuilder = new Lumina.Text.SeStringBuilder(); redBuilder.PushColorRgba(red).Append("VIP").PopColor();
Check(NativeTitleSuffix.AppendColored(Utf("Base"), false, red)!.SequenceEqual(Join(Utf("Base "), redBuilder.GetViewAsSpan().ToArray())), "configured native RGB payload only surrounds local marker");
var emptyManager = new VipManager(new Configuration());
emptyManager.SetTemporaryVip(" Test Person ", true);
Check(emptyManager.Revision == 1, "temporary membership increments managed revision");
emptyManager.ClearTemporaryVips();
Check(emptyManager.Revision == 2, "session clear increments managed revision");
emptyManager.Dispose(); emptyManager.Dispose();
Check(true, "manager disposal is idempotent without touching game or config");
var proxy = System.Reflection.DispatchProxy.Create<Dalamud.Plugin.IDalamudPluginInterface, ConfigOnlyProxy>();
var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>("{\"Version\":1,\"SpreadsheetId\":\"legacy-sheet\",\"ShowHighlightRing\":false,\"VipListRange\":42}")!;
legacy.Initialize(proxy);
Check(legacy.Profiles.Count == 1 && legacy.GetActiveProfile().SpreadsheetId == "legacy-sheet" && legacy.GetActiveProfile().TagPlacement == VipTagPlacement.Floating && legacy.GetActiveProfile().VipListRange == 42 && !legacy.GetActiveProfile().ShowHighlightRing, "legacy migration retains sheet ring and range while defaulting floating mode");
Check(legacy.SpreadsheetId == null && legacy.Profiles[0].Columns.Count == 4, "legacy migration clears old fields and retains original default columns");
Check((int)VipTagPlacement.Floating == 0 && (int)VipTagPlacement.NativeTitle == 1 && (int)VipTagPlacement.NativeFreeCompany == 2 && (int)VipTagPlacement.NativeTitlePrefix == 3, "prefix enum appended without renumbering saved modes");
Check(NativeTagSettings.IsNative(VipTagPlacement.NativeTitlePrefix), "prefix recognized as guarded native mode");
var prefixGold = goldBuilder.GetViewAsSpan().ToArray();
foreach (var item in new[] {
    (Utf("\u300ATitle\u300B"), Join(Utf("\u300A"), prefixGold, Utf(" Title\u300B"))),
    (Utf("Plain"), Join(prefixGold, Utf(" Plain"))),
    (Array.Empty<byte>(), Join(Utf("\u300A"), prefixGold, Utf("\u300B"))),
    (Utf("\u300A\u300B"), Join(Utf("\u300A"), prefixGold, Utf("\u300B"))),
    (Join(color, Utf("\u300AStyled\u300B"), reset), Join(color, Utf("\u300A"), prefixGold, Utf(" Styled\u300B"), reset)),
    (Join(Utf("\u300A"), color, Utf("Inner"), reset, Utf("\u300B")), Join(Utf("\u300A"), prefixGold, Utf(" "), color, Utf("Inner"), reset, Utf("\u300B"))),
    (Utf("\u00ABCustom\u00BB"), Join(Utf("\u00AB"), prefixGold, Utf(" Custom\u00BB"))),
    (Utf("\"Quoted\""), Join(Utf("\""), prefixGold, Utf(" Quoted\""))) })
{
    var result = NativeTitleSuffix.AppendColored(item.Item1, false, null, true)!;
    Check(result.SequenceEqual(item.Item2), "prefix exact quote spacing and original payload preservation");
    Check(SeString.Parse(result).Encode().SequenceEqual(result), "prefix installed setter byte roundtrip");
    var c = new PersistentTitleController(bytes => NativeTitleSuffix.AppendColored(bytes, false, null, true));
    var plan = c.Plan(alice, item.Item1, item.Item1, true)!; c.Commit(plan, plan.Desired);
    Check(c.Plan(alice, plan.Desired, item.Item1, true) == null, "prefix stable frame no rewrite/duplicate");
    var clean = c.Plan(alice, plan.Desired, item.Item1, false)!;
    Check(clean.Desired.SequenceEqual(item.Item1), "prefix removes gold macros and VIP, restores exact source");
    c.Commit(clean, clean.Desired);
    Check(c.ObserveCleanup(alice, clean.Desired, item.Item1) && c.IsClean, "prefix cleanup post acknowledged");
}
foreach (string natural in new[] {"VIP", "VIP Leader", "\u300AVIP Leader\u300B", "[VIP] Leader", "vip leader"})
    Check(NativeTitleSuffix.AppendColored(Utf(natural), false, null, true) == null, "natural existing VIP prefix left intact");
Check(NativeTitleSuffix.AppendColored(new byte[] {255}, false, null, true) == null, "malformed prefix UTF8 rejected");
Check(NativeTitleSuffix.AppendColored(new byte[] {2,19}, false, null, true) == null, "malformed prefix payload rejected");
var prefixCtl = new PersistentTitleController(bytes => NativeTitleSuffix.AppendColored(bytes, false, null, true));
var prefixOld = Utf("Old"); var prefixNew = Utf("New");
var prefixPlan = prefixCtl.Plan(alice, prefixOld, prefixOld, true)!; prefixCtl.Commit(prefixPlan, prefixPlan.Desired);
var prefixNext = prefixCtl.Plan(alice, prefixPlan.Desired, prefixNew, true)!; prefixCtl.Commit(prefixNext, prefixNext.Desired);
Check(prefixNext.Desired.SequenceEqual(Join(prefixGold, Utf(" New"))), "prefix latest synced title replaces old source");
var prefixClean = prefixCtl.Plan(alice, prefixNext.Desired, prefixNew, false)!;
Check(prefixClean.Desired.SequenceEqual(prefixNew), "prefix clean returns latest source not stale title");
var prefixProfile = new VipProfile { TagPlacement = VipTagPlacement.NativeTitle };
var beforePrefixSwitch = NativeArmSelection.From(prefixProfile); prefixProfile.TagPlacement = VipTagPlacement.NativeTitlePrefix;
Check(beforePrefixSwitch != NativeArmSelection.From(prefixProfile), "suffix-to-prefix setting invalidates armed snapshot and requires cleanup");
Check(Newtonsoft.Json.JsonConvert.DeserializeObject<VipProfile>(Newtonsoft.Json.JsonConvert.SerializeObject(prefixProfile))!.TagPlacement == VipTagPlacement.NativeTitlePrefix, "prefix mode config roundtrip");
RuntimeEnableChecks.Run(Check);
Console.WriteLine($"{passed} checks passed. No game process or live settings accessed.");









public class ConfigOnlyProxy : System.Reflection.DispatchProxy
{
    public static int Saves;
    protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "SavePluginConfig") { Saves++; return null; }
        throw new InvalidOperationException("Unexpected config-only stub call: " + targetMethod?.Name);
    }
}