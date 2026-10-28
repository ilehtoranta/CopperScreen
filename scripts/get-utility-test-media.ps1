#requires -Version 5.1
<#
.SYNOPSIS
Downloads or verifies the pinned, local Amiga utility test collection.
.DESCRIPTION
Keeps original distributions intact and extracts only the two pinned ZIP ADF
members. Does not execute software or install anything in a guest. See
docs/UTILITY_TEST_CORPUS.md for licences, prerequisites and A/B procedure.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../media/utilities'),
    [string[]]$Id,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'qualification/amiga-utilities-v1.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) { throw 'Unsupported utility manifest schema.' }
$root = [System.IO.Path]::GetFullPath($OutputDirectory)

function Get-CollectionPath([string]$RelativePath) {
    if ([System.IO.Path]::IsPathRooted($RelativePath)) { throw 'Expected a relative collection path.' }
    $path = [System.IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    $prefix = $root.TrimEnd([char[]]'\/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes collection directory: $RelativePath"
    }
    return $path
}

function Assert-Asset($Asset, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing input: $Path" }
    if ((Get-Item -LiteralPath $Path).Length -ne $Asset.bytes) { throw "Size mismatch: $Path" }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $Asset.sha256) { throw "SHA-256 mismatch: $Path. Keep the pinned version; review upstream changes separately." }
}

$applications = @($manifest.applications)
if ($Id) {
    foreach ($requestedId in $Id) {
        if ($requestedId -notin $applications.id) { throw "Unknown utility ID: $requestedId" }
    }
    $applications = @($applications | Where-Object { $_.id -in $Id })
}

$assetCount = 0
foreach ($application in $applications) {
    foreach ($asset in $application.assets) {
        $path = Get-CollectionPath $asset.file
        if (-not (Test-Path -LiteralPath $path)) {
            if ($VerifyOnly) { throw "Missing input: $path" }
            if ([Uri]::new($asset.url).Scheme -ne 'https') { throw 'Downloads must use HTTPS.' }
            New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
            $temporary = "$path.$([Guid]::NewGuid().ToString('N')).partial"
            try {
                Invoke-WebRequest -Uri $asset.url -OutFile $temporary -UseBasicParsing -TimeoutSec 60
                Assert-Asset $asset $temporary
                Move-Item -LiteralPath $temporary -Destination $path
            }
            finally {
                if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
            }
        }
        Assert-Asset $asset $path
        $assetCount++
        Write-Host "Verified $($application.name): $($asset.file)"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$imageCount = 0
foreach ($image in $manifest.readyImages) {
    if ($image.applicationId -notin $applications.id) { continue }
    $path = Get-CollectionPath $image.file
    if (-not (Test-Path -LiteralPath $path)) {
        if ($VerifyOnly) { throw "Missing extracted ADF: $path" }
        New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
        $archive = [System.IO.Compression.ZipFile]::OpenRead((Get-CollectionPath $image.archive))
        $temporary = "$path.$([Guid]::NewGuid().ToString('N')).partial"
        try {
            $entry = $archive.GetEntry($image.member)
            if ($null -eq $entry) { throw "Missing ZIP member: $($image.member)" }
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $temporary)
            Assert-Asset $image $temporary
            Move-Item -LiteralPath $temporary -Destination $path
        }
        finally {
            $archive.Dispose()
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
        }
    }
    Assert-Asset $image $path
    $imageCount++
    Write-Host "Verified extracted ADF: $($image.file)"
}

Write-Host "$($applications.Count) utilities, $assetCount original files, $imageCount extracted ADFs verified in $root"
Write-Host 'Acquisition/hash verification does not execute guest scenarios. See the manifest and evidence document for recorded coverage.'
