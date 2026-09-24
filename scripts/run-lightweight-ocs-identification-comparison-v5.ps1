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
# OCS absent-DENISEID comparison v5: pre-change engine versus cached bus word, fixed-size DMA word access and inline OCS palette.
# Frozen reference and SDK 10.0.401 palette candidate both use explicitly pinned .NET 10.0.12.
# Shared runner/CPU/disk; unchanged six-pair timing, placement and host-load policy v2.
# See docs/engine/TOWER_ASSAULT_INVESTIGATION.md.
$ErrorActionPreference = 'Stop'
$requiredRuntime = '10.0.12'
$dotnetPath = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$dotnetRoot = Split-Path $dotnetPath -Parent
$pinnedRuntime = @{
    'dotnet.exe' = '21A46F1E5235CF4E844B9DE5429F0E198B9C97A41F0503A66442F1D639CA3EE6'
    'host/fxr/10.0.12/hostfxr.dll' = '838591885C5396FB0588A3AEDC2F3E841D3AFC67570868B1BCE615264005A44C'
    'shared/Microsoft.NETCore.App/10.0.12/coreclr.dll' = '128AEE8C62A673D64739E585E3876B61133571CEC83A46240A62581D5465639B'
    'shared/Microsoft.NETCore.App/10.0.12/clrjit.dll' = '0749D8FC8E944D0576E1BB47120F87ADD8F875E5CD8968995633063FC79256F6'
    'shared/Microsoft.NETCore.App/10.0.12/System.Private.CoreLib.dll' = '1125ACC8106C43FC8BAD2D203C4C4485DF6182D292846C2FFF415C1040C54678'
}
function Assert-FrozenRuntime {
    foreach ($name in $pinnedRuntime.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $dotnetRoot $name)).Hash -cne $pinnedRuntime[$name]) {
            throw "Pinned .NET 10.0.12 runtime changed: $name"
        }
    }
}
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
public static class OcsIdentificationTopologyV5 {
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
    Assert-FrozenRuntime
    foreach ($name in $pinnedRuntime.Keys) { "RUNTIME $name SHA256=$($pinnedRuntime[$name])" }
    $pinnedBuild = @{
        'CopperMod.Amiga.Lightweight.Runner.dll' = '9AB9627DA0BC4207F1AB0D37621241C9012C77C22DDBCF4B3106C0B59E3240C0'
        'CopperDisk.dll' = '5D2625E5EB504F8FF60921CFA3616ACCC9D0CDE0CFC77A554A24EAD2B718956A'
        'CopperFloat.dll' = '4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235'
    }
    if ((Get-FileHash "$reference/CopperMod.Amiga.Lightweight.dll").Hash -ne
        '1B7F7AF0742F8B94DEA8F8AED1C1C76BA459B022CF6FAB412911657CA8D09683') {
        throw 'This comparison requires the pre-change September 23 corpus reference (engine source unchanged at cb946eb).'
    }
    if ((Get-FileHash "$candidate/CopperMod.Amiga.Lightweight.dll").Hash -ne
        '2F9E2C78E0B53ADBE1349B857D047A28892EC934F277B2BB27F7E0A34DD607EA') {
        throw 'This comparison requires the frozen inline-palette absent-DENISEID candidate.'
    }
    if ((Get-FileHash "$reference/Copper68k.dll").Hash -ne
        '982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F') {
        throw 'Reference CPU is not the shipped locality.1 binary.'
    }
    if ((Get-FileHash "$candidate/Copper68k.dll").Hash -ne
        '982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F') {
        throw 'Candidate CPU differs from the shared shipped locality.1 binary.'
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
    $topology = @([OcsIdentificationTopologyV5]::Read() | ForEach-Object {
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
    "PROTOCOL ocs-identification-comparison-v5; Normal; CPU=$LogicalProcessor; protected=$($protected -join ','); six balanced pairs/workload; all workload identities identical across builds"
    "Topology=$($topology | ConvertTo-Json -Compress)"
    "Environment OS=$([Environment]::OSVersion) CPU=$((Get-CimInstance Win32_Processor).Name)"
    & $dotnetPath --info
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
            $si=[Diagnostics.ProcessStartInfo]::new($dotnetPath)
            $si.UseShellExecute=$false; $si.CreateNoWindow=$true
            $si.RedirectStandardOutput=$true; $si.RedirectStandardError=$true
            foreach($arg in (@('--fx-version',$requiredRuntime,"$directory/CopperMod.Amiga.Lightweight.Runner.dll")+$workload.Flags)) { $si.ArgumentList.Add($arg) }
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
    Assert-FrozenRuntime
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
    'VALID measured series; engine throughput only. All complete workload identities match. No historical evidence replaced.'
} catch {
    "INVALID / RERUN: $($_.Exception.Message)" | Set-Content -LiteralPath "$evidence/INVALID.txt"
    throw
} finally { Stop-Transcript | Out-Null }
