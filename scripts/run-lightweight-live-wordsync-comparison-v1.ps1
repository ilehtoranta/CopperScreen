param(
    [Parameter(Mandatory)][int]$LogicalProcessor,
    [Parameter(Mandatory)][string]$ReferenceDirectory,
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$AdfPath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$ValidateOnly
)
# Live-WORDSYNC comparison v1: accepted c92b16b engine versus running-read
# WORDSYNC control correction. Same timing, host policy and six-pair design as
# blitter-modulo v1. All complete workload identities must match across builds.
# See docs/engine/DISK_LIVE_WORDSYNC.md.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/agnus-g6-host-load.ps1"
$reference = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$rom = (Resolve-Path -LiteralPath $RomPath).Path
$adf = (Resolve-Path -LiteralPath $AdfPath).Path
$inputScript = (Resolve-Path -LiteralPath $InputScriptPath).Path
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory; previous evidence is immutable.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
Start-Transcript -Path "$evidence/protocol.log" | Out-Null
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LiveWordsyncTopologyV1 {
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
    R = 'engine=lightweight-a500 workload=image cpuMode=conservative-batch warmup=10920 frames=3600 completed=14520 cycle=2063189662 cpu=0x249E90223C45891F hardware=0xF4951BBFA89CA99A output=0x7D31C3889FF73760 pixels=284204 audioSamples=1924 pcm=real allocated=0 adf=True unsupported=none'
    C = 'engine=lightweight-a500 workload=image cpuMode=conservative-batch warmup=10920 frames=3600 completed=14520 cycle=2063189662 cpu=0x249E90223C45891F hardware=0xF4951BBFA89CA99A output=0x7D31C3889FF73760 pixels=284204 audioSamples=1924 pcm=real allocated=0 adf=True unsupported=none'
}
function Assert-NativeFingerprint([string]$Role, [string]$Fingerprint) {
    if (!$nativeFingerprints.ContainsKey($Role) -or $Fingerprint -cne $nativeFingerprints[$Role]) {
        throw 'Native replay differs from the independently validated identity for this build.'
    }
}
try {
    $pinnedBuild = @{
        'CopperMod.Amiga.Lightweight.Runner.dll' = '40B979416A38D99E0342C366D34D385D8EABF7C6C737EDFF332044EA41BBE2DF'
        'Copper68k.dll' = '0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC'
        'CopperDisk.dll' = '4F02010A01B314B6B385C81BF1AB608DDA0D799620105B20B986B438BB31DE8F'
        'CopperFloat.dll' = '4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235'
    }
    if ((Get-FileHash "$reference/CopperMod.Amiga.Lightweight.dll").Hash -ne
        'DDD6C3634ECA4DA1D53D9E8B09A49ED36D7383D44122C15B2274578FC796A365') {
        throw 'This comparison requires the accepted c92b16b reference.'
    }
    if ((Get-FileHash "$candidate/CopperMod.Amiga.Lightweight.dll").Hash -ne
        '8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5') {
        throw 'This comparison requires the validated running-read WORDSYNC candidate.'
    }
    foreach ($directory in @($reference, $candidate)) {
        foreach ($name in $pinnedBuild.Keys) {
            if ((Get-FileHash "$directory/$name").Hash -ne $pinnedBuild[$name]) {
                throw "Unvalidated dependency or runner: $name"
            }
        }
    }
    foreach ($role in @('R', 'C')) {
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
    $topology = @([LiveWordsyncTopologyV1]::Read() | ForEach-Object {
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
    "PROTOCOL live-wordsync-comparison-v1; Normal; CPU=$LogicalProcessor; protected=$($protected -join ','); six balanced pairs/workload; all workload identities identical across builds"
    "Topology=$($topology | ConvertTo-Json -Compress)"
    "Environment OS=$([Environment]::OSVersion) CPU=$((Get-CimInstance Win32_Processor).Name)"
    dotnet --info
    powercfg /GETACTIVESCHEME
    git -C (Split-Path $PSScriptRoot -Parent) rev-parse HEAD
    foreach ($directory in @($reference,$candidate)) {
        foreach ($file in Get-ChildItem -LiteralPath $directory -File | Where-Object Extension -in '.dll','.json') {
            "BUILD $($file.FullName) SHA256=$((Get-FileHash -LiteralPath $file.FullName).Hash)"
        }
    }
    $paths=@($rom,$adf,$inputScript,$PSCommandPath,"$PSScriptRoot/agnus-g6-host-load.ps1")
    foreach ($entry in (Get-Content -LiteralPath $inputScript -Raw | ConvertFrom-Json) | Where-Object adfPath) {
        $paths += [IO.Path]::GetFullPath($entry.adfPath,(Split-Path $inputScript -Parent))
    }
    foreach ($path in $paths) { "INPUT $path SHA256=$((Get-FileHash -LiteralPath $path).Hash)" }
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
        @{Name='lores';Flags=@('--synthetic-paula-dma','--wide-output','--warmup','600','--frames','7200')},
        @{Name='hires';Flags=@('--synthetic-paula-dma','--hires','--warmup','600','--frames','7200')},
        @{Name='lemmings';Flags=@('--rom',$rom,'--adf',$adf,'--input-script',$inputScript,'--warmup','10920','--frames','3600')}
    )
    $order=@('R1','C1','C2','R2','R3','C3','C4','R4','R5','C5','C6','R6')
    foreach($workload in $workloads) {
        foreach($label in $order) {
            "SAMPLE $($workload.Name) $label $([DateTime]::UtcNow.ToString('o'))"
            $directory=if($label.StartsWith('R')){$reference}else{$candidate}
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
                if($workload.Name -eq 'lemmings') { Assert-NativeFingerprint $role $fingerprint }
                $prior=@($results | Where-Object {
                    $_.Workload -eq $workload.Name -and
                    ($workload.Name -ne 'lemmings' -or $_.Label.StartsWith($role))
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
    $summary=@()
    foreach($workload in $workloads) {
        $rows=@($results | Where-Object Workload -eq $workload.Name)
        $ratios=@(1..6 | ForEach-Object {
            $r=($rows | Where-Object Label -eq "R$_").Fps
            $c=($rows | Where-Object Label -eq "C$_").Fps
            [math]::Log($r/$c)
        })
        $mean=($ratios | Measure-Object -Average).Average
        $variance=($ratios | ForEach-Object { ($_-$mean)*($_-$mean) } | Measure-Object -Sum).Sum/5
        $upper=$mean+2.015048*[math]::Sqrt($variance/6) # one-sided 95%, Student t with df=5
        $regression=100*([math]::Exp($mean)-1)
        $upperPercent=100*([math]::Exp($upper)-1)
        $disposition=if($regression -gt 1){'ACCEPTANCE_REQUIRED'}elseif($upperPercent -gt 1){'INCONCLUSIVE'}else{'PASS'}
        $summary += [pscustomobject]@{Workload=$workload.Name;FrameTimeRegressionPercent=$regression;Upper95Percent=$upperPercent;Disposition=$disposition}
    }
    $summary | ConvertTo-Json | Set-Content -LiteralPath "$evidence/summary.json"
    $summary | Format-Table
    'VALID measured series; engine throughput only. Native is a documented changed-state comparison. No historical evidence replaced.'
} catch {
    "INVALID / RERUN: $($_.Exception.Message)" | Set-Content -LiteralPath "$evidence/INVALID.txt"
    throw
} finally { Stop-Transcript | Out-Null }
