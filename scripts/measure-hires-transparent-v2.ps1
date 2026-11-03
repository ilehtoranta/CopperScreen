[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReferenceDirectory,
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$DiskPath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$QualificationPath,
    [Parameter(Mandatory)][int]$LogicalProcessor,
    [switch]$ValidateOnly,
    [switch]$LoadedHostDiagnostics
)

# hires-transparent-v2: six engine-only pairs per workload, common CPU.
# v2 records native CPU deltas to distinguish idle reusable VS build nodes.
# Historical protocols and identities remain unchanged.
# This does not replace historical three-workload retention or release gates.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.IdleBuildNodes.psm1') -Force
. (Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1')
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new immutable evidence directory.' }
$null = New-Item -ItemType Directory -Path $output
$record = [ordered]@{
    Protocol='hires-transparent-v2'; Status='RUNNING'; StartedUtc=[DateTime]::UtcNow.ToString('o')
    Scope='Empty hires sprite CCK fast path; six pairs per lores/hires/native workload; common prefetch development CPU'
    AcceptanceMeasurement=$false; PerformanceRetentionMeasurement=$true; Samples=@(); HostLoadPolicy='v2, unchanged limits and rejection rules'
    LoadedHostDiagnostics=[bool]$LoadedHostDiagnostics; HostLoadViolations=@()
}
$process = $null
$sampleLabel = 'preflight'
try {
    if (-not $IsWindows) { throw 'Windows native CPU-set topology and telemetry required.' }
    $repo = Split-Path $PSScriptRoot -Parent
    $oldContract = Join-Path $repo 'docs/engine/PERFORMANCE_BASELINE_2026-10-12.json'
    $currentContract = Join-Path $repo 'docs/engine/LEMMINGS_CORRECTNESS_2026-11-02.json'
    $qualificationContract = (Resolve-Path -LiteralPath $QualificationPath).Path
    if ((Get-FileHash $oldContract).Hash -cne 'BA7A5977E0F3F2E2B984F393220CECAA3393E326E458A452F9DBF7BA4803FF00' -or
        (Get-FileHash $currentContract).Hash -cne '663D84DEFAFEA783AA7353EAF50D21F35E9DBD1CAB734AFEA1F850967E7FAEA9') {
        throw 'Reviewed identity contract changed; version and validate a new protocol.'
    }
    $old = Get-Content $oldContract -Raw | ConvertFrom-Json
    $current = Get-Content $currentContract -Raw | ConvertFrom-Json
    $qualification = Get-Content $qualificationContract -Raw | ConvertFrom-Json
    if ($qualification.Protocol -cne 'hires-transparent-qualification-v2' -or $qualification.Status -cne 'CORRECTNESS_PASS' -or
        $qualification.EngineTestsPassed -ne 1287 -or $qualification.NativeCaptureFilesMatched -ne 320) { throw 'Sprite qualification incomplete.' }
    Assert-CopperScreenQualificationFiles $qualification.Files
    $expected = @{
        R = $qualification.NativeBenchmarkIdentity
        C = 'engine=lightweight-a500 workload=image executionMode=Interpreter cpuMode=conservative-batch warmup=14520 frames=3600 completed=18120 ' +
            $current.NativeReplay.CurrentBenchmarkIdentity.Replace(' unsupported=none',' adf=True unsupported=none')
    }
    if ($expected.R -cne $expected.C) { throw 'Qualified reference/candidate identities must match exactly.' }
    $workloads=@{
        lores=@{Expected=$qualification.ExpectedIdentities.lores;Warmup=600;Frames=7200;Args=@('--synthetic-paula-dma','--wide-output')}
        hires=@{Expected=$qualification.ExpectedIdentities.hires;Warmup=600;Frames=7200;Args=@('--synthetic-paula-dma','--wide-output','--hires')}
        lemmings=@{Expected=$expected.R;Warmup=14520;Frames=3600;Args=@()}
    }
    $record.WorkloadExpectedIdentities=$qualification.ExpectedIdentities
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
    foreach ($role in @('R','C')) {
        $expectedDirectory = if ($role -ceq 'R') { $qualification.ReferenceDirectory } else { $qualification.CandidateDirectory }
        if ($directories[$role] -cne $expectedDirectory) { throw "Capsule path differs from qualification: $role" }
        if ((Get-FileHash (Join-Path $directories[$role] 'Copper68k.dll')).Hash -cne '20B4E1002E8F7B2994EB139E04905BE1B5AC2970C360C4E2B1F682CA14029133') { throw 'Common qualified prefetch CPU differs.' }
    }
    $rFiles=@(Get-ChildItem $directories.R -File | Sort-Object Name)
    $cFiles=@(Get-ChildItem $directories.C -File | Sort-Object Name)
    if (($rFiles.Name -join '|') -cne ($cFiles.Name -join '|')) { throw 'Capsule file sets differ.' }
    foreach ($file in $rFiles | Where-Object Name -cne 'CopperMod.Amiga.Lightweight.dll') {
        if ((Get-FileHash $file.FullName).Hash -cne (Get-FileHash (Join-Path $directories.C $file.Name)).Hash) { throw "Only the engine DLL may differ: $($file.Name)" }
    }
    $dotnet = (Get-Command dotnet).Source
    $runtimeDirectory = Join-Path (Split-Path $dotnet -Parent) 'shared/Microsoft.NETCore.App/10.0.12'
    $paths = @($inputs) + @($qualification.Files.Path) + @($PSCommandPath,$oldContract,$currentContract,$qualificationContract,
        (Join-Path $PSScriptRoot 'qualification/CopperScreen.IdleBuildNodes.psm1'),
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
    if (@($record.RuntimeEnvironment | Where-Object Name -match '(JitDisasm|JitDump|JitStdOut|TieredCompilation|TieredPGO)').Count) { throw 'Remove diagnostic/JIT overrides before sprite timing.' }
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
    Assert-CopperScreenIdleBuildNodeControls
    $record.BuildNodeClassification='Native zero CPU delta, unchanged identity, explicit reusable node mode and VS parent required; missing/active nodes remain jobs. Historical records unchanged.'
    function Snapshot($benchmark=$null) {
        $snapshot=Get-G6HostSnapshot $benchmark
        $snapshot|Add-Member -NotePropertyName BuildNodes -NotePropertyValue @(Get-CopperScreenBuildNodeSnapshot)
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
        $load|Add-Member -NotePropertyName RawWorkloads -NotePropertyValue @($load.Workloads)
        $idle=@(Get-CopperScreenIdleBuildNodeIds $before.BuildNodes $after.BuildNodes)
        $load.Workloads=@($load.Workloads|Where-Object {
            -not ($_ -match '(?i)^MSBuild\.exe\[(\d+)\]$' -and $idle -contains [int]$Matches[1])
        })
        $load|Add-Member -NotePropertyName IdleBuildNodeIds -NotePropertyValue $idle
        $load|Add-Member -NotePropertyName BuildNodesBefore -NotePropertyValue $before.BuildNodes
        $load|Add-Member -NotePropertyName BuildNodesAfter -NotePropertyValue $after.BuildNodes
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
    $order=@(foreach($pair in 1..6){
        foreach($workload in @('lores','hires','lemmings')){
            $roles=if($pair%2-eq1){@('R','C')}else{@('C','R')}
            foreach($role in $roles){[pscustomobject]@{Label="$workload-$role$pair";Role=$role;Pair=$pair;Workload=$workload}}
        }
    })
    $record.Order=$order
    foreach ($job in $order) {
        $label=$job.Label; $sampleLabel=$label; $role=$job.Role; $workload=$workloads[$job.Workload]
        Assert-CopperScreenQualificationFiles $files
        $runner=Join-Path $directories[$role] 'CopperMod.Amiga.Lightweight.Runner.dll'
        $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
        $start.UseShellExecute=$false; $start.CreateNoWindow=$true
        $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
        $arguments=@('exec','--fx-version','10.0.12',$runner)+$workload.Args
        if($job.Workload-ceq'lemmings'){$arguments+=@('--rom',$rom,'--adf',$disk,'--input-script',$script)}
        $arguments+=@('--warmup',[string]$workload.Warmup,'--frames',[string]$workload.Frames)
        foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
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
        $sample=Read-CopperScreenBaselineSample $lines[0] $workload.Expected
        $record.Samples += [pscustomobject]@{Label=$label;Role=$role;Pair=$job.Pair;Workload=$job.Workload;Fps=$sample.Fps;MsPerField=$sample.MsPerField;Fingerprint=$sample.Fingerprint}
        $record | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'comparison-record.json')
        Write-Host "$label FPS=$($sample.Fps) ms/field=$($sample.MsPerField) identity=match"
        $process.Dispose(); $process=$null
        $sampleLabel="$label-cooldown"; Cooldown
    }
    Assert-CopperScreenQualificationFiles $files
    $record.Comparisons=@(foreach($name in @('lores','hires','lemmings')){
        $logs=@(foreach($pair in 1..6){
            $r=@($record.Samples|Where-Object Label -ceq "$name-R$pair")[0]
            $c=@($record.Samples|Where-Object Label -ceq "$name-C$pair")[0]
            [math]::Log($r.Fps/$c.Fps)
        })
        $mean=($logs|Measure-Object -Average).Average
        $squares=($logs|ForEach-Object{($_-$mean)*($_-$mean)}|Measure-Object -Sum).Sum
        $se=[math]::Sqrt($squares/5/6);$margin=2.0150483733330233*$se
        $upper=100*([math]::Exp($mean+$margin)-1)
        [pscustomobject]@{Workload=$name;PairedThroughputChangePercent=100*([math]::Exp(-$mean)-1);ThroughputTwoSided95LowerPercent=100*([math]::Exp(-$mean-2.570581835636305*$se)-1);ThroughputTwoSided95UpperPercent=100*([math]::Exp(-$mean+2.570581835636305*$se)-1);PairedFrameTimeChangePercent=100*([math]::Exp($mean)-1);OneSided95UpperPercent=$upper;OneSided95LowerPercent=100*([math]::Exp($mean-$margin)-1);Pairs=6;DegreesOfFreedom=5;Retention=if($upper-le1){'PASS'}else{'NOT_ESTABLISHED'}}
    })
    $record.Summary=@(foreach($name in @('lores','hires','lemmings')){foreach($role in @('R','C')){
        $samples=@($record.Samples|Where-Object{$_.Workload-ceq$name-and$_.Role-ceq$role})
        [pscustomobject]@{Workload=$name;Role=$role;Samples=$samples.Count;MeanFps=($samples.Fps|Measure-Object -Average).Average;MinFps=($samples.Fps|Measure-Object -Minimum).Minimum;MaxFps=($samples.Fps|Measure-Object -Maximum).Maximum;MeanMsPerField=($samples.MsPerField|Measure-Object -Average).Average}
    }})
    $record.Status=if($record.HostLoadViolations.Count){'INVALID / RERUN'}else{'VALID_THREE_WORKLOAD_COMPARISON'}
    $record.Retention=if($record.HostLoadViolations.Count){'INVALID / RERUN'}elseif(@($record.Comparisons|Where-Object Retention -cne 'PASS').Count){'NOT_ESTABLISHED'}else{'PASS'}
    $record.ComparisonInterpretation=if($record.HostLoadViolations.Count){'Loaded-host descriptive pairs only; no causal gain or retention decision.'}else{'Three-workload engine retention at the recorded common CPU version; not release or metadata-consumer qualification.'}
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
