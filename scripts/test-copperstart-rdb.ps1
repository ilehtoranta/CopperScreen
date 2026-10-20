[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$RdbFixturePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$AdditionalPackageSource
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$artifactsModule = Join-Path $PSScriptRoot 'CopperScreen.Artifacts.psm1'
Import-Module $artifactsModule -Force
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$contract = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperStart.json') -Raw | ConvertFrom-Json
$rom = (Resolve-Path -LiteralPath $RomPath).Path
$fixture = (Resolve-Path -LiteralPath $RdbFixturePath).Path
if ((Get-Item $rom).Length -ne $contract.bytes -or (Get-FileHash $rom).Hash -ine $contract.sha256) { throw 'Supply the pinned raw CopperStart ROM.' }
if ((Get-FileHash $fixture).Hash -ne '000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368') { throw 'Supply the qualified two-partition RDB readback fixture. It is never modified.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a new or empty output directory.' }
$null = New-Item -ItemType Directory -Path $output -Force
$build = Join-Path $output 'build'
$arguments = @('build', (Join-Path $repository 'CopperScreen.slnx'), '-c', 'Release', '-m:1', '--artifacts-path', $build)
if ($AdditionalPackageSource) { $arguments += "-p:RestoreAdditionalProjectSources=$AdditionalPackageSource" }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Production solution build failed.' }
$runner = Join-Path $build 'bin/CopperMod.Amiga.Lightweight.Runner/release/CopperMod.Amiga.Lightweight.Runner.dll'
$names = @('COPPERSCREEN_COPPERSTART_ROM','COPPERSCREEN_COPPERSTART_RDB','COPPERSCREEN_LIGHTWEIGHT_RUNNER')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:COPPERSCREEN_COPPERSTART_ROM = $rom
    $env:COPPERSCREEN_COPPERSTART_RDB = $fixture
    $env:COPPERSCREEN_LIGHTWEIGHT_RUNNER = $runner
    $filter = '(FullyQualifiedName~CopperScreenRdbSettingsTests|FullyQualifiedName~CopperScreenHardDiskSettingsTests|FullyQualifiedName~CopperStartTests.MissingDamagedNativeAndAmbiguousRomsHaveRecoveryGuidance)&FullyQualifiedName!~QualifiedHardDiskFreshBoot'
    & dotnet test (Join-Path $repository 'CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj') -c Release --no-build --no-restore --artifacts-path $build --filter $filter --logger 'trx;LogFileName=rdb-frontends.trx' --results-directory $output
    if ($LASTEXITCODE -ne 0) { throw 'Configuration/native RDB replay failed.' }
    & dotnet test (Join-Path $repository 'CopperScreen.Desktop.Tests/CopperScreen.Desktop.Tests.csproj') -c Release --no-build --no-restore --artifacts-path $build --filter 'FullyQualifiedName~HardDiskForm|FullyQualifiedName~RdbLayout' --logger 'trx;LogFileName=rdb-ui.trx' --results-directory $output
    if ($LASTEXITCODE -ne 0) { throw 'Storage UI workflow failed.' }
    foreach ($report in @(@('rdb-frontends.trx',25),@('rdb-ui.trx',2))) {
        [xml]$trx = Get-Content (Join-Path $output $report[0]) -Raw
        $cases = @($trx.TestRun.Results.UnitTestResult)
        if ($cases.Count -ne $report[1] -or @($cases | Where-Object outcome -ne Passed).Count) { throw "Expected all $($report[1]) cases without skips in $($report[0])." }
    }
    & (Join-Path $PSScriptRoot 'tests/test-rdb-hardfile-runner.ps1') -RunnerPath $runner
    @{
        protocol='copperstart-rdb-frontends-v1'; passed=$true; qualificationId=$contract.qualificationId
        romSha256=$contract.sha256; fixtureSha256=(Get-FileHash $fixture).Hash; runnerSha256=(Get-FileHash $runner).Hash
        settingsFirmwareCases=23; uiCases=2; runnerOptionCases=8; fullRomCases=2; fullRomBoots=4; compiledMc68000Cases=0
        persistedLayoutPathProtection=$true; bothPartitionsReadBack=$true; installedPackage='Separate coverage'
    } | ConvertTo-Json | Set-Content (Join-Path $output 'qualification.json') -Encoding utf8
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    $compression = Compress-CopperScreenCompletedArtifacts -RepositoryRoot $repository -Path $output
    if ($compression.Status -eq 'SKIPPED') { Write-Warning "Completed output was not compressed: $($compression.Reason)" }
}
