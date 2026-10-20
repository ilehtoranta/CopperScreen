[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunnerDirectory,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$DiskPath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][int]$LogicalProcessor,
    [string]$ContractPath = (Join-Path $PSScriptRoot '../docs/engine/PERFORMANCE_BASELINE_2026-10-12.json'),
    [switch]$ValidateOnly,
    [switch]$LoadedHostDiagnostics
)

# copperscreen-release-engine-baseline-v1: descriptive, not installed-app or release-budget acceptance.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1') -Force
. (Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1')
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence directory; earlier results are immutable.' }
$null = New-Item -ItemType Directory -Path $output
$record = [ordered]@{Protocol='copperscreen-release-engine-baseline-v1';Status='RUNNING';AcceptanceMeasurement=$false;InstalledAppMeasurement=$false;ReleaseBudgetsFrozen=$false;StartedUtc=[DateTime]::UtcNow.ToString('o');Samples=@()}
$record.LoadedHostDiagnostics = [bool]$LoadedHostDiagnostics
$record.HostLoadViolations = @()
$process = $null
$phase = 'preflight'
try {
    if (-not $IsWindows) { throw 'Windows CPU-set topology and native telemetry are required.' }
    $runner = Join-Path (Resolve-Path -LiteralPath $RunnerDirectory).Path 'CopperMod.Amiga.Lightweight.Runner.dll'
    $rom = (Resolve-Path -LiteralPath $RomPath).Path
    $disk = (Resolve-Path -LiteralPath $DiskPath).Path
    $script = (Resolve-Path -LiteralPath $InputScriptPath).Path
    $contractFile = Get-CopperScreenQualificationFile $ContractPath
    if ($contractFile.Sha256 -cne 'BA7A5977E0F3F2E2B984F393220CECAA3393E326E458A452F9DBF7BA4803FF00') { throw 'The reviewed baseline contract changed; version and validate a new protocol.' }
    $contract = Get-Content -LiteralPath $ContractPath -Raw | ConvertFrom-Json
    $inputs = @($rom, $disk, $script)
    foreach ($entry in (Get-Content -LiteralPath $script -Raw | ConvertFrom-Json)) {
        if ($entry.PSObject.Properties['adfPath']) { $inputs += [IO.Path]::GetFullPath($entry.adfPath, (Split-Path $script -Parent)) }
    }
    if ($inputs.Count -ne 4) { throw 'Expected the retained ROM, first disk, script and second disk only.' }
    foreach ($path in $inputs) {
        $expected = @($contract.Collection.Inputs | Where-Object File -ceq ([IO.Path]::GetFileName($path)))
        if ($expected.Count -ne 1 -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $expected[0].Sha256) { throw "Unqualified retained workload input: $path" }
    }
    $expectedIdentities = @{}
    foreach ($name in @('lores','hires','lemmings')) {
        $identities = @($contract.Samples | Where-Object Workload -ceq $name | ForEach-Object Fingerprint | Select-Object -Unique)
        if ($identities.Count -ne 1) { throw 'Historical complete workload identity is ambiguous.' }
        $expectedIdentities[$name] = $identities[0]
    }
    $dotnet = (Get-Command dotnet).Source
    $runtimeDirectory = Join-Path (Split-Path $dotnet -Parent) 'shared/Microsoft.NETCore.App/10.0.12'
    $runtimeFiles = @(Get-ChildItem -LiteralPath $runtimeDirectory -File | ForEach-Object { Get-CopperScreenQualificationFile $_.FullName })
    $files = @($inputs + @($runner, $PSCommandPath, (Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1'), (Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1'), $ContractPath) |
        ForEach-Object { Get-CopperScreenQualificationFile $_ })
    $files += @(Get-ChildItem -LiteralPath (Split-Path $runner -Parent) -File | ForEach-Object { Get-CopperScreenQualificationFile $_.FullName })
    $record.Files = $files; $record.RuntimeFiles = $runtimeFiles
    $record.Contract = $contractFile; $record.ExpectedIdentities = $expectedIdentities
    $record.Runtime = (& $dotnet --info | Out-String)
    $record.SourceHead = (git -C (Join-Path $PSScriptRoot '..') rev-parse HEAD)
    $record.WorkingTreeDirty = @(git -C (Join-Path $PSScriptRoot '..') status --porcelain).Count -gt 0
    $record.Windows = [Environment]::OSVersion.ToString()
    $record.Cpu = (Get-CimInstance Win32_Processor).Name
    $record.PowerPlan = (powercfg /getactivescheme | Out-String).Trim()
    $topology = @(Get-CopperScreenQualificationTopology)
    $counters = @(Get-G6ProcessorCounters)
    if (@($topology.Group | Select-Object -Unique).Count -ne 1 -or $topology[0].Group -ne 0 -or
        @($topology.Efficiency | Select-Object -Unique).Count -ne 1 -or $topology.Count -ne $counters.Count -or
        @($topology.Cpu | Select-Object -Unique).Count -ne $counters.Count) { throw 'Validated homogeneous single-group topology required; do not weaken a hybrid protocol.' }
    $selected = @($topology | Where-Object Cpu -eq $LogicalProcessor)
    if ($selected.Count -ne 1 -or $LogicalProcessor -lt 0 -or $LogicalProcessor -ge 63) { throw 'Invalid selected logical processor.' }
    $protected = @($topology | Where-Object Core -eq $selected[0].Core | ForEach-Object Cpu)
    $mask = [int64]1 -shl $LogicalProcessor
    $record.Topology = $topology; $record.LogicalProcessor = $LogicalProcessor; $record.ProtectedCpus = $protected; $record.Priority = 'Normal'
    $state = @{}
    $sampleLabel = 'preflight'
    function Observe($before, $after, $benchmark = $null) {
        $load = Get-G6HostLoad $before $after $LogicalProcessor $protected
        $otherRunners = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
            ($null -eq $benchmark -or $_.ProcessId -ne $benchmark.Id) -and $_.CommandLine -match 'CopperMod\.Amiga\.Lightweight\.Runner\.dll'
        })
        if ($otherRunners.Count) { $load.Workloads += @($otherRunners | ForEach-Object { "Runner[$($_.ProcessId)]" }) }
        $load | Add-Member -NotePropertyName Sample -NotePropertyValue $sampleLabel
        $load | ConvertTo-Json -Depth 5 -Compress | Add-Content -LiteralPath (Join-Path $output 'telemetry.jsonl')
        try { Update-G6HostLoadState $state $load 25 25 10 }
        catch {
            if (-not $LoadedHostDiagnostics) { throw }
            # Only explicit load diagnostics continue; telemetry/placement/fingerprint errors still stop.
            # Every contaminated sample and the entire series remain INVALID / RERUN.
            $record.HostLoadViolations += [pscustomobject]@{Sample=$sampleLabel;Message=$_.Exception.Message}
        }
    }
    function Cooldown {
        $before = Get-G6HostSnapshot
        $start = $before.TimeSeconds
        do {
            Start-Sleep -Seconds 1
            $after = Get-G6HostSnapshot
            Observe $before $after
            $before = $after
        } while ($after.TimeSeconds - $start -lt 10)
    }
    # Fail-closed controls run before any timed process.
    $rejected = $false
    try { Get-G6HostLoad ([pscustomobject]@{Cores=@{}}) ([pscustomobject]@{Cores=@{}}) $LogicalProcessor $protected } catch { $rejected = $true }
    if (-not $rejected) { throw 'Missing telemetry accepted.' }
    $rejected = $false
    try { Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=10;Selected=26;Sibling=0;Package=0;Workloads=@()}) 25 25 10 } catch { $rejected = $true }
    if (-not $rejected) { throw 'Sustained interference accepted.' }
    Cooldown
    if ($ValidateOnly) { $record.Status = if ($record.HostLoadViolations.Count) { 'INVALID / RERUN' } else { 'VALIDATED_PREFLIGHT_ONLY' }; return }
    $workloads = @{
        lores = @('--synthetic-paula-dma','--wide-output','--warmup','600','--frames','7200')
        hires = @('--synthetic-paula-dma','--hires','--warmup','600','--frames','7200')
        lemmings = @('--rom',$rom,'--adf',$disk,'--input-script',$script,'--warmup','14520','--frames','3600')
    }
    # Same nine-run order as the October 12 descriptive record; no paired regression claim.
    $order = @('lores','hires','lemmings','lemmings','hires','lores','hires','lemmings','lores')
    $record.Order = $order
    $index = 0
    foreach ($name in $order) {
        $sampleLabel = '{0:D2}-{1}' -f (++$index), $name
        Assert-CopperScreenQualificationFiles ($files + $runtimeFiles)
        $phase = 'measurement'
        $start = [Diagnostics.ProcessStartInfo]::new($dotnet)
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        foreach ($argument in @('exec','--fx-version','10.0.12',$runner) + $workloads[$name]) { $start.ArgumentList.Add($argument) }
        $start.Environment['DOTNET_ROLL_FORWARD'] = 'Disable'
        $process = [Diagnostics.Process]::Start($start)
        $process.PriorityClass = 'Normal'; $process.ProcessorAffinity = [IntPtr]$mask
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $before = Get-G6HostSnapshot $process
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not $process.HasExited) {
            Start-Sleep -Milliseconds 1000
            $after = Get-G6HostSnapshot $process
            if (-not $process.HasExited -and ($process.ProcessorAffinity.ToInt64() -ne $mask -or $process.PriorityClass -ne 'Normal')) { throw 'Benchmark placement/priority changed.' }
            Observe $before $after $process
            $before = $after
            if ($timer.Elapsed.TotalSeconds -gt 600) { throw 'Complete-workload process timed out.' }
        }
        $process.WaitForExit()
        $out = $stdout.GetAwaiter().GetResult(); $err = $stderr.GetAwaiter().GetResult()
        $out | Set-Content -LiteralPath (Join-Path $output ($sampleLabel + '.stdout.log')) -Encoding utf8
        $err | Set-Content -LiteralPath (Join-Path $output ($sampleLabel + '.stderr.log')) -Encoding utf8
        if ($process.ExitCode -ne 0) { throw "Runner exited with code $($process.ExitCode)." }
        $lines = @($out -split '\r?\n' | Where-Object { $_ -clike 'engine=lightweight-a500 *' })
        if ($lines.Count -ne 1) { throw 'Runner must emit exactly one complete-workload result.' }
        $sample = Read-CopperScreenBaselineSample $lines[0] $expectedIdentities[$name]
        $record.Samples += [pscustomobject]@{Label=$sampleLabel;Workload=$name;Arguments=$workloads[$name];Fps=$sample.Fps;MsPerField=$sample.MsPerField;Fingerprint=$sample.Fingerprint;ExitCode=$process.ExitCode;CompleteIdentityMatches=$true;TimingDisposition= if ($record.HostLoadViolations.Count) { 'INVALID / RERUN; diagnostic observation only' } else { 'DESCRIPTIVE; not release acceptance' }}
        $process.Dispose(); $process = $null
        $phase = 'cooldown'; $sampleLabel += '-cooldown'; Cooldown
    }
    Assert-CopperScreenQualificationFiles ($files + $runtimeFiles)
    $record.Summary = @($workloads.Keys | Sort-Object | ForEach-Object {
        $samples = @($record.Samples | Where-Object Workload -ceq $_)
        [pscustomobject]@{Workload=$_;Samples=$samples.Count;MeanFps=($samples.Fps | Measure-Object -Average).Average;MinFps=($samples.Fps | Measure-Object -Minimum).Minimum;MaxFps=($samples.Fps | Measure-Object -Maximum).Maximum;MeanMsPerField=($samples.MsPerField | Measure-Object -Average).Average}
    })
    $record.Status = if ($record.HostLoadViolations.Count) { 'INVALID / RERUN' } else { 'VALID_DESCRIPTIVE_BASELINE' }
    $record.FrozenFilesUnchanged = $true
}
catch {
    $record.Status = 'INVALID / RERUN'; $record.FailedPhase = $phase; $record.Error = $_.Exception.ToString()
    throw
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        if ($null -ne (Get-Variable stdout -ErrorAction SilentlyContinue)) {
            $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $output ($sampleLabel + '.stdout.log')) -Encoding utf8
            $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $output ($sampleLabel + '.stderr.log')) -Encoding utf8
        }
        $process.Dispose()
    }
    $record.CompletedUtc = [DateTime]::UtcNow.ToString('o')
    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'baseline-record.json') -Encoding utf8
}
