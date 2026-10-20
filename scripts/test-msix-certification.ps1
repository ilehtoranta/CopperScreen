[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [string]$OutputDirectory,
    [string]$AppCertPath,
    [ValidateRange(60, 3600)][int]$TimeoutSeconds = 1200
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'msix/CopperScreen.Msix.psm1') -Force
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot ('../artifacts/msix-wack-' + [Guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'WACK evidence requires a new or empty output directory.' }
$null = New-Item -ItemType Directory -Path $output -Force
$record = [ordered]@{ SchemaVersion = 1; Status = 'RUNNING'; StoreCertified = $false }
$failure = $null
$ownsRegistration = $false
try {
    if (-not $IsWindows) { $record.Status = 'UNAVAILABLE'; throw 'Windows App Certification Kit requires Windows.' }
    $package = (Resolve-Path -LiteralPath $PackagePath).Path
    $identity = Get-CopperScreenMsixIdentity $package
    $record.Identity = $identity
    $record.Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    if (-not $identity.Signed) { $record.Status = 'UNAVAILABLE'; throw 'WACK package deployment requires a signed package trusted by the test machine.' }
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        $record.Status = 'UNAVAILABLE'; throw 'Run WACK from an elevated shell on the dedicated test machine; this script never requests elevation or changes trust.'
    }
    if (@(Get-AppxPackage -Name $identity.Name).Count) { throw 'WACK refused a pre-existing package identity. Use a clean test account/machine.' }
    if (-not $AppCertPath) { $AppCertPath = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/App Certification Kit/appcert.exe' }
    if (-not (Test-Path -LiteralPath $AppCertPath -PathType Leaf)) { $record.Status = 'UNAVAILABLE'; throw 'Windows App Certification Kit is not installed.' }
    $record.Tool = @{ Path = (Resolve-Path -LiteralPath $AppCertPath).Path; Version = (Get-Item -LiteralPath $AppCertPath).VersionInfo.FileVersion }
    $record.StartedAt = (Get-Date).ToString('o')
    $reportPath = Join-Path $output 'wack-report.xml'
    $arguments = 'test -appxpackagepath "' + $package + '" -reportoutputpath "' + $reportPath + '"'
    $ownsRegistration = $true
    $process = Start-Process -FilePath $AppCertPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'wack-stdout.log') -RedirectStandardError (Join-Path $output 'wack-stderr.log')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $process.WaitForExit(500)) {
        if ($timer.Elapsed.TotalSeconds -gt $TimeoutSeconds) { $process.Kill(); throw 'WACK timed out; no passing certification evidence.' }
    }
    $record.ExitCode = $process.ExitCode
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath)) { throw 'WACK failed or did not generate its XML report.' }
    $xml = Read-CopperScreenXml (Get-Content -LiteralPath $reportPath -Raw)
    $record.Report = @{ Path = $reportPath; Sha256 = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash }
    $record.Summary = Get-CopperScreenWackSummary $xml
    if ($record.Summary.AppName -cne $identity.Name -or [version]$record.Summary.AppVersion -ne $identity.Version -or
        $record.Summary.PartialRun -cne 'FALSE' -or -not $record.Summary.RequiredTestsPassed) { throw 'WACK identity, completeness or required tests did not pass; inspect the recorded per-test summary.' }
    Assert-CopperScreenWackReport $xml
    $record.Status = 'PASSED'
}
catch {
    if ($record.Status -eq 'RUNNING') { $record.Status = 'FAILED' }
    $record.Error = $_.Exception.ToString()
    $failure = $_
}
finally {
    $record.CompletedAt = (Get-Date).ToString('o')
    if ($ownsRegistration) {
        try {
            foreach ($package in @(Get-AppxPackage -Name $identity.Name)) {
                if ($package.Publisher -cne $identity.Publisher) { throw 'WACK cleanup refused an unexpected publisher.' }
                Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
            }
            $record.Cleanup = 'Removed only the registration created during this WACK run.'
        }
        catch { $record.Cleanup = $_.Exception.ToString(); $record.Status = 'FAILED'; $failure = $_ }
    }
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'wack-record.json') -Encoding utf8
}
if ($failure) { throw $failure }
Write-Output "WACK report: $reportPath"
