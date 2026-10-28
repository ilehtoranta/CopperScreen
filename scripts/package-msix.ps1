[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IdentityName,
    [Parameter(Mandatory)][string]$Publisher,
    [Parameter(Mandatory)][string]$PublisherDisplayName,
    [Parameter(Mandatory)][string]$PackageVersion,
    [ValidateSet('win-x64')][string]$RuntimeIdentifier = 'win-x64',
    [string]$OutputDirectory,
    [string]$WindowsSdkBin,
    [string]$AdditionalPackageSource,
    [string]$PackagesPath,
    [string]$CertificateThumbprint,
    [string]$CopperOsSeed,
    [string]$CopperStartRom,
    [switch]$InstalledTestBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'MSIX packaging requires Windows and the Windows SDK.' }
Import-Module (Join-Path $PSScriptRoot 'msix/CopperScreen.Msix.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'CopperScreen.Artifacts.psm1') -Force
Assert-CopperScreenIdentity $IdentityName $Publisher $PackageVersion -InstalledTestBuild:$InstalledTestBuild
if ([string]::IsNullOrWhiteSpace($PublisherDisplayName)) { throw 'PublisherDisplayName is required.' }
$makeappx = Find-CopperScreenSdkTool 'makeappx.exe' $WindowsSdkBin
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository ('artifacts/msix-' + [Guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a new or empty output directory; existing package artifacts are never overwritten.' }
$layout = Join-Path $output 'Layout'
$null = New-Item -ItemType Directory -Path $layout -Force
$lockDirectory = Join-Path $output 'locks'
$null = New-Item -ItemType Directory -Path $lockDirectory
$sourceLocks = @{}
foreach ($projectName in @('CopperScreen', 'CopperMod.Amiga.Lightweight', 'CopperDisk')) {
    $sourceLock = Join-Path $repository "$projectName/packages.lock.json"
    Copy-Item -LiteralPath $sourceLock -Destination (Join-Path $lockDirectory "$projectName.packages.lock.json")
    $sourceLocks[$projectName] = Get-Content -LiteralPath $sourceLock -Raw | ConvertFrom-Json -AsHashtable
}

# A private lock file per project avoids modifying checked-in locks during RID restore.
$lockTargets = Join-Path $output 'Msix.Restore.targets'
@'
<Project><PropertyGroup><NuGetLockFilePath>$(MSBuildThisFileDirectory)locks/$(MSBuildProjectName).packages.lock.json</NuGetLockFilePath></PropertyGroup></Project>
'@ | Set-Content -LiteralPath $lockTargets -Encoding utf8
$arguments = @('publish', (Join-Path $repository 'CopperScreen/CopperScreen.csproj'), '-c', 'Release', '-r', $RuntimeIdentifier,
    '-m:1',
    '--self-contained', 'true', '--artifacts-path', (Join-Path $output 'build'), '-o', $layout,
    '-p:PublishTrimmed=false', '-p:PublishSingleFile=false', '-p:DebugType=portable', '-p:DebugSymbols=true',
    "-p:Version=$PackageVersion", "-p:AssemblyVersion=$PackageVersion", "-p:FileVersion=$PackageVersion",
    "-p:CustomBeforeMicrosoftCommonTargets=$lockTargets", "-p:CopperScreenInstalledTests=$($InstalledTestBuild.IsPresent.ToString().ToLowerInvariant())")
if ($AdditionalPackageSource) { $arguments += "-p:RestoreAdditionalProjectSources=$AdditionalPackageSource" }
if ($PackagesPath) { $arguments += "-p:RestorePackagesPath=$PackagesPath" }
& dotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $output 'publish.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed; build evidence is retained in the output directory.' }
foreach ($projectName in $sourceLocks.Keys) {
    $restored = Get-Content -LiteralPath (Join-Path $lockDirectory "$projectName.packages.lock.json") -Raw | ConvertFrom-Json -AsHashtable
    foreach ($target in $sourceLocks[$projectName].dependencies.Keys) {
        foreach ($dependency in $sourceLocks[$projectName].dependencies[$target].Keys) {
            $before = $sourceLocks[$projectName].dependencies[$target][$dependency]
            $after = $restored.dependencies[$target][$dependency]
            if ($before.ContainsKey('resolved') -and ($before.resolved -cne $after.resolved -or $before.contentHash -cne $after.contentHash)) {
                throw "RID restore changed locked dependency $projectName/$dependency. Qualify new dependencies separately."
            }
        }
    }
}

foreach ($required in @('CopperScreen.exe', 'CopperScreen.dll', 'coreclr.dll', 'libSkiaSharp.dll', 'libHarfBuzzSharp.dll', 'miniaudio.dll', 'HidSharp.dll', 'av_libglesv2.dll', 'Presets')) {
    if (-not (Test-Path -LiteralPath (Join-Path $layout $required))) { throw "Published runtime input missing: $required" }
}
if (Test-Path -LiteralPath (Join-Path $layout 'Profiles')) { throw 'A package must not contain writable personal Profiles.' }
$manifestTool = Find-CopperScreenSdkTool 'mt.exe' $WindowsSdkBin
$executableManifest = Join-Path $output 'executable.manifest.xml'
& $manifestTool "-inputresource:$(Join-Path $layout 'CopperScreen.exe');#1" "-out:$executableManifest" 2>&1 | Tee-Object -FilePath (Join-Path $output 'executable-manifest.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the published executable manifest.' }
Assert-CopperScreenExecutableManifest (Read-CopperScreenXml (Get-Content -LiteralPath $executableManifest -Raw))
if ((Test-CopperScreenDiagnosticAssembly (Join-Path $layout 'CopperScreen.dll')) -ne $InstalledTestBuild.IsPresent) {
    throw 'Compiled diagnostic entry point does not match the requested package kind. Do not submit a diagnostic assembly to Store.'
}
$nativeGuestProof = $false
if ($CopperOsSeed -or $CopperStartRom) {
    if (-not $InstalledTestBuild -or -not $CopperOsSeed -or -not $CopperStartRom) { throw 'Native guest proof assets require both explicit paths and an installed diagnostic build.' }
    $osMetadata = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperOS.json') -Raw | ConvertFrom-Json
    $romMetadata = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperStart.json') -Raw | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath $CopperOsSeed).Hash.ToLowerInvariant() -cne $osMetadata.Sha256 -or
        (Get-FileHash -LiteralPath $CopperStartRom).Hash.ToLowerInvariant() -cne $romMetadata.sha256) { throw 'Native diagnostic guest assets do not match the qualified metadata.' }
    $guestLayout = Join-Path $layout 'Diagnostics/GuestProof'
    New-Item -ItemType Directory -Path $guestLayout -Force | Out-Null
    Copy-Item -LiteralPath $CopperOsSeed -Destination (Join-Path $guestLayout 'System.adf')
    Copy-Item -LiteralPath $CopperStartRom -Destination (Join-Path $guestLayout 'CopperStart-DiskBoot.rom')
    $nativeGuestProof = $true
}

Copy-Item -LiteralPath (Join-Path $repository 'LICENSE'), (Join-Path $repository 'THIRD-PARTY-NOTICES.md') -Destination $layout
Copy-Item -LiteralPath (Join-Path $repository 'CopperDisk/THIRD-PARTY-NOTICES.md') -Destination (Join-Path $layout 'CopperDisk-THIRD-PARTY-NOTICES.md')
$symbols = Join-Path $output 'Symbols'
$null = New-Item -ItemType Directory -Path $symbols
Get-ChildItem -LiteralPath $layout -Filter '*.pdb' -File | Move-Item -Destination $symbols
New-CopperScreenMsixAssets (Join-Path $repository 'CopperScreen/Assets/CopperScreen.ico') (Join-Path $layout 'Assets')
$manifest = Read-CopperScreenXml (Get-Content -LiteralPath (Join-Path $repository 'packaging/msix/AppxManifest.xml') -Raw)
$manifest.Package.Identity.SetAttribute('Name', $IdentityName)
$manifest.Package.Identity.SetAttribute('Publisher', $Publisher)
$manifest.Package.Identity.SetAttribute('Version', $PackageVersion)
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
if ($InstalledTestBuild) {
    $manifest.Package.Properties.DisplayName = 'CopperScreen package diagnostics'
    $manifest.Package.Applications.Application.VisualElements.SetAttribute('DisplayName', 'CopperScreen package diagnostics')
    @{ SchemaVersion = 1; Purpose = 'Installed package tests only; never submit to Store.' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $layout 'installed-test-build.json') -Encoding utf8
}
$manifest.Save((Join-Path $layout 'AppxManifest.xml'))
$packagePath = Join-Path $output ("CopperScreen-$PackageVersion-$RuntimeIdentifier.msix")
& $makeappx pack /d $layout /p $packagePath /h SHA256 /no 2>&1 | Tee-Object -FilePath (Join-Path $output 'makeappx.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx validation/packing failed.' }
if ($CertificateThumbprint) {
    $certificate = Get-Item -LiteralPath "Cert:/CurrentUser/My/$CertificateThumbprint"
    if (-not $certificate.HasPrivateKey -or $certificate.Subject -cne $Publisher) { throw 'Signing certificate must have a private key and match the manifest Publisher exactly.' }
    $signtool = Find-CopperScreenSdkTool 'signtool.exe' $WindowsSdkBin
    & $signtool sign /fd SHA256 /sha1 $CertificateThumbprint /s My $packagePath 2>&1 | Tee-Object -FilePath (Join-Path $output 'signing.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'MSIX signing failed.' }
}
$identity = Get-CopperScreenMsixIdentity $packagePath
$files = @(Get-ChildItem -LiteralPath $layout -Recurse -File | ForEach-Object {
    @{ Path = [IO.Path]::GetRelativePath($layout, $_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$record = [ordered]@{
    SchemaVersion = 1; Package = $packagePath; Sha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
    Identity = $identity; InstalledTestBuild = $InstalledTestBuild.IsPresent
    SourceCommit = (& git -C $repository rev-parse HEAD); WorkingTreeDirty = [bool](& git -C $repository status --porcelain)
    DotnetSdk = (& dotnet --version); MakeAppx = $makeappx; Files = $files
    SourceInputs = @(& git -C $repository ls-files --cached --others --exclude-standard -- CopperScreen CopperDisk CopperMod.Amiga.Lightweight packaging/msix |
        Where-Object { $_ -match '\.(cs|csproj|json|ico|xml|manifest)$' -and (Test-Path -LiteralPath (Join-Path $repository $_) -PathType Leaf) } |
        ForEach-Object { @{ Path = $_; Sha256 = (Get-FileHash -LiteralPath (Join-Path $repository $_) -Algorithm SHA256).Hash } })
    GuestAssets = 'Embedded qualified CopperStart A1200 ROM and CopperOS HDF; personal writable HDF created on first selection.'
    BundledCopperStart = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperStartA1200.json') -Raw | ConvertFrom-Json
    BundledCopperOs = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperOsA1200.json') -Raw | ConvertFrom-Json
    DiagnosticA500GuestAssets = $nativeGuestProof
}
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'package-record.json') -Encoding utf8
$compression = Compress-CopperScreenCompletedArtifacts -RepositoryRoot $repository -Path $output
if ($compression.Status -eq 'SKIPPED') { Write-Warning "Completed package output was not compressed: $($compression.Reason)" }
[pscustomobject]@{ PackagePath = $packagePath; OutputDirectory = $output; Layout = $layout; Identity = $identity }
