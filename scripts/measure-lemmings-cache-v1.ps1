[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReferenceDirectory,
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$DiskPath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][int]$LogicalProcessor,
    [switch]$ValidateOnly,
    [switch]$LoadedHostDiagnostics
)

# lemmings-interface-cache-v1: six engine-only pairs with one common identity.
# Both qualified capsules differ only in the engine DLL's capability cache.
# This does not replace historical three-workload retention or release gates.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1') -Force
. (Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1')
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new immutable evidence directory.' }
$null = New-Item -ItemType Directory -Path $output
$record = [ordered]@{
    Protocol='lemmings-interface-cache-v1'; Status='RUNNING'; StartedUtc=[DateTime]::UtcNow.ToString('o')
    Scope='Qualified current engine without versus with interface cache; identical runner and dependencies; native Lemmings only'
    AcceptanceMeasurement=$false; Samples=@(); HostLoadPolicy='v2, unchanged limits and rejection rules'
    LoadedHostDiagnostics=[bool]$LoadedHostDiagnostics; HostLoadViolations=@()
}
$process = $null
$sampleLabel = 'preflight'
try {
    if (-not $IsWindows) { throw 'Windows native CPU-set topology and telemetry required.' }
    $repo = Split-Path $PSScriptRoot -Parent
    $oldContract = Join-Path $repo 'docs/engine/PERFORMANCE_BASELINE_2026-10-12.json'
    $currentContract = Join-Path $repo 'docs/engine/LEMMINGS_CORRECTNESS_2026-11-02.json'
    $cacheContract = Join-Path $repo 'docs/engine/INTERRUPT_BOOKKEEPING_2026-11-02.json'
    if ((Get-FileHash $oldContract).Hash -cne 'BA7A5977E0F3F2E2B984F393220CECAA3393E326E458A452F9DBF7BA4803FF00' -or
        (Get-FileHash $currentContract).Hash -cne '663D84DEFAFEA783AA7353EAF50D21F35E9DBD1CAB734AFEA1F850967E7FAEA9' -or
        (Get-FileHash $cacheContract).Hash -cne 'D8AFA045BF53A81CD3E738829BF8D8FE42DE1CA15519193913E0CE7C74720AE8') {
        throw 'Reviewed identity contract changed; version and validate a new protocol.'
    }
    $old = Get-Content $oldContract -Raw | ConvertFrom-Json
    $current = Get-Content $currentContract -Raw | ConvertFrom-Json
    $cache = Get-Content $cacheContract -Raw | ConvertFrom-Json
    $expected = @{
        R = $cache.NativeBenchmarkIdentity
        C = 'engine=lightweight-a500 workload=image executionMode=Interpreter cpuMode=conservative-batch warmup=14520 frames=3600 completed=18120 ' +
            $current.NativeReplay.CurrentBenchmarkIdentity.Replace(' unsupported=none',' adf=True unsupported=none')
    }
    if ($expected.R -cne $expected.C) { throw 'Qualified uncached and cached identities must match exactly.' }
    foreach ($identity in $expected.Values) {
        $good = $identity.Replace(' cycle=', ' fps=300.00 cycle=')
        $null = Read-CopperScreenBaselineSample $good $identity
        foreach ($bad in @($good.Replace('allocated=0','allocated=1'), $good.Replace('pcm=real','pcm=none'),
            $good.Replace('frames=3600','frames=3599'), $good.Replace('output=0x','output=0x0'))) {
            $rejected = $false
            try { $null = Read-CopperScreenBaselineSample $bad $identity } catch { $rejected = $true }
            if (-not $rejected) { throw 'Identity rejection control failed.' }
        }
    }
    $record.ExpectedIdentities = $expected
    $directories = @{ R=(Resolve-Path $ReferenceDirectory).Path; C=(Resolve-Path $CandidateDirectory).Path }
    $rom = (Resolve-Path -LiteralPath $RomPath).Path
    $disk = (Resolve-Path -LiteralPath $DiskPath).Path
    $script = (Resolve-Path -LiteralPath $InputScriptPath).Path
    $inputs = @($rom,$disk,$script)
    foreach ($entry in (Get-Content $script -Raw | ConvertFrom-Json)) {
        if ($entry.PSObject.Properties['adfPath']) { $inputs += [IO.Path]::GetFullPath($entry.adfPath,(Split-Path $script -Parent)) }
    }
    if ($inputs.Count -ne 4) { throw 'Expected retained ROM, disk 1, script and disk 2.' }
    foreach ($path in $inputs) {
        $pin = @($old.Collection.Inputs | Where-Object File -ceq ([IO.Path]::GetFileName($path)))
        if ($pin.Count -ne 1 -or (Get-FileHash -LiteralPath $path).Hash -cne $pin[0].Sha256) { throw "Workload input differs: $path" }
    }
    $referencePins=@{
        'CopperMod.Amiga.Lightweight.dll'='859B6D399CE169189C27581D634A7DCC6165503B186828D7BCE00050F4380A98'
    }
    foreach ($name in $referencePins.Keys) {
        if ((Get-FileHash (Join-Path $directories.R $name)).Hash -cne $referencePins[$name]) { throw "Reconstructed reference differs from the qualified replay: $name" }
    }
    if ((Get-FileHash (Join-Path $directories.C 'CopperMod.Amiga.Lightweight.dll')).Hash -cne '8056A36DBABF5604408914C09F7FDD0AC7FF8D07D60929A764AF3AD8C6332AE5') { throw 'Cached engine differs from qualification.' }
    foreach ($pin in $current.CurrentBinaries | Where-Object File -cne 'CopperMod.Amiga.Lightweight.dll') {
        foreach ($role in @('R','C')) {
            if ((Get-FileHash (Join-Path $directories[$role] $pin.File)).Hash -cne $pin.Sha256) { throw "Unqualified shared binary: $($pin.File)" }
        }
    }
    $rFiles=@(Get-ChildItem $directories.R -File | Sort-Object Name)
    $cFiles=@(Get-ChildItem $directories.C -File | Sort-Object Name)
    if (($rFiles.Name -join '|') -cne ($cFiles.Name -join '|')) { throw 'Capsule file sets differ.' }
    foreach ($file in $rFiles | Where-Object Name -cne 'CopperMod.Amiga.Lightweight.dll') {
        if ((Get-FileHash $file.FullName).Hash -cne (Get-FileHash (Join-Path $directories.C $file.Name)).Hash) { throw "Only the engine DLL may differ: $($file.Name)" }
    }
    $dotnet = (Get-Command dotnet).Source
    $runtimeDirectory = Join-Path (Split-Path $dotnet -Parent) 'shared/Microsoft.NETCore.App/10.0.12'
    $paths = @($inputs) + @($PSCommandPath,$oldContract,$currentContract,$cacheContract,
        (Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1'),
        (Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1'))
    foreach ($directory in $directories.Values) { $paths += @(Get-ChildItem $directory -File | ForEach-Object FullName) }
    $paths += @(Get-ChildItem $runtimeDirectory -File | ForEach-Object FullName)
    $files = @($paths | ForEach-Object { Get-CopperScreenQualificationFile $_ })
    $record.Files=$files; $record.Directories=$directories
    $record.SourceHead=(git -C $repo rev-parse HEAD); $record.Runtime=(& $dotnet --info | Out-String)
    $record.Cpu=(Get-CimInstance Win32_Processor).Name; $record.Windows=[Environment]::OSVersion.ToString()
    $record.PowerPlan=(powercfg /getactivescheme | Out-String).Trim()
    $record.SharedCapsuleFilesMatch=$true
    $record.RuntimeEnvironment=@(Get-ChildItem Env: | Where-Object Name -match '^(DOTNET_|COMPlus_)' | Select-Object Name,Value)
    if (@($record.RuntimeEnvironment | Where-Object Name -match '(JitDisasm|JitDump|JitStdOut|TieredCompilation|TieredPGO)').Count) { throw 'Remove diagnostic/JIT overrides before production cache timing.' }
    $topology = @(Get-CopperScreenQualificationTopology)
    $counters = @(Get-G6ProcessorCounters)
    if (@($topology.Group | Select-Object -Unique).Count -ne 1 -or $topology[0].Group -ne 0 -or
        @($topology.Efficiency | Select-Object -Unique).Count -ne 1 -or $topology.Count -ne $counters.Count -or
        @($topology.Cpu | Select-Object -Unique).Count -ne $counters.Count) { throw 'Homogeneous single-group topology required.' }
    $selected = @($topology | Where-Object Cpu -eq $LogicalProcessor)
    if ($selected.Count -ne 1 -or $LogicalProcessor -lt 0 -or $LogicalProcessor -ge 63) { throw 'Invalid logical CPU.' }
    $protected = @($topology | Where-Object Core -eq $selected[0].Core | ForEach-Object Cpu)
    $mask = [int64]1 -shl $LogicalProcessor
    $record.Topology=$topology; $record.LogicalProcessor=$LogicalProcessor; $record.ProtectedCpus=$protected; $record.Priority='Normal'
    $state=@{}
    function Snapshot($benchmark=$null) {
        $snapshot=Get-G6HostSnapshot $benchmark
        # Native build tools are additional explicit competing jobs. Idle reusable
        # MSBuild nodes remain governed by the shared policy, not process presence.
        $nativeBuilds=@(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -match '^(cl|clang|clang-cl|UnrealHeaderTool)(\.exe)?$'
        } | ForEach-Object { "$($_.Name)[$($_.ProcessId)]" })
        $snapshot.Workloads += $nativeBuilds
        return $snapshot
    }
    function Observe($before,$after,$benchmark=$null) {
        $load=Get-G6HostLoad $before $after $LogicalProcessor $protected
        $others=@(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {
            ($null -eq $benchmark -or $_.ProcessId -ne $benchmark.Id) -and $_.CommandLine -match 'CopperMod\.Amiga\.Lightweight\.Runner\.dll'
        })
        if ($others.Count) { $load.Workloads += @($others | ForEach-Object { "Runner[$($_.ProcessId)]" }) }
        $load | Add-Member -NotePropertyName Sample -NotePropertyValue $sampleLabel
        $load | ConvertTo-Json -Depth 5 -Compress | Add-Content (Join-Path $output 'telemetry.jsonl')
        try { Update-G6HostLoadState $state $load 25 25 10 }
        catch {
            if (-not $LoadedHostDiagnostics) { throw }
            # Continue only policy violations at the owner's explicit request.
            # Counter/placement/identity/file failures still stop unconditionally.
            $record.HostLoadViolations += [pscustomobject]@{Sample=$sampleLabel;Message=$_.Exception.Message}
        }
    }
    function Cooldown {
        $before=Snapshot
        $start=$before.TimeSeconds
        do { Start-Sleep -Seconds 1; $after=Snapshot; Observe $before $after; $before=$after }
        while ($after.TimeSeconds-$start -lt 10)
    }
    $rejected=$false
    try { $null=Get-G6HostLoad ([pscustomobject]@{Cores=@{}}) ([pscustomobject]@{Cores=@{}}) $LogicalProcessor $protected } catch { $rejected=$true }
    if (-not $rejected) { throw 'Missing telemetry accepted.' }
    $rejected=$false
    try { Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=10;Selected=26;Sibling=0;Package=0;Workloads=@()}) 25 25 10 } catch { $rejected=$true }
    if (-not $rejected) { throw 'Sustained noise accepted.' }
    Cooldown
    if ($ValidateOnly) { $record.Status=if($record.HostLoadViolations.Count){'INVALID / RERUN'}else{'VALIDATED_PREFLIGHT_ONLY'}; return }
    $order=@('R1','C1','C2','R2','R3','C3','C4','R4','R5','C5','C6','R6')
    $record.Order=$order
    foreach ($label in $order) {
        $sampleLabel=$label; $role=$label.Substring(0,1)
        Assert-CopperScreenQualificationFiles $files
        $runner=Join-Path $directories[$role] 'CopperMod.Amiga.Lightweight.Runner.dll'
        $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
        $start.UseShellExecute=$false; $start.CreateNoWindow=$true
        $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
        foreach ($argument in @('exec','--fx-version','10.0.12',$runner,'--rom',$rom,'--adf',$disk,'--input-script',$script,'--warmup','14520','--frames','3600')) { $start.ArgumentList.Add($argument) }
        $start.Environment['DOTNET_ROLL_FORWARD']='Disable'
        $process=[Diagnostics.Process]::Start($start)
        $process.PriorityClass='Normal'; $process.ProcessorAffinity=[IntPtr]$mask
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        $before=Snapshot $process; $timer=[Diagnostics.Stopwatch]::StartNew()
        while (-not $process.HasExited) {
            Start-Sleep -Seconds 1
            $after=Snapshot $process
            if (-not $process.HasExited -and ($process.ProcessorAffinity.ToInt64() -ne $mask -or $process.PriorityClass -ne 'Normal')) { throw 'Placement or priority changed.' }
            Observe $before $after $process; $before=$after
            if ($timer.Elapsed.TotalSeconds -gt 600) { throw 'Complete replay timed out.' }
        }
        $process.WaitForExit()
        $out=$stdout.GetAwaiter().GetResult(); $err=$stderr.GetAwaiter().GetResult()
        $out | Set-Content (Join-Path $output "$label.stdout.log")
        $err | Set-Content (Join-Path $output "$label.stderr.log")
        if ($process.ExitCode -ne 0) { throw "Runner exited $($process.ExitCode)." }
        $lines=@($out -split '\r?\n' | Where-Object { $_ -clike 'engine=lightweight-a500 *' })
        if ($lines.Count -ne 1) { throw 'Expected one complete workload result.' }
        $sample=Read-CopperScreenBaselineSample $lines[0] $expected[$role]
        $record.Samples += [pscustomobject]@{Label=$label;Role=$role;Pair=[int]$label.Substring(1);Fps=$sample.Fps;MsPerField=$sample.MsPerField;Fingerprint=$sample.Fingerprint}
        $record | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'comparison-record.json')
        Write-Host "$label FPS=$($sample.Fps) ms/field=$($sample.MsPerField) identity=match"
        $process.Dispose(); $process=$null
        $sampleLabel="$label-cooldown"; Cooldown
    }
    Assert-CopperScreenQualificationFiles $files
    $logs=@(1..6 | ForEach-Object {
        $r=@($record.Samples | Where-Object Label -ceq "R$_")[0]
        $c=@($record.Samples | Where-Object Label -ceq "C$_")[0]
        [math]::Log($r.Fps/$c.Fps)
    })
    $mean=($logs | Measure-Object -Average).Average
    $squares=($logs | ForEach-Object { ($_-$mean)*($_-$mean) } | Measure-Object -Sum).Sum
    $margin=2.0150483733330233*[math]::Sqrt($squares/5/6)
    $record.Comparison=[ordered]@{PairedThroughputChangePercent=100*([math]::Exp(-$mean)-1);PairedFrameTimeChangePercent=100*([math]::Exp($mean)-1);OneSided95UpperPercent=100*([math]::Exp($mean+$margin)-1);OneSided95LowerPercent=100*([math]::Exp($mean-$margin)-1);Pairs=6;DegreesOfFreedom=5}
    $record.Summary=@('R','C' | ForEach-Object {
        $samples=@($record.Samples | Where-Object Role -ceq $_)
        [pscustomobject]@{Role=$_;Samples=$samples.Count;MeanFps=($samples.Fps|Measure-Object -Average).Average;MinFps=($samples.Fps|Measure-Object -Minimum).Minimum;MaxFps=($samples.Fps|Measure-Object -Maximum).Maximum;MeanMsPerField=($samples.MsPerField|Measure-Object -Average).Average}
    })
    $record.Status=if($record.HostLoadViolations.Count){'INVALID / RERUN'}else{'VALID_NATIVE_COMPARISON'}
    $record.ComparisonInterpretation=if($record.HostLoadViolations.Count){'Loaded-host descriptive pairs only. Statistical interval does not remove host interference; no causal cache impact or retention decision.'}else{'Native engine-only cache comparison; not full three-workload or release acceptance.'}
    $record.FrozenFilesUnchanged=$true
}
catch { $record.Status='INVALID / RERUN'; $record.Error=$_.Exception.ToString(); $record.FailedSample=$sampleLabel; throw }
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $stdout.GetAwaiter().GetResult() | Set-Content (Join-Path $output "$sampleLabel.partial.stdout.log")
        $stderr.GetAwaiter().GetResult() | Set-Content (Join-Path $output "$sampleLabel.partial.stderr.log")
        $process.Dispose()
    }
    $record.CompletedUtc=[DateTime]::UtcNow.ToString('o')
    $record | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'comparison-record.json')
}
