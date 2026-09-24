param(
    [Parameter(Mandatory)][int]$LogicalProcessor,
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$ArchivePath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$ValidateOnly
)
# Tower Assault throughput v1: six candidate-only native gameplay samples.
# Not a baseline comparison or a regression acceptance gate.
# Reuses verified homogeneous topology, Normal priority and host-load policy v2.
# See docs/engine/ALIEN_BREED_II_INVESTIGATION.md.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/agnus-g6-host-load.ps1"
$candidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$rom = (Resolve-Path -LiteralPath $RomPath).Path
$archive = (Resolve-Path -LiteralPath $ArchivePath).Path
$inputScript = (Resolve-Path -LiteralPath $InputScriptPath).Path
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory; previous evidence is immutable.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
Start-Transcript -Path "$evidence/protocol.log" | Out-Null
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TowerThroughputTopologyV1 {
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetSystemCpuSetInformation(IntPtr p,uint n,out uint size,IntPtr process,uint flags);
 public static string[] Read() {
  uint size; GetSystemCpuSetInformation(IntPtr.Zero,0,out size,IntPtr.Zero,0);
  if(size==0) throw new Exception("Missing topology");
  var p=Marshal.AllocHGlobal(checked((int)size));
  try {
   if(!GetSystemCpuSetInformation(p,size,out size,IntPtr.Zero,0)) throw new Exception("CPU topology unavailable");
   var rows=new System.Collections.Generic.List<string>();
   for(int offset=0;offset<size;) {
    var q=IntPtr.Add(p,offset); var length=Marshal.ReadInt32(q);
    if(length<8 || offset+length>size) throw new Exception("Invalid topology entry");
    if(Marshal.ReadInt32(q,4)==0) {
     if(length<20) throw new Exception("Truncated CPU set");
     rows.Add($"{Marshal.ReadInt16(q,12)},{Marshal.ReadByte(q,14)},{Marshal.ReadByte(q,15)},{Marshal.ReadByte(q,18)}");
    }
    offset+=length;
   }
   return rows.ToArray();
  } finally { Marshal.FreeHGlobal(p); }
 }
}
'@
$results = @()
$nativeFingerprints = @{
    C = 'engine=lightweight-a500 workload=image-wide cpuMode=conservative-batch warmup=20000 frames=3600 completed=23600 cycle=3353195568 cpu=0xF7CA38E52A444E2C hardware=0x21F1929E512D20D0 output=0xF7C82D0B7A308BE0 pixels=284204 audioSamples=1924 pcm=real allocated=0 adf=True unsupported=none'
}
function Assert-NativeFingerprint([string]$Role, [string]$Fingerprint) {
    if (!$nativeFingerprints.ContainsKey($Role) -or $Fingerprint -cne $nativeFingerprints[$Role]) {
        throw 'Native replay differs from the independently validated identity for this build.'
    }
}
try {
    $pinnedBuild = @{
        'CopperMod.Amiga.Lightweight.Runner.dll' = '9AB9627DA0BC4207F1AB0D37621241C9012C77C22DDBCF4B3106C0B59E3240C0'
        'CopperDisk.dll' = '5D2625E5EB504F8FF60921CFA3616ACCC9D0CDE0CFC77A554A24EAD2B718956A'
        'CopperFloat.dll' = '4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235'
    }
    if ((Get-FileHash "$candidate/CopperMod.Amiga.Lightweight.dll").Hash -ne
        '2076345FF2476EE86A1F77CB9B553641D4ECFC1363705051BD75F7F9A426EFA3') {
        throw 'This comparison requires the frozen absent-DENISEID candidate.'
    }
    if ((Get-FileHash "$candidate/Copper68k.dll").Hash -ne
        '982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F') {
        throw 'Candidate CPU differs from the shared shipped locality.1 binary.'
    }
    foreach ($directory in @($candidate)) {
        foreach ($name in $pinnedBuild.Keys) {
            if ((Get-FileHash "$directory/$name").Hash -ne $pinnedBuild[$name]) {
                throw "Unvalidated dependency or runner: $name"
            }
        }
    }
    foreach ($role in @('C')) {
        $good = $nativeFingerprints[$role]
        Assert-NativeFingerprint $role $good
        foreach ($bad in @(
            $good.Replace('allocated=0', 'allocated=1'),
            $good.Replace('frames=3600', 'frames=3599'),
            $good.Replace('cpu=0x', 'cpu=0x0'),
            $good.Replace('hardware=0x', 'hardware=0x0'),
            $good.Replace('output=0x', 'output=0x0'),
            $good.Replace('pixels=284204', 'pixels=1'),
            $good.Replace('pcm=real', 'pcm=none'))) {
            $rejected = $false
            try { Assert-NativeFingerprint $role $bad } catch { $rejected = $true }
            if (!$rejected) { throw 'Native identity negative probe was accepted.' }
        }
    }
    'VALIDATION native identity positive/negative probes passed; no identity learned from timing samples.'
    $topology = @([TowerThroughputTopologyV1]::Read() | ForEach-Object {
        $v=$_.Split(','); [pscustomobject]@{Group=[int]$v[0];Cpu=[int]$v[1];Core=[int]$v[2];Efficiency=[int]$v[3]}
    })
    $counters = @(Get-G6ProcessorCounters)
    if (@($topology.Group | Sort-Object -Unique).Count -ne 1 -or $topology[0].Group -ne 0 -or
        @($topology.Efficiency | Sort-Object -Unique).Count -ne 1 -or
        @($topology.Cpu | Sort-Object -Unique).Count -ne $counters.Count -or
        $topology.Count -ne $counters.Count) { throw 'Verified single-group homogeneous topology required.' }
    $selected = @($topology | Where-Object Cpu -eq $LogicalProcessor)
    if ($selected.Count -ne 1 -or $LogicalProcessor -ge 63 -or $LogicalProcessor -lt 0) { throw 'Invalid selected processor.' }
    $protected = @($topology | Where-Object Core -eq $selected[0].Core | ForEach-Object Cpu)
    $mask = [int64]1 -shl $LogicalProcessor
    "PROTOCOL tower-throughput-v1; Normal; CPU=$LogicalProcessor; protected=$($protected -join ','); six candidate-only samples; throughput only, no regression claim"
    "Topology=$($topology | ConvertTo-Json -Compress)"
    "Environment OS=$([Environment]::OSVersion) CPU=$((Get-CimInstance Win32_Processor).Name)"
    dotnet --info
    powercfg /GETACTIVESCHEME
    git -C (Split-Path $PSScriptRoot -Parent) rev-parse HEAD
    foreach ($directory in @($candidate)) {
        foreach ($file in Get-ChildItem -LiteralPath $directory -File | Where-Object Extension -in '.dll','.json') {
            "BUILD $($file.FullName) SHA256=$((Get-FileHash -LiteralPath $file.FullName).Hash)"
        }
    }
    $paths=@($rom,$archive,$inputScript,$PSCommandPath,"$PSScriptRoot/agnus-g6-host-load.ps1")
    foreach ($entry in (Get-Content -LiteralPath $inputScript -Raw | ConvertFrom-Json) | Where-Object { $_.adfPath -or $_.diskPath }) {
        $paths += [IO.Path]::GetFullPath($(if ($entry.diskPath) {$entry.diskPath} else {$entry.adfPath}),(Split-Path $inputScript -Parent))
    }
    foreach ($path in $paths) { "INPUT $path SHA256=$((Get-FileHash -LiteralPath $path).Hash)" }
    $pinnedInputs = @{
        $rom = 'EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53'
        $archive = '52DC0DBA381EAA53DC671E83993A5FDD978DF4558891003856B66C58C71879F4'
        $inputScript = 'C321865216C6D9BBF9DF027658277AD2952735D81E728D7B25259CE9094DAA9E'
        ([IO.Path]::GetFullPath('../../media/tower-assault-disk2.ipf', (Split-Path $inputScript -Parent))) = '256B2FBDF5242342CFB0959F5AD3B8FB837ADB418D23316B2AE7B085C5158827'
    }
    foreach ($path in $pinnedInputs.Keys) {
        if ((Get-FileHash -LiteralPath $path).Hash -ne $pinnedInputs[$path]) { throw "Unvalidated Tower input: $path" }
    }
    # Exercise invalid-telemetry and sustained-noise rejection before measuring.
    $rejected=$false
    try { Get-G6HostLoad ([pscustomobject]@{Cores=@{}}) ([pscustomobject]@{Cores=@{}}) $LogicalProcessor $protected }
    catch { $rejected=$true }
    if(!$rejected) { throw 'Missing telemetry was accepted.' }
    $rejected=$false
    try { Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=10;Selected=26;Sibling=0;Package=0;Workloads=@()}) 25 25 10 }
    catch { $rejected=$true }
    if(!$rejected) { throw 'Sustained interference was accepted.' }
    $state=@{}
    function Observe($before,$after,$benchmarkId=0) {
        $load=Get-G6HostLoad $before $after $LogicalProcessor $protected
        $otherRunners=@(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object {
            $_.ProcessId -ne $benchmarkId -and $_.CommandLine -match 'CopperMod\.Amiga\.Lightweight\.Runner\.dll'
        })
        if($otherRunners.Count) { throw 'Competing Lightweight runner.' }
        $load | ConvertTo-Json -Compress | Add-Content -LiteralPath "$evidence/telemetry.jsonl"
        Update-G6HostLoadState $state $load 25 25 10
    }
    function Cooldown {
        $before=Get-G6HostSnapshot
        $start=$before.TimeSeconds
        do {
            Start-Sleep -Seconds 1
            $after=Get-G6HostSnapshot
            Observe $before $after
            $before=$after
        } while ($after.TimeSeconds-$start -lt 10)
    }
    Cooldown
    if($ValidateOnly) { 'VALIDATION topology and live telemetry valid; rejection probes passed; no acceptance measurement'; return }
    $workloads=@(
        @{Name='tower';Flags=@('--rom',$rom,'--disk',"$archive#/AlienBreedTowerAssault_Disk1.ipf",'--input-script',$inputScript,'--wide-output','--warmup','20000','--frames','3600')}
    )
    $order=@('C1','C2','C3','C4','C5','C6')
    foreach($workload in $workloads) {
        foreach($label in $order) {
            "SAMPLE $($workload.Name) $label $([DateTime]::UtcNow.ToString('o'))"
            $directory=$candidate
            $si=[Diagnostics.ProcessStartInfo]::new('dotnet')
            $si.UseShellExecute=$false; $si.CreateNoWindow=$true
            $si.RedirectStandardOutput=$true; $si.RedirectStandardError=$true
            foreach($arg in (@("$directory/CopperMod.Amiga.Lightweight.Runner.dll")+$workload.Flags)) { $si.ArgumentList.Add($arg) }
            $p=[Diagnostics.Process]::Start($si)
            try {
                $p.ProcessorAffinity=[IntPtr]$mask; $p.PriorityClass='Normal'
                if($p.ProcessorAffinity.ToInt64() -ne $mask -or $p.PriorityClass -ne 'Normal') { throw 'Unconfirmed placement' }
                $stdout=$p.StandardOutput.ReadToEndAsync(); $stderr=$p.StandardError.ReadToEndAsync()
                $before=Get-G6HostSnapshot $p
                do {
                    $done=$p.WaitForExit(1000); $p.Refresh()
                    if(!$done -and ($p.ProcessorAffinity.ToInt64() -ne $mask -or $p.PriorityClass -ne 'Normal')) { throw 'Placement changed' }
                    $after=Get-G6HostSnapshot $p
                    Observe $before $after $p.Id
                    $before=$after
                } while (!$done)
                $output=$stdout.Result.Trim()
                $output | Set-Content -LiteralPath "$evidence/$($workload.Name)-$label.log"
                if($p.ExitCode -ne 0) { throw "Workload failed: $($stderr.Result)" }
                $lines=@($output -split '\r?\n' | Where-Object { $_ -match '^engine=' })
                if($lines.Count -ne 1) { throw 'Missing/ambiguous measurement result.' }
                $line=$lines[0]
                if($line -notmatch ' pcm=real allocated=0 .* unsupported=none$') { throw 'Incomplete workload or measured allocation.' }
                $fps=[double]::Parse(([regex]::Match($line,'fps=([\d,.]+)').Groups[1].Value.Replace(',','.')),[Globalization.CultureInfo]::InvariantCulture)
                if($fps -le 0) { throw 'Invalid throughput.' }
                $fingerprint=$line -replace 'fps=[\d,.]+ ',''
                $role=$label.Substring(0,1)
                if($workload.Name -eq 'tower') { Assert-NativeFingerprint $role $fingerprint }
                $prior=@($results | Where-Object {
                    $_.Workload -eq $workload.Name -and
                    ($workload.Name -ne 'tower' -or $_.Label.StartsWith($role))
                })
                if($prior.Count -and $prior[0].Fingerprint -ne $fingerprint) { throw 'Reference/candidate fingerprints differ.' }
                $results += [pscustomobject]@{Workload=$workload.Name;Label=$label;Fps=$fps;MsPerFrame=1000/$fps;Fingerprint=$fingerprint}
                $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$evidence/samples.json"
                "RESULT $($workload.Name) $label fps=$fps msPerFrame=$(1000/$fps)"
            } finally {
                if(!$p.HasExited) { $p.Kill(); $p.WaitForExit() }
                $p.Dispose()
            }
            Cooldown
        }
    }
    $summary=[pscustomobject]@{
        Workload='tower';Samples=$results.Count
        MeanFps=($results.Fps | Measure-Object -Average).Average
        MinFps=($results.Fps | Measure-Object -Minimum).Minimum
        MaxFps=($results.Fps | Measure-Object -Maximum).Maximum
        Disposition='VALID_THROUGHPUT_ONLY_NO_BASELINE_COMPARISON'
    }
    $summary | ConvertTo-Json | Set-Content -LiteralPath "$evidence/summary.json"
    $summary | Format-List
    'VALID candidate-only throughput series; not regression acceptance. No historical evidence replaced.'
} catch {
    "INVALID / RERUN: $($_.Exception.Message)" | Set-Content -LiteralPath "$evidence/INVALID.txt"
    throw
} finally { Stop-Transcript | Out-Null }
