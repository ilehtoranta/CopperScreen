[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RomPath,
    [string]$LotusDiskPath,
    [string]$HardDiskFixturePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$AdditionalPackageSource
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$artifactsModule = Join-Path $PSScriptRoot 'CopperScreen.Artifacts.psm1'
Import-Module $artifactsModule -Force
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifact = Get-Content -LiteralPath (Join-Path $repository 'CopperScreen/Firmware/CopperStart.json') -Raw | ConvertFrom-Json
$rom = (Resolve-Path -LiteralPath $RomPath).Path
if ((Get-Item -LiteralPath $rom).Length -ne $artifact.bytes -or (Get-FileHash -LiteralPath $rom -Algorithm SHA256).Hash -ine $artifact.sha256) {
    throw "Supply the raw qualified CopperStart $($artifact.qualificationId) ROM, SHA-256 $($artifact.sha256). No ROM is downloaded or rebuilt."
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a new or empty qualification output directory.' }
$null = New-Item -ItemType Directory -Path $output -Force
$names = @('COPPERSCREEN_COPPERSTART_ROM', 'COPPERSCREEN_COPPERSTART_LOTUS', 'COPPERSCREEN_COPPERSTART_OUTPUT', 'COPPERSCREEN_COPPERSTART_HDF')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:COPPERSCREEN_COPPERSTART_ROM = $rom
    $env:COPPERSCREEN_COPPERSTART_LOTUS = $LotusDiskPath
    $env:COPPERSCREEN_COPPERSTART_HDF = $HardDiskFixturePath
    $env:COPPERSCREEN_COPPERSTART_OUTPUT = Join-Path $output 'lotus-app'
    $arguments = @('test', (Join-Path $repository 'CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj'),
        '-c', 'Release', '-m:1', '--artifacts-path', (Join-Path $output 'build'), '--filter', 'FullyQualifiedName~CopperStartTests|FullyQualifiedName~QualifiedHardDiskFreshBootReadsThroughPersistedAppSettings',
        '--logger', 'trx;LogFileName=copperstart.trx', '--results-directory', $output)
    if ($AdditionalPackageSource) { $arguments += "-p:RestoreAdditionalProjectSources=$AdditionalPackageSource" }
    & dotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $output 'tests.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'CopperStart qualification tests failed. Inspect tests.log and copperstart.trx.' }
    [xml]$trx = Get-Content -LiteralPath (Join-Path $output 'copperstart.trx') -Raw
    $counts = $trx.TestRun.ResultSummary.Counters
    $expected = if ($LotusDiskPath) { 12 } else { 11 }
    if ($HardDiskFixturePath) { $expected++ }
    if ([int]$counts.passed -ne $expected -or [int]$counts.failed -ne 0) { throw 'Intended CopperStart tests did not all run.' }
    @{
        SchemaVersion = 1
        Qualification = $artifact.qualificationId
        RomSha256 = $artifact.sha256
        Lifecycle = 'PASSED'
        HardDiskFreshReadback = $(if ($HardDiskFixturePath) { 'PASSED: two fresh app-session boots' } else { 'UNAVAILABLE: no qualified fixture supplied' })
        LotusReplay = $(if ($LotusDiskPath) { 'CAPTURES_REQUIRE_REVIEW' } else { 'UNAVAILABLE: no media supplied' })
        InstalledPackage = 'UNAVAILABLE: run the separately signed installed test pipeline'
        Note = 'Diagnostic compatibility checks, not a performance measurement or full-game claim.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'qualification.json') -Encoding utf8
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    $compression = Compress-CopperScreenCompletedArtifacts -RepositoryRoot $repository -Path $output
    if ($compression.Status -eq 'SKIPPED') { Write-Warning "Completed output was not compressed: $($compression.Reason)" }
}
