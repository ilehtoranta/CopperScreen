[CmdletBinding()]
param([string]$Output = "$PSScriptRoot/../artifacts/copperexplorer/feed", [switch]$PublishPrivate)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/..").Path
$outputPath = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $outputPath | Out-Null
Push-Location $repo
try {
    $fixturePath = Join-Path $repo 'artifacts/copperexplorer/fixtures'
    $env:COPPEREXPLORER_FIXTURE_OUTPUT = $fixturePath
    dotnet test CopperScreen.Desktop.Tests/CopperScreen.Desktop.Tests.csproj -c Release --filter FullyQualifiedName~GeneratesIndependentConsumerFixtures
    if ($LASTEXITCODE) { throw 'Fixture generation failed.' }
    foreach ($project in @('CopperDisk/CopperDisk.csproj','CopperDisk.FileSystem/CopperDisk.FileSystem.csproj')) {
        dotnet pack $project -c Release -o $outputPath
        if ($LASTEXITCODE) { throw "Package build failed: $project" }
    }
    $service = Join-Path $repo 'artifacts/packages/copperstart.filesystem.service/0.5.0-dev.10/copperstart.filesystem.service.0.5.0-dev.10.nupkg'
    if (!(Test-Path -LiteralPath $service)) { throw 'The exact qualified service artifact is required; do not rebuild or substitute it.' }
    if ((Get-FileHash -LiteralPath $service -Algorithm SHA256).Hash -ne 'FC9671935C2D5ED41D16F67EBFCA623AD5F546645AC60E01219D17AB99BEDBBF') { throw 'Qualified service artifact hash mismatch.' }
    Copy-Item -LiteralPath $service -Destination $outputPath -Force
    $fixtureProject = Join-Path $fixturePath 'Fixtures.csproj'
    @'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><PackageId>CopperExplorer.TestFixtures</PackageId><Version>0.1.0-dev.3</Version><IncludeBuildOutput>false</IncludeBuildOutput><SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking><EnableDefaultCompileItems>false</EnableDefaultCompileItems><PackageLicenseExpression>MIT</PackageLicenseExpression><Description>Generated test-only Amiga filesystem fixtures. No third-party media.</Description></PropertyGroup><ItemGroup><None Include="*.adf;*.hdf;*.ipf;*.dms;*.json" Pack="true" PackagePath="fixtures/" /></ItemGroup></Project>
'@ | Set-Content -LiteralPath $fixtureProject -Encoding utf8
    dotnet pack $fixtureProject -c Release -o $outputPath
    if ($LASTEXITCODE) { throw 'Fixture package failed.' }
    Get-ChildItem -LiteralPath $outputPath -Filter '*.nupkg' | Get-FileHash -Algorithm SHA256 | Select-Object Hash,@{n='File';e={Split-Path $_.Path -Leaf}} | ConvertTo-Json | Set-Content (Join-Path $outputPath 'hashes.json')
    if ($PublishPrivate) {
        if (!$env:COPPEREXPLORER_PACKAGES_TOKEN) { throw 'COPPEREXPLORER_PACKAGES_TOKEN needs read:packages/write:packages. Never commit credentials.' }
        # Package visibility belongs to the package, not to the version. Never
        # publish development versions into an existing public package.
        $priorGhToken = $env:GH_TOKEN
        try {
            $env:GH_TOKEN = $env:COPPEREXPLORER_PACKAGES_TOKEN
            foreach ($id in @('CopperDisk','CopperDisk.FileSystem','CopperExplorer.TestFixtures','CopperStart.FileSystem.Service')) {
                $visibility = gh api "/users/ilehtoranta/packages/nuget/$id" --jq '.visibility' 2>&1
                if ($LASTEXITCODE -eq 0) {
                    if ("$visibility" -ne 'private') { throw "Refusing publication into a non-private package: $id" }
                } elseif ("$visibility" -notmatch '\(HTTP 404\)') { throw "Cannot verify private package access: $id" }
            }
        } finally { $env:GH_TOKEN = $priorGhToken }
        $env:NuGetPackageSourceCredentials_CopperExplorerPrivate = "Username=ilehtoranta;Password=$env:COPPEREXPLORER_PACKAGES_TOKEN;ValidAuthenticationTypes=Basic"
        $config = Join-Path $outputPath 'private-push.config'
        '<configuration><packageSources><clear/><add key="CopperExplorerPrivate" value="https://nuget.pkg.github.com/ilehtoranta/index.json"/></packageSources></configuration>' | Set-Content $config
        foreach ($package in Get-ChildItem -LiteralPath $outputPath -Filter '*.nupkg') {
            dotnet nuget push $package.FullName --source CopperExplorerPrivate --configfile $config --api-key $env:COPPEREXPLORER_PACKAGES_TOKEN
            if ($LASTEXITCODE) { throw "Private publication failed. Existing versions are immutable: $($package.Name)" }
        }
    }
} finally { Remove-Item Env:COPPEREXPLORER_FIXTURE_OUTPUT -ErrorAction SilentlyContinue; Pop-Location }
