[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$RdbFixturePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$RunnerPath,
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
if ((Get-FileHash $fixture).Hash -ne '000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368') { throw 'Supply the qualified original rdb-both-written.hdf fixture. It is never modified.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a new or empty output directory.' }
$null = New-Item -ItemType Directory -Path $output -Force
if (!$RunnerPath) {
    $build = Join-Path $output 'build'
    $arguments = @('build', (Join-Path $repository 'CopperMod.Amiga.Lightweight.Runner/CopperMod.Amiga.Lightweight.Runner.csproj'), '-c', 'Release', '-m:1', '--artifacts-path', $build)
    if ($AdditionalPackageSource) { $arguments += "-p:RestoreAdditionalProjectSources=$AdditionalPackageSource" }
    & dotnet @arguments 2>&1 | Tee-Object (Join-Path $output 'runner-build.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Runner build failed.' }
    $RunnerPath = Join-Path $build 'bin/CopperMod.Amiga.Lightweight.Runner/release/CopperMod.Amiga.Lightweight.Runner.dll'
}
$runner = (Resolve-Path -LiteralPath $RunnerPath).Path
$names = @('COPPERSCREEN_COPPERSTART_ROM','COPPERSCREEN_COPPERSTART_RDB','COPPERSCREEN_LIGHTWEIGHT_RUNNER','COPPERSCREEN_RDB_WRITE_OUTPUT')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:COPPERSCREEN_COPPERSTART_ROM = $rom
    $env:COPPERSCREEN_COPPERSTART_RDB = $fixture
    $env:COPPERSCREEN_LIGHTWEIGHT_RUNNER = $runner
    $env:COPPERSCREEN_RDB_WRITE_OUTPUT = Join-Path $output 'native'
    $filter = 'FullyQualifiedName~CopperStartRdbWriteTests'
    $arguments = @('test', (Join-Path $repository 'CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj'), '-c', 'Release', '-m:1',
        '--artifacts-path', (Join-Path $output 'build'), '--filter', $filter,
        '--logger', 'trx;LogFileName=frontend-writes.trx', '--results-directory', $output)
    if ($AdditionalPackageSource) { $arguments += "-p:RestoreAdditionalProjectSources=$AdditionalPackageSource" }
    & dotnet @arguments 2>&1 | Tee-Object (Join-Path $output 'tests.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Native hard-disk write validation failed.' }
    [xml]$trx = Get-Content (Join-Path $output 'frontend-writes.trx') -Raw
    $expected = 6
    if (@($trx.TestRun.Results.UnitTestResult).Count -ne $expected -or @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -ne Passed).Count) { throw "All $expected intended full-ROM cases must pass without skips." }
    @{
        protocol='copperstart-frontend-rdb-writes-v1'; passed=$true; qualificationId=$contract.qualificationId
        romSha256=$contract.sha256; fixtureSha256=(Get-FileHash $fixture).Hash; runnerSha256=(Get-FileHash $runner).Hash
        fullRomCases=$expected; machineBoots=11; appBoots=6; runnerBoots=5; hostSemanticCases=0; compiledMc68000Cases=0
        ffsPartitions=2; payloadBytesPerPartition=2049; sysSelection=@('DH1 priority +5 / DH0 -128', 'DH0 priority +5 / DH1 -128')
        guestFlushBeforeHostSaveOrDispose=$true; reset=$true; sessionReplacement=$true; processRestart=$true; freshProtectedReadback=$true
        readOnlyMutationControls=2; installedPackage='UNAVAILABLE: source app/runtime and runner only'
    } | ConvertTo-Json | Set-Content (Join-Path $output 'qualification.json') -Encoding utf8
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    $compression = Compress-CopperScreenCompletedArtifacts -RepositoryRoot $repository -Path $output
    if ($compression.Status -eq 'SKIPPED') { Write-Warning "Completed output was not compressed: $($compression.Reason)" }
}
