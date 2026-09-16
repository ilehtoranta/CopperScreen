[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$AdditionalPackageSource,
    [string]$PackagesPath
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $repository ('artifacts/package-' + [Guid]::NewGuid().ToString('N'))
$publish = Join-Path $output 'CopperScreen'
$null = New-Item -ItemType Directory -Path $publish
$arguments = @('publish', "$repository/CopperScreen/CopperScreen.csproj", '-c', 'Release', '-r', $RuntimeIdentifier, '--self-contained', 'true', '-p:PublishTrimmed=false', '-p:DebugType=None', '-p:DebugSymbols=false', "-p:NuGetLockFilePath=$output/packages.lock.json", '-o', $publish)
if ($AdditionalPackageSource) { $arguments += "-p:RestoreAdditionalProjectSources=$AdditionalPackageSource" }
if ($PackagesPath) { $arguments += "-p:RestorePackagesPath=$PackagesPath" }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed; evidence retained in artifacts.' }
Copy-Item -LiteralPath "$repository/LICENSE", "$repository/THIRD-PARTY-NOTICES.md" -Destination $publish
$archive = Join-Path $output "CopperScreen-$RuntimeIdentifier.zip"
Compress-Archive -Path "$publish/*" -DestinationPath $archive
Get-FileHash -LiteralPath $archive -Algorithm SHA256
Write-Host "Local package: $archive"
# No release creation, remote push, deletion or credential handling.
