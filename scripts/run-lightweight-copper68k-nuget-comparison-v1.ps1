param(
    [Parameter(Mandatory)][int]$LogicalProcessor,
    [Parameter(Mandatory)][string]$ReferenceDirectory,
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$AdfPath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$ValidateOnly
)
# Copper68k NuGet package comparison v1: accepted engine/runner/workloads frozen; compare the previously accepted CPU assembly against the currently signed NuGet 1.4.1 assembly.
# Only Copper68k.dll changes; .NET 10.0.12, balanced six-pair order, placement, and host-load policy v2 remain frozen.
# Candidate package signature, archive, assembly, and all three accepted complete fingerprints are pinned.
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
$nugetPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory; previous evidence is immutable.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
Start-Transcript -Path "$evidence/protocol.log" | Out-Null
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class OcsIdentificationTopologyV8 {
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
$expectedWorkloadFingerprints = @{
    lores = 'engine=lightweight-a500 workload=paula-dma-wide cpuMode=conservative-batch warmup=600 frames=7200 completed=7800 cycle=1108395730 cpu=0x61B8E8E422AA0567 hardware=0x2F1CDAE2DDCFA737 output=0x516B826B90FE2CD7 pixels=284204 audioSamples=1924 pcm=real allocated=0 adf=False unsupported=none'
    hires = 'engine=lightweight-a500 workload=paula-dma-wide-hires cpuMode=conservative-batch warmup=600 frames=7200 completed=7800 cycle=1108395726 cpu=0x9453D8C5C72902D3 hardware=0x09554374C200BE7A output=0x342F83A04C3EBCF6 pixels=284204 audioSamples=1924 pcm=real allocated=0 adf=False unsupported=none'
    lemmings = 'engine=lightweight-a500 workload=image cpuMode=conservative-batch warmup=10920 frames=3600 completed=14520 cycle=2063189662 cpu=0x249E90223C45891F hardware=0xF4951BBFA89CA99A output=0x7D31C3889FF73760 pixels=284204 audioSamples=1924 pcm=real allocated=0 adf=True unsupported=none'
}
function Assert-WorkloadFingerprint([string]$Workload, [string]$Fingerprint) {
    if (!$expectedWorkloadFingerprints.ContainsKey($Workload) -or $Fingerprint -cne $expectedWorkloadFingerprints[$Workload]) {
        throw "Workload differs from the independently accepted complete identity: $Workload"
    }
}
try {
    Assert-FrozenRuntime
    foreach ($name in $pinnedRuntime.Keys) { "RUNTIME $name SHA256=$($pinnedRuntime[$name])" }
    $pinnedBuild = @{
        'CopperDisk.dll' = '5D2625E5EB504F8FF60921CFA3616ACCC9D0CDE0CFC77A554A24EAD2B718956A'
        'CopperDisk.pdb' = '8AACD661224DF58D0DCF13CFE540A1353036E32460F5BF7ED8EF199C9D63361C'
        'CopperDisk.xml' = 'B5E21B2026216BFE4EF67D71790EE83DAF38C9E524307F1AF23ABC4260098D69'
        'CopperFloat.dll' = '4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235'
        'CopperMod.Amiga.Lightweight.dll' = '683C0AB519FBCCDE85770977269BB965FF88C39D20411C7550ACF62597B7A5E8'
        'CopperMod.Amiga.Lightweight.pdb' = '0917CD1B15113CC3796BCD48090350F06A2534F237A40DBFE8447EE394AEABB5'
        'CopperMod.Amiga.Lightweight.Runner.deps.json' = '769E715D0015AF69BA39415D442F456F06FCEFF6DAAC21C30029BEF904EFF9B6'
        'CopperMod.Amiga.Lightweight.Runner.dll' = '9AB9627DA0BC4207F1AB0D37621241C9012C77C22DDBCF4B3106C0B59E3240C0'
        'CopperMod.Amiga.Lightweight.Runner.exe' = '0907BF3BFA8BD034F8843D37B294F2F90A7797EA61D1E6E1F969DFFFF45EACD2'
        'CopperMod.Amiga.Lightweight.Runner.pdb' = '1A78C1E8CB18BE266A4D621A8EBBD6DF63F76EA6BAAAA04C899C598E0D611B49'
        'CopperMod.Amiga.Lightweight.Runner.runtimeconfig.json' = 'C230A317A54DD960BCBEB5F347F52E18DC665A26F7EFDA2159FCED9A5AC7E097'
        'CopperMod.Amiga.Lightweight.xml' = 'A8C3FE2D0ECE3CBD91F8DB833E38413F15CBC871E67D5CE1E339E698E0891B3F'
    }
    $expectedEngine = '683C0AB519FBCCDE85770977269BB965FF88C39D20411C7550ACF62597B7A5E8'
    if ((Get-FileHash "$reference/CopperMod.Amiga.Lightweight.dll").Hash -cne $expectedEngine -or
        (Get-FileHash "$candidate/CopperMod.Amiga.Lightweight.dll").Hash -cne $expectedEngine) {
        throw 'Both sides must use the exact accepted Lightweight engine assembly.'
    }
    if ((Get-FileHash "$reference/Copper68k.dll").Hash -cne
        '982DFFFFBB18BA1D96ACE514E0B8044D3AC780565A018112CED3FB4FCD784F0F') {
        throw 'Reference Copper68k assembly differs from the accepted frozen binary.'
    }
    if ((Get-FileHash "$candidate/Copper68k.dll").Hash -cne
        'F82CA4A5FD953B0BEE31EE360212070548DB0DAA80874EEEC36DB0EFD1F1AFE1') {
        throw 'Candidate Copper68k assembly differs from the downloaded NuGet package.'
    }
    if ((Get-FileHash -Algorithm SHA512 $nugetPackage).Hash -cne
        '4307758F0E4E89C491535849800C16AD24F2E54DFBBF5B62A81BD616A6AB59D97DE9BB427241513049F5CF4138FF07892FDC911462BC9B4F17BDAAF1C2367606') {
        throw 'NuGet archive hash differs from the downloaded signed package.'
    }
    $signatureOutput = & $dotnetPath nuget verify --all $nugetPackage 2>&1
    if ($LASTEXITCODE -ne 0) { throw "NuGet package signature verification failed: $signatureOutput" }
    $signatureOutput | ForEach-Object { "SIGNATURE $_" }
    foreach ($directory in @($reference, $candidate)) {
        $files = @(Get-ChildItem -LiteralPath $directory -File)
        if ($files.Count -ne ($pinnedBuild.Count + 1)) { throw 'Comparison directory has missing or extra build inputs.' }
        foreach ($name in $pinnedBuild.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $directory $name)).Hash -cne $pinnedBuild[$name]) {
                throw "Unvalidated identical dependency or runner: $name"
            }
        }
    }
    foreach ($name in $expectedWorkloadFingerprints.Keys) {
        $good = $expectedWorkloadFingerprints[$name]
        Assert-WorkloadFingerprint $name $good
        foreach ($bad in @($good.Replace('allocated=0', 'allocated=1'), $good.Replace('pixels=284204', 'pixels=1'), $good.Replace('pcm=real', 'pcm=none'))) {
            $rejected = $false
            try { Assert-WorkloadFingerprint $name $bad } catch { $rejected = $true }
            if (!$rejected) { throw "Workload identity negative probe was accepted: $name" }
        }
    }
    'VALIDATION all three workload identities positive/negative probes passed before timing; no identity learned from timing samples.'
    $topology = @([OcsIdentificationTopologyV8]::Read() | ForEach-Object {
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
    "PROTOCOL copper68k-nuget-comparison-v1; Normal; CPU=$LogicalProcessor; protected=$($protected -join ','); six balanced pairs/workload; all three full workload identities pinned before timing"
    "Topology=$($topology | ConvertTo-Json -Compress)"
    "Environment OS=$([Environment]::OSVersion) CPU=$((Get-CimInstance Win32_Processor).Name)"
    & $dotnetPath --info
    powercfg /GETACTIVESCHEME
    git -C (Split-Path $PSScriptRoot -Parent) rev-parse HEAD
    foreach ($directory in @($reference,$candidate)) {
        foreach ($file in Get-ChildItem -LiteralPath $directory -File) {
            "BUILD $($file.FullName) SHA256=$((Get-FileHash -LiteralPath $file.FullName).Hash)"
        }
    }
    $paths=@($rom,$adf,$inputScript,$nugetPackage,$PSCommandPath,"$PSScriptRoot/agnus-g6-host-load.ps1")
    foreach ($entry in (Get-Content -LiteralPath $inputScript -Raw | ConvertFrom-Json) | Where-Object adfPath) {
        $paths += [IO.Path]::GetFullPath($entry.adfPath,(Split-Path $inputScript -Parent))
    }
    foreach ($path in $paths) { "INPUT $path SHA256=$((Get-FileHash -LiteralPath $path).Hash)" }
    "PACKAGE_NUPKG_SHA512=$((Get-FileHash -Algorithm SHA512 -LiteralPath $nugetPackage).Hash)"
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
                Assert-WorkloadFingerprint $workload.Name $fingerprint
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
    'VALID measured series; Copper68k NuGet package throughput only. All complete identities match the accepted engine baseline. No historical evidence replaced.'
} catch {
    "INVALID / RERUN: $($_.Exception.Message)" | Set-Content -LiteralPath "$evidence/INVALID.txt"
    throw
} finally { Stop-Transcript | Out-Null }
