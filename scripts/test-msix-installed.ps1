[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselinePackage,
    [Parameter(Mandatory)][string]$UpdatePackage,
    [ValidateSet('Install', 'DevelopmentRegistration')][string]$DeploymentMode = 'Install',
    [string]$OutputDirectory,
    [string]$WindowsSdkBin,
    [ValidateRange(10, 1800)][int]$PhaseTimeoutSeconds = 900,
    [ValidateRange(1, 120)][int]$PhaseExitTimeoutSeconds = 30,
    [switch]$KeepInstalled,
    [switch]$MediaPersistence,
    [switch]$LauncherLayout
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'msix/CopperScreen.Msix.psm1') -Force
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository ('artifacts/msix-installed-' + [Guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Installed-test evidence requires a new or empty output directory.' }
$null = New-Item -ItemType Directory -Path $output -Force
$recordPath = Join-Path $output 'installed-test-record.json'
$record = [ordered]@{ SchemaVersion = 1; Protocol = 'copperscreen-installed-msix-v2'; PhaseExitTimeoutSeconds = $PhaseExitTimeoutSeconds; RequiredProcessExitCode = 0; Status = 'RUNNING'; DeploymentMode = $DeploymentMode; InstalledMsixAcceptance = $false; StoreCertified = $false; NativeCopperOsStorage = 'UNAVAILABLE: installed native workflow has not run'; Phases = @() }
if ($MediaPersistence) { $record.Protocol = 'copperscreen-installed-msix-v3'; $record['MediaPersistenceAcceptance'] = $false }
if ($LauncherLayout) {
    if (-not $MediaPersistence) { throw 'Launcher layout qualification requires the complete media persistence protocol.' }
    $record.Protocol = 'copperscreen-installed-msix-v4'
    $record['LauncherLayoutAcceptance'] = $false
}
$record['PhaseTimeoutSeconds'] = $PhaseTimeoutSeconds
$ownedIdentity = $null
$failure = $null

function Deploy-TestPackage([string]$PackagePath, [string]$LayoutName) {
    if ($DeploymentMode -eq 'Install') {
        Add-AppxPackage -Path $PackagePath -ErrorAction Stop
    }
    else {
        $layout = Join-Path $output $LayoutName
        & $script:makeappx unpack /p $PackagePath /d $layout /no | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Cannot unpack the diagnostic package for development registration.' }
        Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml') -ErrorAction Stop
    }
    $registered = @(Get-AppxPackage -Name $script:baseline.Name | Where-Object { $_.Publisher -ceq $script:baseline.Publisher })
    if ($registered.Count -ne 1) { throw 'Expected one registered test package for this user.' }
    if ($DeploymentMode -eq 'Install' -and $registered[0].IsDevelopmentMode) { throw 'Install acceptance requires an installed package, not a development registration.' }
    return $registered[0]
}

function Invoke-InstalledPhase($Package, [string]$Phase) {
    $reportPath = Join-Path $output ($Phase + '-' + $record.Phases.Count + '.json')
    $probePhase = $(if ($MediaPersistence) { 'media-' + $Phase } else { $Phase })
    $arguments = '--installed-test ' + $probePhase + ' ' + $script:runId + ' "' + $reportPath + '"'
    $applicationId = $Package.PackageFamilyName + '!CopperScreen'
    $activatedId = [CopperScreen.PackageActivation]::Activate($applicationId, $arguments)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Get-Process -Id $activatedId -ErrorAction SilentlyContinue
    if (-not $process) { throw 'Cannot observe the activated diagnostic process.' }
    $null = $process.Handle # Retain the handle so exit status remains observable after process termination.
    while (-not (Test-Path -LiteralPath $reportPath)) {
        if ($timer.Elapsed.TotalSeconds -gt $PhaseTimeoutSeconds) {
            if ($process -and -not $process.HasExited) { $process.Kill() }
            throw "Installed phase '$Phase' timed out; no passing evidence was produced."
        }
        if ($process -and $process.HasExited) { throw "Installed phase '$Phase' exited without a report." }
        Start-Sleep -Milliseconds 200
    }
    $result = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    try { $exit = Wait-CopperScreenProcessExit -Process $process -TimeoutSeconds $PhaseExitTimeoutSeconds }
    catch {
        $result | Add-Member -NotePropertyName HostProcess -NotePropertyValue @{
            Id=$activatedId; ExitCode=$(if ($process.HasExited) { $process.ExitCode } else { $null }); Failure=$_.Exception.Message
        }
        $record.Phases += $result
        $phaseError = $(if ($result.PSObject.Properties.Name -contains 'Error') { $result.Error } else { 'Phase report passed; process exit failed.' })
        throw "Installed phase '$Phase' failed process-exit validation: $phaseError $($_.Exception.Message)"
    }
    $result | Add-Member -NotePropertyName HostProcess -NotePropertyValue $exit
    $record.Phases += $result
    if (-not $result.Passed) { throw "Installed phase '$Phase' failed: $($result.Error)" }
    if ($result.PackageFullName -cne $Package.PackageFullName -or [version]$result.PackageVersion -ne $Package.Version) { throw 'Probe did not execute the expected installed package version.' }
    $expectedRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) "Packages/$($Package.PackageFamilyName)/LocalState/CopperScreen"
    $expectedDirectory = Join-Path $expectedRoot "InstalledTests/$script:runId"
    if ($result.PackageFamilyName -cne $Package.PackageFamilyName -or
        -not [String]::Equals($result.UserDataRoot, $expectedRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not [String]::Equals($result.UserDataDirectory, $expectedDirectory, [StringComparison]::OrdinalIgnoreCase)) { throw 'Installed diagnostics must use the current package family LocalState, not shared portable AppData.' }
    if (-not $result.WindowOpened -or $result.Elevated) { throw 'Expected a successfully opened, non-elevated desktop window.' }
    if ($MediaPersistence) { Assert-CopperScreenMediaPhase $result $probePhase }
    if ($LauncherLayout) { Assert-CopperScreenLauncherLayout $result.LauncherLayout }
    # Retain the real host files before a later phase can uninstall this family.
    # Probe hashes alone cannot be inspected once Windows removes LocalState.
    $stateDirectory = Join-Path $output ('HostState/' + [IO.Path]::GetFileNameWithoutExtension($reportPath))
    $null = New-Item -ItemType Directory -Path $stateDirectory
    $copies = @(foreach ($file in $result.Files.PSObject.Properties) {
        $relative = switch ($file.Name) {
            'lightweight-a500-kickstart13.json' { 'Profiles/lightweight-a500-kickstart13.json' }
            'last-machine.json' { 'last-machine.json' }
            'recent-session.json' { 'recent-session.json' }
            default { throw "Unexpected host-state evidence file '$($file.Name)'." }
        }
        $source = Join-Path $result.UserDataDirectory $relative
        $copy = Join-Path $stateDirectory $file.Name
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ine $file.Value) { throw 'Host-state source changed before evidence capture.' }
        Copy-Item -LiteralPath $source -Destination $copy
        $hash = (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash
        if ($hash -ine $file.Value) { throw 'Host-state evidence copy differs from the phase report.' }
        [pscustomobject]@{ Name=$file.Name; Source=$source; Path=$copy; Sha256=$hash }
    })
    $stateRecord = Join-Path $stateDirectory 'snapshot.json'
    @{ Phase=$probePhase; PackageFullName=$result.PackageFullName; UserDataDirectory=$result.UserDataDirectory; Files=$copies } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $stateRecord -Encoding utf8
    $result | Add-Member -NotePropertyName HostStateEvidence -NotePropertyValue @{ Record=$stateRecord; Files=$copies }
    return $result
}

try {
    if (-not $IsWindows) { $record.Status = 'UNAVAILABLE'; throw 'Installed MSIX testing requires Windows.' }
    $baselinePath = (Resolve-Path -LiteralPath $BaselinePackage).Path
    $updatePath = (Resolve-Path -LiteralPath $UpdatePackage).Path
    $script:baseline = Get-CopperScreenMsixIdentity $baselinePath
    $update = Get-CopperScreenMsixIdentity $updatePath
    Assert-CopperScreenUpdatePair $baseline $update
    $runId = $baseline.Name.Substring('CopperScreen.Test.'.Length)
    $record.Baseline = @{ Identity = $baseline; Sha256 = (Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash }
    $record.Update = @{ Identity = $update; Sha256 = (Get-FileHash -LiteralPath $updatePath -Algorithm SHA256).Hash }
    if (@(Get-AppxPackage -Name $baseline.Name).Count) { throw 'This diagnostic identity is already installed. Use a new run identity; the pipeline never replaces a pre-existing installation.' }
    if ($DeploymentMode -eq 'Install' -and (-not $baseline.Signed -or -not $update.Signed)) {
        $record.Status = 'UNAVAILABLE'
        throw 'Install mode requires signed packages whose publisher is trusted on this test machine. The pipeline never installs certificates or changes machine trust.'
    }
    if ($DeploymentMode -eq 'DevelopmentRegistration') {
        $development = Get-ItemProperty -LiteralPath 'HKLM:/SOFTWARE/Microsoft/Windows/CurrentVersion/AppModelUnlock' -ErrorAction SilentlyContinue
        if (-not $development -or $development.AllowDevelopmentWithoutDevLicense -ne 1) {
            $record.Status = 'UNAVAILABLE'; throw 'Development registration requires Windows Developer Mode, enabled by the machine owner.'
        }
        $script:makeappx = Find-CopperScreenSdkTool 'makeappx.exe' $WindowsSdkBin
    }
    if (-not ('CopperScreen.PackageActivation' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace CopperScreen {
    public static class PackageActivation {
        [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager {
            [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
            [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
            [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, out uint processId);
        }
        public static uint Activate(string id, string arguments) {
            var manager = (IApplicationActivationManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
            try {
                uint pid;
                int result = manager.ActivateApplication(id, arguments, 2 | 4, out pid);
                Marshal.ThrowExceptionForHR(result);
                return pid;
            } finally { Marshal.ReleaseComObject(manager); }
        }
    }
}
'@
    }
    # This synthetic customer file is outside package-managed storage and must survive removal.
    $customerFile = Join-Path $output 'user-owned-original.txt'
    'Original user content; never packaged.' | Set-Content -LiteralPath $customerFile -Encoding utf8
    $customerHash = (Get-FileHash -LiteralPath $customerFile -Algorithm SHA256).Hash
    $ownedIdentity = $baseline.Name
    $installed = Deploy-TestPackage $baselinePath 'BaselineLayout'
    $seed = Invoke-InstalledPhase $installed 'seed'
    # Relaunch the baseline before updating, proving an actual process restart.
    $restart = Invoke-InstalledPhase $installed 'verify'
    $installed = Deploy-TestPackage $updatePath 'UpdateLayout'
    if ($installed.Version -ne $update.Version) { throw 'Windows did not deploy the newer version.' }
    $verify = Invoke-InstalledPhase $installed 'verify'
    if ($MediaPersistence) {
        Assert-CopperScreenMediaUpdate $seed $restart
        Assert-CopperScreenMediaUpdate $seed $verify
    }
    if ($seed.UserDataDirectory -cne $verify.UserDataDirectory -or $seed.InstallDirectory -ceq $verify.InstallDirectory) { throw 'Expected unchanged user-data path and a changed package version directory.' }
    foreach ($file in @('lightweight-a500-kickstart13.json', 'last-machine.json', 'recent-session.json')) {
        if ($seed.Files.$file -cne $verify.Files.$file) { throw "Update changed persisted file '$file'." }
    }
    if ($seed.PSObject.Properties.Name -contains 'CopperOsStorage' -and $seed.CopperOsStorage.PSObject.Properties.Name -contains 'passed' -and $seed.CopperOsStorage.passed) {
        if (-not $restart.CopperOsStorage.passed -or -not $restart.CopperOsStorage.reopen -or -not $verify.CopperOsStorage.passed -or -not $verify.CopperOsStorage.reopen -or
            $seed.CopperOsStorage.workspace -cne $restart.CopperOsStorage.workspace -or
            $seed.CopperOsStorage.workspace -cne $verify.CopperOsStorage.workspace -or
            $seed.CopperOsStorage.workspacePairId -cne $restart.CopperOsStorage.workspacePairId -or
            $seed.CopperOsStorage.workspacePairId -cne $verify.CopperOsStorage.workspacePairId -or
            $seed.CopperOsStorage.imageSha256 -cne $restart.CopperOsStorage.imageSha256 -or
            $seed.CopperOsStorage.imageSha256 -cne $verify.CopperOsStorage.imageSha256) { throw 'Native guest files/settings did not survive restart and MSIX update unchanged.' }
        $record.NativeCopperOsStorage = 'PASSED: native DOS create/write/read/rename/delete; process restart and package update readback.'
    } else { $record.NativeCopperOsStorage = 'UNAVAILABLE: qualified guest assets were not supplied.' }
    if ($MediaPersistence) {
        $recovery = Invoke-InstalledPhase $installed 'recovery'
        $recovered = Invoke-InstalledPhase $installed 'recovered'
        if (-not $recovery.CopperOsStorage.denyHostSaves -or -not $recovered.CopperOsStorage.reopen -or
            $recovery.CopperOsStorage.imageSha256 -cne $recovered.CopperOsStorage.imageSha256) { throw 'Denied-save export/import did not survive a native process restart.' }
        Assert-CopperScreenMediaUpdate $seed $recovery
        Assert-CopperScreenMediaUpdate $seed $recovered
    }
    Remove-AppxPackage -Package $installed.PackageFullName -ErrorAction Stop
    if (@(Get-AppxPackage -Name $ownedIdentity).Count) { throw 'Uninstall left the diagnostic package registered.' }
    if ((Get-FileHash -LiteralPath $customerFile -Algorithm SHA256).Hash -cne $customerHash) { throw 'Uninstall changed external user content.' }
    if ($MediaPersistence) {
        foreach ($map in @($recovered.MediaPersistence.Originals, $recovered.MediaPersistence.Exports)) {
            foreach ($file in $map.PSObject.Properties) {
                if (-not (Test-Path -LiteralPath $file.Name -PathType Leaf) -or
                    (Get-FileHash -LiteralPath $file.Name -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.Value) { throw 'Uninstall changed a protected original or exported backup.' }
            }
        }
    }
    $installed = Deploy-TestPackage $updatePath 'ReinstalledLayout'
    $fresh = Invoke-InstalledPhase $installed 'fresh'
    if ($MediaPersistence) {
        if (-not $fresh.MediaPersistence.PackagePrivateStateWasRemoved -or -not $fresh.MediaPersistence.NativeReadback -or
            $fresh.MediaPersistence.RestoredImageSha256 -cne $seed.CopperOsStorage.imageSha256) { throw 'Clean reinstall could not restore and read back the external workspace backup.' }
        $record.MediaPersistenceAcceptance = $DeploymentMode -eq 'Install'
    }
    $record.Status = 'PASSED'
    $record.InstalledMsixAcceptance = $DeploymentMode -eq 'Install'
    if ($LauncherLayout) { $record.LauncherLayoutAcceptance = $DeploymentMode -eq 'Install' }
}
catch {
    if ($record.Status -eq 'RUNNING') { $record.Status = 'FAILED' }
    $record.Error = $_.Exception.ToString()
    $failure = $_
}
finally {
    if ($ownedIdentity -and -not $KeepInstalled) {
        try {
            foreach ($package in @(Get-AppxPackage -Name $ownedIdentity)) {
                if ($package.Publisher -cne $baseline.Publisher) { throw 'Cleanup refused an unexpected publisher.' }
                Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
            }
            $record.Cleanup = 'Test registration removed; evidence/layouts retained.'
        }
        catch { $record.Cleanup = $_.Exception.ToString(); $record.Status = 'FAILED'; $failure = $_ }
    }
    $record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recordPath -Encoding utf8
}
Write-Output "Installed-test record: $recordPath"
if ($failure) { throw $failure }
[pscustomobject]@{ RecordPath = $recordPath; Status = $record.Status; InstalledMsixAcceptance = $record.InstalledMsixAcceptance }
