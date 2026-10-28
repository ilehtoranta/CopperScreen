[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CopperStartA1200Rom,
    [Parameter(Mandatory)][string]$CopperOsHdf,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository 'artifacts/copperos-bundle' }
$inputs = @(
    @{ Source = $CopperStartA1200Rom; Name = 'CopperStart-A1200.rom'; Contract = 'CopperStartA1200.json' },
    @{ Source = $CopperOsHdf; Name = 'CopperOS.hdf'; Contract = 'CopperOsA1200.json' }
)
# Validate both inputs before staging either. Binaries stay outside source control.
foreach ($inputFile in $inputs) {
    $contract = Get-Content -LiteralPath (Join-Path $repository "CopperScreen/Firmware/$($inputFile.Contract)") -Raw | ConvertFrom-Json
    if ((Get-Item -LiteralPath $inputFile.Source).Length -ne $contract.Bytes -or
        (Get-FileHash -LiteralPath $inputFile.Source -Algorithm SHA256).Hash -ine $contract.Sha256) {
        throw "Bundle input does not match $($inputFile.Contract): $($inputFile.Source)"
    }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($inputFile in $inputs) {
    $destination = Join-Path $OutputDirectory $inputFile.Name
    if ([IO.Path]::GetFullPath($inputFile.Source) -ine [IO.Path]::GetFullPath($destination)) {
        Copy-Item -LiteralPath $inputFile.Source -Destination $destination -Force
    }
}
Write-Output "Verified CopperOS bundle staged at $OutputDirectory"
