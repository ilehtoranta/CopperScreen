param(
    [Parameter(Mandatory)][string]$RunnerDirectory,
    [Parameter(Mandatory)][string]$TraceToolPath,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$DiskPath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$OutputDirectory
)
# Sampled wall-stack diagnosis, never a throughput acceptance measurement.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1') -Force
. (Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1')
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path $output){throw 'Use a fresh evidence directory.'}
$null=New-Item -ItemType Directory -Path $output
$record=[ordered]@{Protocol='lemmings-bottleneck-profile-v1';Status='RUNNING';DiagnosticOnly=$true;AcceptanceMeasurement=$false;RequestedSamplingRateMs=1;Runs=@();HostLoadViolations=@();StartedUtc=[DateTime]::UtcNow.ToString('o')}
$collector=$null;$child=$null
try{
    $runner=Join-Path (Resolve-Path $RunnerDirectory).Path 'CopperMod.Amiga.Lightweight.Runner.dll'
    $trace=(Resolve-Path $TraceToolPath).Path
    $rom=(Resolve-Path -LiteralPath $RomPath).Path;$disk=(Resolve-Path -LiteralPath $DiskPath).Path;$script=(Resolve-Path $InputScriptPath).Path
    $contract=Join-Path $PSScriptRoot '../docs/engine/LEMMINGS_CORRECTNESS_2026-11-02.json'
    if((Get-FileHash $contract).Hash-cne'663D84DEFAFEA783AA7353EAF50D21F35E9DBD1CAB734AFEA1F850967E7FAEA9'){throw 'Identity contract changed.'}
    $qualified=Get-Content $contract -Raw|ConvertFrom-Json
    $expected='engine=lightweight-a500 workload=image executionMode=Interpreter cpuMode=conservative-batch warmup=14520 frames=3600 completed=18120 '+$qualified.NativeReplay.CurrentBenchmarkIdentity.Replace(' unsupported=none',' adf=True unsupported=none')
    $paths=@($rom,$disk,$script,$trace,$PSCommandPath,$contract,(Join-Path $PSScriptRoot 'agnus-g6-host-load.ps1'),(Join-Path $PSScriptRoot 'qualification/CopperScreen.Qualification.psm1'))
    foreach($entry in (Get-Content $script -Raw|ConvertFrom-Json)){if($entry.PSObject.Properties['adfPath']){$paths += [IO.Path]::GetFullPath($entry.adfPath,(Split-Path $script -Parent))}}
    $dotnet=(Get-Command dotnet).Source
    $paths+=@(Get-ChildItem (Split-Path $runner -Parent) -File|ForEach-Object FullName)
    $paths+=@(Get-ChildItem (Join-Path (Split-Path $dotnet -Parent) 'shared/Microsoft.NETCore.App/10.0.12') -File|ForEach-Object FullName)
    $files=@($paths|Select-Object -Unique|ForEach-Object {Get-CopperScreenQualificationFile $_})
    $record.Files=$files;$record.ExpectedIdentity=$expected;$record.SourceHead=(git -C (Join-Path $PSScriptRoot '..') rev-parse HEAD)
    $record.Cpu=(Get-CimInstance Win32_Processor).Name;$record.PowerPlan=(powercfg /getactivescheme|Out-String).Trim()
    $topology=@(Get-CopperScreenQualificationTopology)
    if(@($topology.Group|Select-Object -Unique).Count-ne1-or$topology[0].Group-ne0-or@($topology.Efficiency|Select-Object -Unique).Count-ne1-or$topology.Count-ne@(Get-G6ProcessorCounters).Count){throw 'Validated homogeneous topology required.'}
    $record.Topology=$topology;$record.ChildCpu=2;$record.CollectorCpu=0;$record.Priority='Normal'
    $protected=@($topology|Where-Object Core -eq @($topology|Where-Object Cpu -eq 2)[0].Core|ForEach-Object Cpu)
    if($protected-contains0){throw 'Collector must use a different physical core.'}
    $state=@{};$label='preflight'
    function Observe($before,$after){
        $load=Get-G6HostLoad $before $after 2 $protected
        $load|Add-Member -NotePropertyName Sample -NotePropertyValue $label
        $load|ConvertTo-Json -Depth 5 -Compress|Add-Content (Join-Path $output 'telemetry.jsonl')
        try{Update-G6HostLoadState $state $load 25 25 10}catch{$record.HostLoadViolations += [pscustomobject]@{Sample=$label;Message=$_.Exception.Message}}
    }
    function Cooldown {
        $before=Get-G6HostSnapshot;$start=$before.TimeSeconds
        do{Start-Sleep -Seconds 1;$after=Get-G6HostSnapshot;Observe $before $after;$before=$after}while($after.TimeSeconds-$start-lt10)
    }
    Cooldown
    foreach($index in 1..2){
        Assert-CopperScreenQualificationFiles $files
        $label="run-$index"
        $start=[Diagnostics.ProcessStartInfo]::new($trace)
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        foreach($argument in @('collect','--profile','dotnet-sampled-thread-time','--format','Speedscope','--output',(Join-Path $output "$label.nettrace"),'--show-child-io','--',$dotnet,'exec','--fx-version','10.0.12',$runner,'--rom',$rom,'--adf',$disk,'--input-script',$script,'--warmup','14520','--frames','3600')){$start.ArgumentList.Add($argument)}
        $start.Environment['DOTNET_EventPipeThreadSamplingRate']='1'
        $collector=[Diagnostics.Process]::Start($start);$collector.PriorityClass='Normal';$collector.ProcessorAffinity=[IntPtr]1
        $stdout=$collector.StandardOutput.ReadToEndAsync();$stderr=$collector.StandardError.ReadToEndAsync()
        $timer=[Diagnostics.Stopwatch]::StartNew()
        while($null-eq$child -and -not$collector.HasExited){
            $matches=@(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"|Where-Object {$_.ParentProcessId-eq$collector.Id -and $_.CommandLine.Contains($runner)})
            if($matches.Count-eq1){
                $child=Get-Process -Id $matches[0].ProcessId
                # Retain a native process handle across parent reaping so final
                # CPU accounting remains available after the child exits.
                $null=$child.Handle
                $child.PriorityClass='Normal';$child.ProcessorAffinity=[IntPtr]4
                break
            }
            if($timer.Elapsed.TotalSeconds-gt30){throw 'Profile child not found.'}
            Start-Sleep -Milliseconds 50
        }
        if($null-eq$child){
            $stdout.GetAwaiter().GetResult()|Set-Content (Join-Path $output "$label.failed-collector.log")
            $stderr.GetAwaiter().GetResult()|Set-Content (Join-Path $output "$label.failed-collector.stderr.log")
            throw 'Collector exited before creating child; see collector logs.'
        }
        $runtime=@($child.Modules|Where-Object ModuleName -ceq 'coreclr.dll')
        if($runtime.Count-ne1-or$runtime[0].FileName-notmatch'[\\/]10\.0\.12[\\/]'){throw 'Profile child did not load the pinned runtime.'}
        $pinDelay=$timer.Elapsed.TotalSeconds
        $before=Get-G6HostSnapshot $child
        while(-not$child.HasExited){
            Start-Sleep -Seconds 1;$after=Get-G6HostSnapshot $child
            if(-not$child.HasExited-and($child.ProcessorAffinity.ToInt64()-ne4-or$child.PriorityClass-ne'Normal')){throw 'Child placement changed.'}
            if(-not$collector.HasExited-and($collector.ProcessorAffinity.ToInt64()-ne1-or$collector.PriorityClass-ne'Normal')){throw 'Collector placement changed.'}
            Observe $before $after;$before=$after
            if($timer.Elapsed.TotalSeconds-gt600){throw 'Profile timed out.'}
        }
        $child.WaitForExit();$collector.WaitForExit()
        $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
        $out|Set-Content (Join-Path $output "$label.log");$err|Set-Content (Join-Path $output "$label.stderr.log")
        # Get-Process may lose the child's exit status after its parent reaps
        # it. dotnet-trace documents forwarding the launched child's exit code.
        if($collector.ExitCode-ne0){throw 'Profile collector/child failed.'}
        $lines=@($out-split'\r?\n'|Where-Object {$_-clike'engine=lightweight-a500 *'})
        if($lines.Count-ne1){throw 'Expected one complete native result.'}
        $sample=Read-CopperScreenBaselineSample $lines[0] $expected
        $record.Runs += [pscustomobject]@{Run=$label;Fingerprint=$sample.Fingerprint;ChildPinDelaySeconds=$pinDelay;ForwardedChildExitCode=$collector.ExitCode;CollectorExitCode=$collector.ExitCode;RawFpsExcluded=$true}
        $record|ConvertTo-Json -Depth 10|Set-Content (Join-Path $output 'profile-record.json')
        Write-Host "$label complete; full identity matches"
        $child.Dispose();$child=$null;$collector.Dispose();$collector=$null
        $label+="-cooldown";Cooldown
    }
    Assert-CopperScreenQualificationFiles $files
    $record.Status='COMPLETE_DIAGNOSTIC_PROFILES';$record.FrozenFilesUnchanged=$true
}catch{$record.Status='FAILED_DIAGNOSTIC_PROFILE';$record.Error=$_.Exception.ToString();throw}
finally{
    foreach($process in @($child,$collector)){if($null-ne$process){if(-not$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}}
    $record.CompletedUtc=[DateTime]::UtcNow.ToString('o');$record|ConvertTo-Json -Depth 10|Set-Content (Join-Path $output 'profile-record.json')
}
