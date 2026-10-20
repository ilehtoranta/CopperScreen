[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Rom,
    [Parameter(Mandatory)][string]$ProofDirectory,
    [string]$ResultsDirectory = 'artifacts/copperos-proof-results'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$romPath = (Resolve-Path -LiteralPath $Rom).Path
$proof = (Resolve-Path -LiteralPath $ProofDirectory).Path
$receipt = Get-Content -LiteralPath (Join-Path $proof 'system-image.json') -Raw | ConvertFrom-Json
$manifestPath = Join-Path $proof 'native-manifest.json'
if ($receipt.kind -ne 'original-system-boot-proof' -or $receipt.shipping -ne $false -or
    $receipt.inputManifestSha256 -cne (Get-FileHash -LiteralPath $manifestPath).Hash.ToLowerInvariant()) {
    throw 'A hash-bound original nonshipping proof receipt is required.'
}
$previousRom = $env:COPPERSCREEN_COPPERSTART_ROM
$previousProof = $env:COPPERSCREEN_COPPEROS_PROOF
try {
    $env:COPPERSCREEN_COPPERSTART_ROM = $romPath
    $env:COPPERSCREEN_COPPEROS_PROOF = $proof
    & dotnet test (Join-Path $repo 'CopperScreen.Lightweight.Tests/CopperScreen.Lightweight.Tests.csproj') -c Release -m:1 `
        --filter 'FullyQualifiedName~CopperOsBootProofTests' --logger 'trx;LogFileName=copperos.trx' --results-directory $ResultsDirectory
    if ($LASTEXITCODE) { throw 'Native CopperOS Shell proof failed; inspect the transcript and captures.' }
    [xml]$result = Get-Content -LiteralPath (Join-Path $ResultsDirectory 'copperos.trx') -Raw
    if ([int]$result.TestRun.ResultSummary.Counters.passed -ne 1 -or [int]$result.TestRun.ResultSummary.Counters.failed -ne 0) {
        throw 'The intended native proof did not run successfully; skipped coverage is unavailable.'
    }
} finally {
    $env:COPPERSCREEN_COPPERSTART_ROM = $previousRom
    $env:COPPERSCREEN_COPPEROS_PROOF = $previousProof
}
