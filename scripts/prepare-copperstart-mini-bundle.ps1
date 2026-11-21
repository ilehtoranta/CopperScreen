[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Rom,
    [Parameter(Mandatory)][string]$License,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository 'artifacts/copperstart-mini-bundle' }
$expected = (Get-Content -Raw -LiteralPath (Join-Path $repository 'CopperScreen/Firmware/CopperStartMini.sha256')).Trim()
$romSource = (Get-Item -LiteralPath $Rom).FullName
$licenseSource = (Get-Item -LiteralPath $License).FullName
if ((Get-FileHash -LiteralPath $romSource -Algorithm SHA256).Hash -ine $expected) {
    throw 'The Mini ROM does not match the qualified WinUAE build.'
}
if ([string]::IsNullOrWhiteSpace((Get-Content -Raw -LiteralPath $licenseSource))) {
    throw 'Provide the CopperStart Mini binary license.'
}

$staged = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$romDestination = Join-Path $staged 'CopperStart-Mini.rom'
$licenseDestination = Join-Path $staged 'LICENSE.txt'
if ($licenseSource -ne $licenseDestination) { Copy-Item -LiteralPath $licenseSource -Destination $licenseDestination }
if ($romSource -ne $romDestination) { Copy-Item -LiteralPath $romSource -Destination $romDestination }
"Verified CopperStart Mini bundle staged at $staged"
