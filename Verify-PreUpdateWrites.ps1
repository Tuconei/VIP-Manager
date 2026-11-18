param([string]$AssemblyPath = (Join-Path $PSScriptRoot 'bin\Release\VipNameChecker.dll'))
$ErrorActionPreference = 'Stop'
$sdkPath = Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev'
Add-Type -Path (Join-Path $sdkPath 'Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath)
try {
    $module = $assembly.MainModule
    $type = $module.Types | Where-Object FullName -eq 'VipNameChecker.NativeVipTitles'
    $forbidden = '::set_(Title|FreeCompanyTag)\(|::SetField\(|StringArrayData::SetValue|Utf8String::SetString|AtkTextNode::SetText|NativeMemory::(Free|Alloc)'
    foreach ($entryName in @('AfterNativeUpdate', 'Dispose')) {
        $entry = $type.Methods | Where-Object Name -eq $entryName
        if (!$entry) { throw "Missing entry: $entryName" }
        $queue = [System.Collections.Generic.Queue[Mono.Cecil.MethodDefinition]]::new()
        $visited = [System.Collections.Generic.HashSet[string]]::new()
        $queue.Enqueue($entry)
        while ($queue.Count -gt 0) {
            $method = $queue.Dequeue()
            if (!$visited.Add($method.FullName) -or !$method.HasBody) { continue }
            foreach ($instruction in $method.Body.Instructions) {
                $reference = $instruction.Operand
                if ($reference -isnot [Mono.Cecil.MethodReference]) { continue }
                $code = $instruction.OpCode.Code.ToString()
                if ($code -in @('Ldftn', 'Ldvirtftn')) {
                    # Dispose constructs existing handler delegates solely to remove
                    # event subscriptions; loading those references does not call them.
                    if ($entryName -eq 'Dispose' -and $reference.DeclaringType.FullName -eq 'VipNameChecker.NativeVipTitles' -and
                        $reference.Name -in @('BeforeNativeUpdate','AfterNativeUpdate','OnFrameworkUpdate')) { continue }
                    # Other delegate references are followed conservatively.
                } elseif ($code -notin @('Call', 'Callvirt', 'Newobj')) { continue }
                if ($reference.FullName -match $forbidden) {
                    throw "Forbidden post/dispose title write or native allocation: $($method.FullName) -> $($reference.FullName)"
                }
                if ($reference.DeclaringType.Scope -is [Mono.Cecil.ModuleDefinition] -and $reference.DeclaringType.Scope.Mvid -eq $module.Mvid) {
                    $resolved = $reference.Resolve()
                    if ($resolved) { $queue.Enqueue($resolved) }
                }
            }
        }
        "PASS $entryName reachable local methods audited: $($visited.Count); no title-buffer writes or manual native allocation/free calls"
    }
    $pre = $type.Methods | Where-Object Name -eq 'BeforeNativeUpdate'
    $writes = @($pre.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'INamePlateUpdateHandler::set_(Title|FreeCompanyTag)\(' })
    if ($writes.Count -ne 2) { throw "Expected two supported pre-update field setters call site, found $($writes.Count)" }
    'PASS two supported field setter call sites is in BeforeNativeUpdate'
    'This verifies the managed call boundary, not native rendering/occlusion correctness.'
} finally { $assembly.Dispose() }

