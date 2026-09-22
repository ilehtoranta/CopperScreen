param(
    [ValidateSet('lores','hires','lemmings')][string]$Workload = 'lores',
    [ValidateSet('Branches','Data','Instructions')][string]$CounterGroup = 'Branches',
    [string]$EvidenceDirectory,
    [switch]$ValidateOnly
)
# Diagnostic capture, not a throughput acceptance protocol. One bounded replay
# per invocation; inspect real counter payloads before collecting further groups.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/..").Path
$runtime = "$root/artifacts/wordsync-investigation/bench-candidate"
$profile = "$PSScriptRoot/lightweight-pmu.wprp"
$wpr = "$env:SystemRoot/System32/wpr.exe"
$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$pins = @{
    'CopperMod.Amiga.Lightweight.dll' = '8E390EE8307DA4B59FF5C6B9A3E5FCF3A274FDC72E6B51D3211ADC9106C316C5'
    'CopperMod.Amiga.Lightweight.Runner.dll' = '40B979416A38D99E0342C366D34D385D8EABF7C6C737EDFF332044EA41BBE2DF'
    'Copper68k.dll' = '0B9F1B4C4AA682AEE9E9D990FCDC6347F23309A96D47FD9A5AFCDCFA191565CC'
    'CopperDisk.dll' = '4F02010A01B314B6B385C81BF1AB608DDA0D799620105B20B986B438BB31DE8F'
    'CopperFloat.dll' = '4DCCA673E38589763D9488DD38C34034780944D4DA9BFDA943A34DC1A64AD235'
}
function Assert-Hash([string]$Path, [string]$Expected) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -cne $Expected) {
        throw "Pinned input differs: $Path"
    }
}
foreach ($name in $pins.Keys) { Assert-Hash "$runtime/$name" $pins[$name] }
$rom = 'C:/Data/ROM/Kickstart_13.rom'
$disk1 = 'C:/Data/TestImages/Lemmings (1991)(Psygnosis)(Disk 1 of 2)[cr SR].zip'
$inputScript = "$root/CopperScreen.Lightweight.Tests/Workloads/lemmings-native-level1.json"
if ($Workload -eq 'lemmings') {
    Assert-Hash $rom 'EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53'
    Assert-Hash $disk1 '5FC56B436722688BA37836E273A3A1C84D724235A91B39272768FF3E4CAC0A59'
    Assert-Hash "$root/media/lemmings-disk2.zip" '0C1CB9C49CBC29B56D2F974A90C510752E256148F8E8CC10CFBD026DB73DEA1A'
    Assert-Hash $inputScript '4C6B630D3F2C9AE3AD2ADC6E361C27277EEBF23EA02B9621C0753BCFCB5A7253'
}
$flags = switch ($Workload) {
    lores { @('--synthetic-paula-dma','--wide-output','--warmup','600','--frames','7200') }
    hires { @('--synthetic-paula-dma','--hires','--warmup','600','--frames','7200') }
    lemmings { @('--rom',$rom,'--adf',$disk1,'--input-script',$inputScript,'--warmup','10920','--frames','3600') }
}
$cpu = @(Get-CimInstance Win32_Processor)
if ($cpu.Count -ne 1 -or $cpu[0].Name -notmatch 'AMD Ryzen 5 5600X' -or
    $cpu[0].NumberOfCores -ne 6 -or $cpu[0].NumberOfLogicalProcessors -ne 12) {
    throw 'This diagnostic capture is pinned to the audited Ryzen 5 5600X host; revalidate on another CPU.'
}
if (-not ('CopperPmuTopology' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CopperPmuTopology {
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
     rows.Add(string.Format("{0},{1},{2},{3}",Marshal.ReadInt16(q,12),Marshal.ReadByte(q,14),Marshal.ReadByte(q,15),Marshal.ReadByte(q,18)));
    }
    offset+=length;
   }
   return rows.ToArray();
  } finally { Marshal.FreeHGlobal(p); }
 }
}
'@
}
$topology = @([CopperPmuTopology]::Read() | ForEach-Object {
    $v = $_.Split(','); [pscustomobject]@{Group=[int]$v[0]; Cpu=[int]$v[1]; Core=[int]$v[2]; Efficiency=[int]$v[3]}
})
$selected = @($topology | Where-Object Cpu -eq 2)
if ($topology.Count -ne 12 -or @($topology.Group | Sort-Object -Unique).Count -ne 1 -or
    $topology[0].Group -ne 0 -or @($topology.Efficiency | Sort-Object -Unique).Count -ne 1 -or
    $selected.Count -ne 1 -or
    (@($topology | Where-Object Core -eq $selected[0].Core | Sort-Object Cpu | ForEach-Object Cpu) -join ',') -ne '2,3') {
    throw 'Audited homogeneous topology / LP2 and sibling LP3 no longer match.'
}
$validation = & $wpr -profiles $profile 2>&1
if ($LASTEXITCODE -ne 0) { throw "WPR profile validation failed: $validation" }
$sources = & $wpr -pmcsources 2>&1
if ($LASTEXITCODE -ne 0) { throw 'PMU source enumeration failed.' }
$expected = (Get-Content "$root/docs/engine/DISK_LIVE_WORDSYNC_PERFORMANCE_2026-09-22.json" -Raw | ConvertFrom-Json).results | Where-Object workload -eq $Workload
if (-not $expected.fingerprint) { throw 'Missing accepted workload fingerprint.' }
if ($ValidateOnly) {
    "VALIDATED pinned binaries, selected media, workload identity, topology and WPR XML. Recording privilege and PMU payloads remain untested."
    return
}
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Windows denied PMU recording in the ordinary session. Run this collector from an administrator PowerShell; no policy or driver changes are required by this script.'
}
if (-not $EvidenceDirectory) {
    $EvidenceDirectory = "$root/artifacts/pmu-2026-09-22/$Workload-$CounterGroup-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
}
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$sources | Set-Content "$evidence/sources.txt"
$validation | Set-Content "$evidence/profile-validation.txt"
$instance = "CopperPmu-$([Guid]::NewGuid().ToString('N'))"
$started = $false
$runner = $null
$telemetry = @()
$record = [ordered]@{
    classification='DIAGNOSTIC ONLY; PMU payloads require analysis; not an FPS acceptance run'
    status='STARTING'; workload=$Workload; counterGroup=$CounterGroup; instance=$instance
    binaryHashes=$pins; topology=$topology; affinity=4; priority='Normal'; expectedFingerprint=$expected.fingerprint
    arguments=$flags; startedUtc=[DateTime]::UtcNow.ToString('o'); runnerPid=$null
    collectorSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash
    profileSha256=(Get-FileHash -LiteralPath $profile).Hash
    error=$null; etlSha256=$null
}
try {
    & $wpr -start "$profile!Copper$CounterGroup" -filemode -instancename $instance > "$evidence/start.log" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "WPR start failed ($LASTEXITCODE); see start.log. No other session will be stopped." }
    $started = $true
    $self = Get-Process -Id $PID
    $oldAffinity = $self.ProcessorAffinity; $oldPriority = $self.PriorityClass
    try {
        $self.ProcessorAffinity = [IntPtr]4; $self.PriorityClass = 'Normal'
        # All arguments are fixed paths/flags checked above; Windows quotes preserve spaces.
        $runnerArgs = @("$runtime/CopperMod.Amiga.Lightweight.Runner.dll") + $flags
        $quoted = @($runnerArgs | ForEach-Object { if ($_ -match '["\r\n]') { throw 'Unsupported argument character.' }; '"' + $_ + '"' })
        $runner = Start-Process -FilePath $dotnet -ArgumentList ($quoted -join ' ') -WorkingDirectory $root -NoNewWindow -PassThru -RedirectStandardOutput "$evidence/runner.log" -RedirectStandardError "$evidence/runner-error.log"
        $null = $runner.Handle
    } finally {
        $self.ProcessorAffinity = $oldAffinity; $self.PriorityClass = $oldPriority
    }
    $record.runnerPid = $runner.Id
    $record.runnerStartedUtc = $runner.StartTime.ToUniversalTime().ToString('o')
    $deadline = [DateTime]::UtcNow.AddSeconds(300)
    while (-not $runner.WaitForExit(1000)) {
        $runner.Refresh()
        if ($runner.HasExited) { break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Replay exceeded its 300-second limit.' }
        if ($runner.ProcessorAffinity.ToInt64() -ne 4 -or $runner.PriorityClass -ne 'Normal') { throw 'Runner placement changed.' }
        $load = @(Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor | Select-Object Name,PercentProcessorTime)
        $telemetry += [pscustomobject]@{utc=[DateTime]::UtcNow.ToString('o'); runnerCpuSeconds=$runner.TotalProcessorTime.TotalSeconds; processorLoad=$load}
    }
    $runner.WaitForExit()
    $record.runnerEndedUtc = $runner.ExitTime.ToUniversalTime().ToString('o')
    $record.runnerExitCode = $runner.ExitCode
    if ($runner.ExitCode -ne 0) { throw "Runner failed: $($runner.ExitCode)" }
    # Read plain CLR strings: Windows PowerShell's Get-Content attaches provider
    # metadata that ConvertTo-Json can recursively expand into a huge object graph.
    $line = [IO.File]::ReadAllLines("$evidence/runner.log") | Where-Object { $_ -match '^engine=lightweight-a500' } | Select-Object -Last 1
    $record.result = $line
    if (-not $line -or ($line -replace ' fps=\S+','') -cne $expected.fingerprint) { throw 'Complete workload fingerprint differs from accepted evidence.' }
    $record.status = 'REPLAY_MATCHED; COUNTER ANALYSIS PENDING'
} catch {
    $record.status = 'FAILED'; $record.error = $_.Exception.Message
    throw
} finally {
    if ($runner -and -not $runner.HasExited) { $runner.Kill(); $runner.WaitForExit() }
    if ($started) {
        & $wpr -stop "$evidence/counters.etl" -instancename $instance > "$evidence/stop.log" 2>&1
        if ($LASTEXITCODE -ne 0) {
            $record.status = 'FAILED'; $record.error = "WPR stop failed ($LASTEXITCODE); see stop.log."
            # Only our successfully started, uniquely named session may be cancelled.
            & $wpr -cancel -instancename $instance > "$evidence/cancel.log" 2>&1
        } elseif (Test-Path -LiteralPath "$evidence/counters.etl") {
            $record.etlSha256 = (Get-FileHash -LiteralPath "$evidence/counters.etl").Hash
        } else {
            $record.status = 'FAILED'; $record.error = 'WPR returned success without the expected ETL.'
        }
    }
    $telemetry | ConvertTo-Json -Depth 6 | Set-Content "$evidence/host-load.json"
    $record | ConvertTo-Json -Depth 8 | Set-Content "$evidence/capture.json"
}
if ($record.status -eq 'FAILED') { throw $record.error }
"Captured $evidence; complete replay matches, including zero measured allocation. Counter contents and steady-state attribution still require validation."
