[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReferenceDirectory,
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$GatedControlDirectory,
    [Parameter(Mandatory)][string]$ProbeDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][int]$LogicalProcessor,
    [switch]$LoadedHostDiagnostics
)
# Artificial poll isolation only. G is a known incorrect historical gate, R
# samples every boundary, C caches the interface and still samples every boundary.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1') -Force
. (Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1')
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path $output){throw 'Use a new evidence directory.'}
$null=New-Item -ItemType Directory -Path $output
$record=[ordered]@{Protocol='interrupt-interface-cost-v1';Status='RUNNING';AcceptanceMeasurement=$false;NativeThroughputMeasurement=$false;OptimizationMode='FullOpts; tiering disabled; no PGO; diagnostic only';LoadedHostDiagnostics=[bool]$LoadedHostDiagnostics;Samples=@();HostLoadViolations=@();StartedUtc=[DateTime]::UtcNow.ToString('o')}
$process=$null; $label='preflight'
try {
    if(-not $IsWindows){throw 'Native Windows topology and thread counters required.'}
    $directories=@{R=(Resolve-Path $ReferenceDirectory).Path;C=(Resolve-Path $CandidateDirectory).Path;G=(Resolve-Path $GatedControlDirectory).Path}
    $probe=Join-Path (Resolve-Path $ProbeDirectory).Path 'InterruptBookkeeping.dll'
    $dotnet=(Get-Command dotnet).Source
    $paths=@($PSCommandPath,(Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1'),(Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1'))
    foreach($directory in @($directories.Values)+@((Split-Path $probe -Parent))){$paths+=@(Get-ChildItem $directory -File|ForEach-Object FullName)}
    $paths+=@(Get-ChildItem (Join-Path (Split-Path $dotnet -Parent) 'shared/Microsoft.NETCore.App/10.0.12') -File|ForEach-Object FullName)
    $files=@($paths|Select-Object -Unique|ForEach-Object {Get-CopperScreenQualificationFile $_})
    foreach($directory in $directories.Values){if((Get-FileHash (Join-Path $directory 'Copper68k.dll')).Hash-cne'FF3A73B2DD2A62329BE1FCDAD1EAB8CA3A62D95F73BCFC03A4B87BD25955837A'){throw 'Expected the same qualified CPU binary in every role.'}}
    $record.Files=$files; $record.Directories=$directories
    $record.Runtime=(& $dotnet --info|Out-String);$record.Cpu=(Get-CimInstance Win32_Processor).Name;$record.PowerPlan=(powercfg /getactivescheme|Out-String).Trim()
    $topology=@(Get-CopperScreenQualificationTopology);$counters=@(Get-G6ProcessorCounters)
    if(@($topology.Group|Select-Object -Unique).Count-ne1 -or $topology[0].Group-ne0 -or @($topology.Efficiency|Select-Object -Unique).Count-ne1 -or $topology.Count-ne$counters.Count -or @($topology.Cpu|Select-Object -Unique).Count-ne$counters.Count){throw 'Homogeneous single-group topology required.'}
    $selected=@($topology|Where-Object Cpu -eq $LogicalProcessor)
    if($selected.Count-ne1 -or $LogicalProcessor-lt0 -or $LogicalProcessor-ge63){throw 'Invalid selected CPU.'}
    $protected=@($topology|Where-Object Core -eq $selected[0].Core|ForEach-Object Cpu)
    $mask=[int64]1-shl$LogicalProcessor
    $record.Topology=$topology;$record.LogicalProcessor=$LogicalProcessor;$record.ProtectedCpus=$protected;$record.Priority='Normal'
    $state=@{}
    function Snapshot($benchmark=$null){
        $snapshot=Get-G6HostSnapshot $benchmark
        $snapshot.Workloads += @(Get-CimInstance Win32_Process|Where-Object {
            ($null-eq$benchmark -or $_.ProcessId-ne$benchmark.Id) -and
            ($_.Name-match'^(cl|clang|clang-cl|UnrealHeaderTool)(\.exe)?$' -or
                ($_.Name-eq'dotnet.exe' -and $_.CommandLine-match'(CopperMod\.Amiga\.Lightweight\.Runner|InterruptBookkeeping)\.dll'))
        }|ForEach-Object {"$($_.Name)[$($_.ProcessId)]"})
        return $snapshot
    }
    function Observe($before,$after){
        $load=Get-G6HostLoad $before $after $LogicalProcessor $protected
        $load|Add-Member -NotePropertyName Sample -NotePropertyValue $label
        $load|ConvertTo-Json -Depth 5 -Compress|Add-Content (Join-Path $output 'telemetry.jsonl')
        try{Update-G6HostLoadState $state $load 25 25 10}catch{
            if(-not$LoadedHostDiagnostics){throw}
            $record.HostLoadViolations += [pscustomobject]@{Sample=$label;Message=$_.Exception.Message}
        }
    }
    function Cooldown {
        $before=Snapshot;$start=$before.TimeSeconds
        do{Start-Sleep -Seconds 1;$after=Snapshot;Observe $before $after;$before=$after}while($after.TimeSeconds-$start-lt10)
    }
    $rejected=$false
    try{$null=Get-G6HostLoad ([pscustomobject]@{Cores=@{}}) ([pscustomobject]@{Cores=@{}}) $LogicalProcessor $protected}catch{$rejected=$true}
    if(-not$rejected){throw 'Missing telemetry accepted.'}
    $rejected=$false
    try{Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=10;Selected=26;Sibling=0;Package=0;Workloads=@()}) 25 25 10}catch{$rejected=$true}
    if(-not$rejected){throw 'Sustained noise accepted.'}
    Cooldown
    # Six balanced permutations avoid always assigning one role the same position.
    $roundOrders=@('GRC','CRG','RCG','GCR','CGR','RGC')
    $round=0
    foreach($order in $roundOrders){
        $round++
        foreach($character in $order.ToCharArray()){
            $role=[string]$character;$label="$role$round"
            Assert-CopperScreenQualificationFiles $files
            $resultPath=Join-Path $output "$label.json"
            $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
            $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
            foreach($argument in @('exec','--fx-version','10.0.12',$probe,$directories[$role],$resultPath,'20000000')){$start.ArgumentList.Add($argument)}
            $start.Environment['DOTNET_ROLL_FORWARD']='Disable'
            $start.Environment['DOTNET_TieredCompilation']='0'
            $before=Snapshot
            $process=[Diagnostics.Process]::Start($start);$process.PriorityClass='Normal';$process.ProcessorAffinity=[IntPtr]$mask
            $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
            $timer=[Diagnostics.Stopwatch]::StartNew()
            do{
                Start-Sleep -Seconds 1;$after=Snapshot $process
                if(-not$process.HasExited -and ($process.ProcessorAffinity.ToInt64()-ne$mask -or $process.PriorityClass-ne'Normal')){throw 'Placement changed.'}
                Observe $before $after;$before=$after
                if($timer.Elapsed.TotalSeconds-gt120){throw 'Isolated probe timed out.'}
            }while(-not$process.HasExited)
            $process.WaitForExit();$stdout.GetAwaiter().GetResult()|Set-Content (Join-Path $output "$label.stdout.log");$stderr.GetAwaiter().GetResult()|Set-Content (Join-Path $output "$label.stderr.log")
            if($process.ExitCode-ne0){throw "Probe failed: $label"}
            $result=Get-Content $resultPath -Raw|ConvertFrom-Json
            if($result.cpuSha256-cne'FF3A73B2DD2A62329BE1FCDAD1EAB8CA3A62D95F73BCFC03A4B87BD25955837A' -or $result.engineSha256-cne(Get-FileHash (Join-Path $directories[$role] 'CopperMod.Amiga.Lightweight.dll')).Hash){throw 'Wrong probe assemblies.'}
            if(@($result.observations).Count-ne2){throw 'Expected inactive and masked scenarios.'}
            foreach($observation in $result.observations){
                if($observation.iterations-ne20000000 -or $observation.accepted-ne0 -or $observation.allocated-ne0 -or $observation.finalSample.Cycle-ne164000000){throw 'Probe contract differs.'}
                if($role-ne'G' -and $observation.pipeline.EvaluatedSampleCycle-ne164000000){throw 'Required IPL sampling was skipped.'}
                if($role-eq'G' -and $observation.pipeline.EvaluatedSampleCycle-ne[long]::MinValue){throw 'Gated control did not skip sampling.'}
                if($observation.threadCpuNanosecondsPerPoll-le0 -or $observation.threadCyclesPerPoll-le0){throw 'Missing thread CPU accounting.'}
            }
            $record.Samples += [pscustomobject]@{Role=$role;Round=$round;Result=$result}
            $record|ConvertTo-Json -Depth 12|Set-Content (Join-Path $output 'cost-record.json')
            Write-Host "$label complete: $(@($result.observations|ForEach-Object {[math]::Round($_.threadCyclesPerPoll,2)}) -join ', ') thread cycles/poll"
            $process.Dispose();$process=$null
        }
        $label="round$round-cooldown";Cooldown
    }
    foreach($round in 1..6){
        $r=@($record.Samples|Where-Object {$_.Role-ceq'R'-and$_.Round-eq$round})[0].Result
        $c=@($record.Samples|Where-Object {$_.Role-ceq'C'-and$_.Round-eq$round})[0].Result
        for($i=0;$i-lt2;$i++){if(($r.observations[$i].pipeline|ConvertTo-Json -Compress)-cne($c.observations[$i].pipeline|ConvertTo-Json -Compress)){throw 'Cached and reference IPL states differ.'}}
    }
    Assert-CopperScreenQualificationFiles $files
    $record.FrozenFilesUnchanged=$true;$record.ReferenceCandidatePipelineStatesMatch=$true
    $record.Status=if($record.HostLoadViolations.Count){'INVALID / RERUN'}else{'VALID_ISOLATED_POLL_DIAGNOSTIC'}
}catch{$record.Status='INVALID / RERUN';$record.FailedSample=$label;$record.Error=$_.Exception.ToString();throw}
finally{
    if($null-ne$process){if(-not$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
    $record.CompletedUtc=[DateTime]::UtcNow.ToString('o');$record|ConvertTo-Json -Depth 12|Set-Content (Join-Path $output 'cost-record.json')
}
