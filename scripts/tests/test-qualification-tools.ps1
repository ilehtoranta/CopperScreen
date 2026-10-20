$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot '../qualification/CopperScreen.Qualification.psm1') -Force
. (Join-Path $PSScriptRoot '../agnus-g6-host-load.ps1')
$contract = Get-Content (Join-Path $PSScriptRoot '../../docs/engine/PERFORMANCE_BASELINE_2026-10-12.json') -Raw | ConvertFrom-Json
$checks = 0
function Reject([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'An invalid qualification control was accepted.' }
    $script:checks++
}
foreach ($name in @('lores','hires','lemmings')) {
    $identity = @($contract.Samples | Where-Object Workload -ceq $name)[0].Fingerprint
    $line = $identity.Replace('cycle=', 'fps=123.45 cycle=')
    $sample = Read-CopperScreenBaselineSample $line $identity
    if ($sample.Fps -ne 123.45 -or $sample.Fingerprint -cne $identity) { throw 'Valid complete identity rejected.' }
    $checks++
    if ((Read-CopperScreenBaselineSample ($line.Replace('fps=123.45','fps=123,45')) $identity).Fps -ne 123.45) { throw 'Comma decimal FPS was not parsed consistently.' }; $checks++
    foreach ($change in @(@('allocated=0','allocated=1'),@('pcm=real','pcm=none'),@('pixels=284204','pixels=1'),@('cpu=0x','cpu=0x0'),@('fps=123.45','fps=0'),@('fps=123.45','fps=NaN'),@('fps=123.45','fps=123,45,67'))) {
        $invalid = $line.Replace($change[0],$change[1])
        Reject { Read-CopperScreenBaselineSample $invalid $identity }
    }
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('copperscreen-qualification-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $root
try {
    $path = Join-Path $root 'capsule.bin'
    [IO.File]::WriteAllBytes($path,[byte[]](1,2,3))
    $frozen = Get-CopperScreenQualificationFile $path
    Assert-CopperScreenQualificationFiles @($frozen); $checks++
    [IO.File]::WriteAllBytes($path,[byte[]](1,2,4))
    Reject { Assert-CopperScreenQualificationFiles @($frozen) }
    Remove-Item -LiteralPath $path
    Reject { Assert-CopperScreenQualificationFiles @($frozen) }
} finally { Remove-Item -LiteralPath $root } # Empty, uniquely owned fixture; no recursive deletion.
Reject { Get-G6HostLoad ([pscustomobject]@{Cores=@{}}) ([pscustomobject]@{Cores=@{}}) 0 @(0) }
Reject { Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=10;Selected=25;Sibling=0;Package=0;Workloads=@()}) 25 25 10 }
Reject { Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=10;Selected=0;Sibling=25;Package=0;Workloads=@()}) 25 25 10 }
Reject { Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=10;Selected=0;Sibling=0;Package=25;Workloads=@()}) 25 25 10 }
Reject { Update-G6HostLoadState @{} ([pscustomobject]@{Seconds=1;Selected=0;Sibling=0;Package=0;Workloads=@('testhost[123]')}) 25 25 10 }
$before = [pscustomobject]@{Cores=@{0=[pscustomobject]@{Time=100;Idle=10}};TimeSeconds=0;BenchmarkSeconds=0;Workloads=@()}
$gap = [pscustomobject]@{Cores=@{0=[pscustomobject]@{Time=200;Idle=20}};TimeSeconds=11;BenchmarkSeconds=0;Workloads=@()}
Reject { Get-G6HostLoad $before $gap 0 @(0) }
$stale = [pscustomobject]@{Cores=@{0=[pscustomobject]@{Time=100;Idle=10}};TimeSeconds=1;BenchmarkSeconds=0;Workloads=@()}
Reject { Get-G6HostLoad $before $stale 0 @(0) }
$state = @{}
Update-G6HostLoadState $state ([pscustomobject]@{Seconds=6;Selected=26;Sibling=0;Package=0;Workloads=@()}) 25 25 10
Update-G6HostLoadState $state ([pscustomobject]@{Seconds=1;Selected=0;Sibling=0;Package=0;Workloads=@()}) 25 25 10
Update-G6HostLoadState $state ([pscustomobject]@{Seconds=6;Selected=26;Sibling=0;Package=0;Workloads=@()}) 25 25 10
if ($state.Selected -ne 6) { throw 'Sustained threshold did not reset below the limit.' }; $checks++
"Qualification tooling controls: $checks passed. These are harness controls, not native or performance acceptance."
