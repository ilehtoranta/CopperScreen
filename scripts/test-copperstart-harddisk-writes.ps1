[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$HardDiskFixturePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$RunnerPath,
    [string]$AdditionalPackageSource,
    [switch]$IncludeTd64,
    [switch]$IncludeLargeVolumeWrites,
    [switch]$IncludeAllocationStress
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$artifactsModule = Join-Path $PSScriptRoot 'CopperScreen.Artifacts.psm1'
Import-Module $artifactsModule -Force
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$contract = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperStart.json') -Raw | ConvertFrom-Json
$rom = (Resolve-Path -LiteralPath $RomPath).Path
$fixture = (Resolve-Path -LiteralPath $HardDiskFixturePath).Path
if ((Get-Item $rom).Length -ne $contract.bytes -or (Get-FileHash $rom).Hash -ine $contract.sha256) { throw 'Supply the pinned raw CopperStart ROM.' }
if ((Get-FileHash $fixture).Hash -ne '74E8A6F56EC6941E66AFF1B653CB13975213026526E4F16BDD8915E55A750046') { throw 'Supply the qualified original copperhdf-track.hdf fixture. It is never modified.' }
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
$names = @('COPPERSCREEN_COPPERSTART_ROM','COPPERSCREEN_COPPERSTART_HDF','COPPERSCREEN_LIGHTWEIGHT_RUNNER','COPPERSCREEN_HDF_WRITE_OUTPUT','COPPERSCREEN_TD64_LARGE_WRITES','COPPERSCREEN_TD64_ALLOCATION_STRESS')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:COPPERSCREEN_COPPERSTART_ROM = $rom
    $env:COPPERSCREEN_COPPERSTART_HDF = $fixture
    $env:COPPERSCREEN_LIGHTWEIGHT_RUNNER = $runner
    $env:COPPERSCREEN_HDF_WRITE_OUTPUT = Join-Path $output 'native'
    $large = $IncludeLargeVolumeWrites -or $IncludeAllocationStress
    $td64 = $IncludeTd64 -or $large
    if ($large) { $env:COPPERSCREEN_TD64_LARGE_WRITES = '1' }
    if ($IncludeAllocationStress) { $env:COPPERSCREEN_TD64_ALLOCATION_STRESS = '1' }
    $filter = 'FullyQualifiedName~CopperStartHardDiskWriteTests'
    if (!$IncludeAllocationStress) { $filter += '&FullyQualifiedName!~AllocationStress' }
    if (!$td64) { $filter += '&FullyQualifiedName!~Td64' }
    elseif (!$large) { $filter += '&FullyQualifiedName!~Td64LargeVolumeBootsUpperFilesWrites' }
    $arguments = @('test', (Join-Path $repository 'CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj'), '-c', 'Release', '-m:1',
        '--artifacts-path', (Join-Path $output 'build'), '--filter', $filter,
        '--logger', 'trx;LogFileName=frontend-writes.trx', '--results-directory', $output)
    if ($AdditionalPackageSource) { $arguments += "-p:RestoreAdditionalProjectSources=$AdditionalPackageSource" }
    & dotnet @arguments 2>&1 | Tee-Object (Join-Path $output 'tests.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Native hard-disk write validation failed.' }
    [xml]$trx = Get-Content (Join-Path $output 'frontend-writes.trx') -Raw
    $expected = if ($IncludeAllocationStress) { 9 } elseif ($large) { 8 } elseif ($td64) { 7 } else { 4 }
    if (@($trx.TestRun.Results.UnitTestResult).Count -ne $expected -or @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -ne Passed).Count) { throw "All $expected intended full-ROM cases must pass without skips." }
    @{
        protocol='copperstart-frontend-hdf-writes-v1'; passed=$true; qualificationId=$contract.qualificationId
        romSha256=$contract.sha256; fixtureSha256=(Get-FileHash $fixture).Hash; runnerSha256=(Get-FileHash $runner).Hash
        fullRomCases=$expected; machineBoots=$(if ($IncludeAllocationStress) {26} elseif ($large) {24} elseif ($td64) {22} else {9}); appBoots=$(if ($IncludeAllocationStress) {16} elseif ($large) {14} elseif ($td64) {12} else {5}); runnerBoots=$(if ($td64) {10} else {4}); hostSemanticCases=0; compiledMc68000Cases=0
        td64=[bool]$td64; largeVolumeWrites=[bool]$large; allocationStress=[bool]$IncludeAllocationStress
        guestFlushBeforeHostSaveOrDispose=$true; reset=$true; sessionReplacement=$true; processRestart=$true; freshProtectedReadback=$true
        readOnlyMutationControls=2; installedPackage='UNAVAILABLE: source app/runtime and runner only'
    } | ConvertTo-Json | Set-Content (Join-Path $output 'qualification.json') -Encoding utf8
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    $compression = Compress-CopperScreenCompletedArtifacts -RepositoryRoot $repository -Path $output
    if ($compression.Status -eq 'SKIPPED') { Write-Warning "Completed output was not compressed: $($compression.Reason)" }
}
