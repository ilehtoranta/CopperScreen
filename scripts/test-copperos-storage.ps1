[CmdletBinding()]
param([Parameter(Mandatory)][string]$Rom,
      [Parameter(Mandatory)][string]$ProofDirectory,
      [string]$ResultsDirectory='artifacts/copperos-storage-results')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path $PSScriptRoot -Parent
$proof=(Resolve-Path $ProofDirectory).Path
$receipt=Get-Content (Join-Path $proof 'system-image.json') -Raw | ConvertFrom-Json
if ($receipt.shipping -ne $false -or $receipt.kind -ne 'original-system-boot-proof' -or
    $receipt.inputManifestSha256 -cne (Get-FileHash (Join-Path $proof 'native-manifest.json')).Hash.ToLowerInvariant() -or
    $receipt.sha256 -cne (Get-FileHash (Join-Path $proof 'System.adf')).Hash.ToLowerInvariant()) { throw 'A complete hash-bound original storage seed is required.' }
$oldRom=$env:COPPERSCREEN_COPPERSTART_ROM
$oldProof=$env:COPPERSCREEN_COPPEROS_STORAGE
try {
    $env:COPPERSCREEN_COPPERSTART_ROM=(Resolve-Path $Rom).Path
    $env:COPPERSCREEN_COPPEROS_STORAGE=$proof
    & dotnet test (Join-Path $repo 'CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj') -c Release -m:1 --filter FullyQualifiedName~CopperOsStorageTests --logger 'trx;LogFileName=copperos-storage.trx' --results-directory $ResultsDirectory
    if ($LASTEXITCODE) { throw 'Native storage/restart proof failed; inspect preserved captures.' }
    [xml]$trx=Get-Content (Join-Path $ResultsDirectory 'copperos-storage.trx') -Raw
    if ([int]$trx.TestRun.ResultSummary.Counters.passed -ne 3 -or [int]$trx.TestRun.ResultSummary.Counters.failed -ne 0) { throw 'Native persistence proof did not execute; skipped coverage is unavailable.' }
} finally {
    $env:COPPERSCREEN_COPPERSTART_ROM=$oldRom
    $env:COPPERSCREEN_COPPEROS_STORAGE=$oldProof
}
