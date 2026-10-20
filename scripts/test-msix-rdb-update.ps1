[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselinePackage,
    [Parameter(Mandatory)][string]$UpdatePackage,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$RdbFixturePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(60,1800)][int]$PhaseTimeoutSeconds = 900,
    [switch]$KeepInstalled
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'msix/CopperScreen.Msix.psm1') -Force
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$package = (Resolve-Path -LiteralPath $BaselinePackage).Path
$update = (Resolve-Path -LiteralPath $UpdatePackage).Path
$updateIdentity = Get-CopperScreenMsixIdentity $update
$rom = (Resolve-Path -LiteralPath $RomPath).Path
$fixture = (Resolve-Path -LiteralPath $RdbFixturePath).Path
$identity = Get-CopperScreenMsixIdentity $package
if (!$identity.Diagnostic -or !$identity.Signed -or $identity.Name -notmatch '^CopperScreen\.Test\.[0-9a-f]{32}$') { throw 'Supply a new signed diagnostic MSIX identity.' }
if (@(Get-AppxPackage -Name $identity.Name).Count) { throw 'The diagnostic identity is already installed; existing installs are never replaced.' }
if ((Get-AuthenticodeSignature $package).Status -ne 'Valid') { throw 'The signed MSIX must already be trusted. This script never changes certificate trust.' }
if (!$updateIdentity.Diagnostic -or !$updateIdentity.Signed -or $updateIdentity.Name -cne $identity.Name -or
    $updateIdentity.Publisher -cne $identity.Publisher -or $updateIdentity.Architecture -cne $identity.Architecture -or
    $updateIdentity.Version -le $identity.Version) { throw 'Update must be a newer signed diagnostic package with the same identity, publisher and architecture.' }
if ((Get-AuthenticodeSignature $update).Status -ne 'Valid') { throw 'The signed update must already be trusted.' }

$contract = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperStart.json') -Raw | ConvertFrom-Json
if ((Get-Item $rom).Length -ne $contract.bytes -or (Get-FileHash $rom).Hash -ine $contract.sha256) { throw 'Supply the current pinned raw ROM.' }
$fixtureHash = '000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368'
if ((Get-FileHash $fixture).Hash -cne $fixtureHash) { throw 'Supply the qualified original rdb-both-written.hdf. It is never modified.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a new or empty evidence directory.' }
$null = New-Item -ItemType Directory -Path $output -Force
$recordPath = Join-Path $output 'installed-rdb-update-record.json'
$record = [ordered]@{Protocol='copperscreen-installed-rdb-update-v1';Status='RUNNING';InstalledMsixAcceptance=$false;PackageSha256=(Get-FileHash $package).Hash;
    Identity=$identity;RomSha256=(Get-FileHash $rom).Hash;QualificationId=$contract.qualificationId;FixtureSha256=$fixtureHash;
    FullRomPhases=15;ResetRomBoots=17;UpdatePackageSha256=(Get-FileHash $update).Hash;UpdateIdentity=$updateIdentity;HostSemanticCases=0;CompiledMc68000Cases=0;Phases=@();StoreCertified=$false}
function Save-Record { $record | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $recordPath -Encoding utf8 }
if (!('CopperScreen.RdbActivation' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace CopperScreen {
 public static class RdbActivation {
  [ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IActivation {
   [PreserveSig]int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string id,[MarshalAs(UnmanagedType.LPWStr)]string arguments,uint options,out uint pid);
   [PreserveSig]int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,[MarshalAs(UnmanagedType.LPWStr)]string verb,out uint pid);
   [PreserveSig]int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,out uint pid);
  }
  public static uint Activate(string id,string arguments) {
   var manager=(IActivation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
   try { uint pid;Marshal.ThrowExceptionForHR(manager.ActivateApplication(id,arguments,6,out pid));return pid; }
   finally { Marshal.ReleaseComObject(manager); }
  }
 }
}
'@
}
$owned = $false
$active = $null
function Invoke-RdbPhase([string]$stage, [string]$phase, $scenario = $null) {
    $stageOutput = Join-Path $output $stage
    $null = New-Item -ItemType Directory -Path $stageOutput -Force
        $requestPath = Join-Path $stageOutput ($phase+'-request.json')
        @{RunId=$identity.Name.Substring(18);Phase=$phase;RomPath=$rom;FixturePath=$fixture;OutputDirectory=$stageOutput;Scenario=$scenario} | ConvertTo-Json | Set-Content $requestPath -Encoding utf8
        $activated = [CopperScreen.RdbActivation]::Activate(($installed.PackageFamilyName+'!CopperScreen'),('--installed-rdb "'+$requestPath+'"'))
        $script:active = Get-Process -Id $activated -ErrorAction Stop
        $null = $active.Handle
        $expectedExe = Join-Path $installed.InstallLocation 'CopperScreen.exe'
        $pathClock = [Diagnostics.Stopwatch]::StartNew()
        $actualExe = $active.Path
        while ([string]::IsNullOrEmpty($actualExe) -and !$active.HasExited -and $pathClock.Elapsed.TotalSeconds -lt 10) {
            Start-Sleep -Milliseconds 100
            $active.Refresh()
            $actualExe = (Get-Process -Id $activated -ErrorAction Stop).Path
        }
        if (![string]::Equals($actualExe,$expectedExe,[StringComparison]::OrdinalIgnoreCase)) {
            $record['FailedProcessPathObservation'] = @{Stage=$stage;Phase=$phase;Pid=$activated;Actual=$actualExe;Expected=$expectedExe;Exited=$active.HasExited}
            throw 'Activated process is not running from this WindowsApps package.'
        }
        @{UpdateStage=$stage;Phase=$phase;Pid=$activated;PackageFullName=$installed.PackageFullName;StartedUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $output 'progress.json') -Encoding utf8
        $clock = [Diagnostics.Stopwatch]::StartNew()
        while (!$active.HasExited) {
            if ($clock.Elapsed.TotalSeconds -gt $PhaseTimeoutSeconds) { throw "Installed RDB phase $phase timed out." }
            Start-Sleep -Milliseconds 300
        }
        $exitCode = $active.ExitCode
        $result = Get-Content (Join-Path $stageOutput ($phase+'.json')) -Raw | ConvertFrom-Json
        $result | Add-Member -NotePropertyName HostProcess -NotePropertyValue @{Id=$activated;ExitCode=$exitCode;Executable=$expectedExe;ElapsedSeconds=$clock.Elapsed.TotalSeconds}
        $result | Add-Member -NotePropertyName UpdateStage -NotePropertyValue $stage
        $record.Phases += $result
        Save-Record
        if ($exitCode -ne 0 -or !$result.passed -or !$result.windowOpened -or $result.elevated -or $result.packageFullName -cne $installed.PackageFullName) { throw "Installed phase $phase failed; inspect its retained report." }
        $expectedPrivate = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Packages/'+$installed.PackageFamilyName+'/LocalState/CopperScreen/RdbProof/')
        if (!$result.userDataDirectory.StartsWith($expectedPrivate,[StringComparison]::OrdinalIgnoreCase)) { throw 'Proof did not use the installed package family LocalState.' }
        foreach ($binary in $result.binaries) {
            if (!$binary.path.StartsWith($installed.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $binary.path).Hash -cne $binary.sha256) { throw 'Loaded binary does not match installed immutable package.' }
        }
        if ((Get-FileHash $fixture).Hash -cne $fixtureHash -or (Get-FileHash $rom).Hash -ine $contract.sha256) { throw 'An original input changed.' }
        Write-Host "PASS installed RDB $stage / $phase (PID $activated, exit 0)"
        $active.Dispose(); $script:active = $null
}
function Read-PersistedState {
    $state = [ordered]@{}
    foreach ($group in @('default','priority')) {
        foreach ($name in @('disk.hdf','profile.json','last-machine.json','recent-session.json')) {
            $relative = $group+'/'+$name
            $path = Join-Path $script:privateRoot $relative
            if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing persisted file: $relative" }
            $state[$relative] = (Get-FileHash -LiteralPath $path).Hash
        }
    }
    return $state
}
function Assert-PersistedState($before, $after) {
    if ($before.Count -ne $after.Count) { throw 'Update changed the persisted file set.' }
    foreach ($key in $before.Keys) { if ($before[$key] -cne $after[$key]) { throw "Update changed persisted bytes: $key" } }
}
try {
    Save-Record
    Add-AppxPackage -Path $package -ErrorAction Stop
    $owned = $true
    $installed = @(Get-AppxPackage -Name $identity.Name)
    if ($installed.Count -ne 1 -or $installed[0].IsDevelopmentMode -or $installed[0].Version -ne $identity.Version) { throw 'Expected a real signed baseline MSIX.' }
    $installed = $installed[0]
    $baselineInstalled = $installed
    $record['BaselineInstalledPackage'] = @{FullName=$installed.PackageFullName;Family=$installed.PackageFamilyName;Location=$installed.InstallLocation;IsDevelopmentMode=$installed.IsDevelopmentMode;Status=$installed.Status.ToString()}
    $script:privateRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Packages/'+$installed.PackageFamilyName+'/LocalState/CopperScreen/RdbProof/'+$identity.Name.Substring(18))
    foreach ($phase in @('write','restart','priority-write','priority-restart')) { Invoke-RdbPhase 'baseline' $phase }
    $before = Read-PersistedState
    $record['BeforeUpdateFiles'] = $before
    Copy-Item -LiteralPath $privateRoot -Destination (Join-Path $output 'before-update-private-data') -Recurse
    Save-Record
    # All observed app processes have exited with code zero. Let deployment
    # release any remaining package activation resources as part of the update.
    # There is no removal/re-registration or host rewrite of persisted data.
    $record['PackageSpecificShutdownDuringUpdate'] = $true
    Add-AppxPackage -Path $update -ForceApplicationShutdown -ErrorAction Stop
    $installed = @(Get-AppxPackage -Name $identity.Name)
    if ($installed.Count -ne 1 -or $installed[0].IsDevelopmentMode -or $installed[0].Version -ne $updateIdentity.Version) { throw 'Expected the newer real installed package.' }
    $installed = $installed[0]
    if ($installed.PackageFamilyName -cne $baselineInstalled.PackageFamilyName -or $installed.InstallLocation -ceq $baselineInstalled.InstallLocation) { throw 'Update must retain family and change the versioned install directory.' }
    $record['UpdatedInstalledPackage'] = @{FullName=$installed.PackageFullName;Family=$installed.PackageFamilyName;Location=$installed.InstallLocation;IsDevelopmentMode=$installed.IsDevelopmentMode;Status=$installed.Status.ToString()}
    $after = Read-PersistedState
    Assert-PersistedState $before $after
    $record['AfterUpdateBeforeActivationFiles'] = $after
    $record['UpdatePreservedAllPersistedBytesBeforeActivation'] = $true
    Save-Record
    foreach ($phase in @('restart','priority-restart')) { Invoke-RdbPhase 'updated-preserved' $phase }
    Assert-PersistedState $before (Read-PersistedState)
    # Fresh boots copy the baseline's pre-save exports into newly owned protected
    # machines; no host regeneration of saved guest files is allowed.
    foreach ($name in @('write-live.hdf','priority-write-live.hdf')) {
        Copy-Item -LiteralPath (Join-Path $output ('baseline/'+$name)) -Destination (Join-Path $output ('updated-preserved/'+$name))
    }
    foreach ($phase in @('fresh','priority-fresh')) { Invoke-RdbPhase 'updated-preserved' $phase }
    Assert-PersistedState $before (Read-PersistedState)
    $record['AfterUpdatedReadbackFiles'] = Read-PersistedState
    # Independently exercise new native writes and flushes in the updated app,
    # while preserving the two migrated baseline images unchanged.
    foreach ($phase in @('write','restart','fresh','priority-write','priority-restart','priority-fresh','protected')) { Invoke-RdbPhase 'updated-writes' $phase 'post-update' }
    $expectedDefault = $fixtureHash
    $expectedPriority = '10755D5522B1FF637DD937C6EA3E5D4B10B6A157DAB43B4A23190D59B1085617'
    if ($record.Phases.Count -ne 15) { throw 'All fifteen installed phases are required.' }
    foreach ($result in $record.Phases) {
        $phase = $result.phase
        if ($phase -ceq 'protected') { continue }
        $expected = if ($phase.StartsWith('priority-')) {$expectedPriority} else {$expectedDefault}
        if ($result.liveSha256 -cne $expected -or $result.savedSha256AfterWindowClose -cne $expected -or !$result.guestFlushBeforeHostSave -or $result.hostSaveAdvancedCycles) { throw "Installed saved bytes/flush evidence failed for $phase" }
        foreach ($observation in $result.observations) {
            $sys = if ($phase.StartsWith('priority-')) {1} else {2}
            if ($observation.sysIdentity -ne $sys -or $observation.sha256 -cne $expected) { throw "Wrong SYS selection/backing for $phase" }
        }
    }
    Assert-PersistedState $before (Read-PersistedState)
    $record['BaselineImagesAndSettingsUnchangedThroughEntireUpdateRun'] = $true
    $record['InstalledUpdateAcceptance'] = $true
    $record['GuestFlushBeforeHostSaveOrDispose'] = $true
    $record['BothBootSelections'] = $true
    $record['SeparateProcessRestarts'] = 6
    $record['FreshProtectedBoots'] = 4
    $record['ProtectedMutationControls'] = 1
    $record.Status = 'PASS'
    $record.InstalledMsixAcceptance = $true
}
catch { $record.Status='FAIL';$record['Error']=$_.Exception.ToString();throw }
finally {
    if ($active -and !$active.HasExited) { $active.Kill();$active.WaitForExit(10000) | Out-Null }
    if ($owned -and !$KeepInstalled) {
        $registered = @(Get-AppxPackage -Name $identity.Name)
        foreach ($item in $registered) {
            $private = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Packages/'+$item.PackageFamilyName+'/LocalState/CopperScreen/RdbProof/'+$identity.Name.Substring(18))
            if (Test-Path -LiteralPath $private) {
                Copy-Item -LiteralPath $private -Destination (Join-Path $output 'package-private-data') -Recurse
                $record['PrivateDataRetained'] = $true
            }
            Remove-AppxPackage -Package $item.PackageFullName -ErrorAction Stop
        }
        $record['OwnedRegistrationRemoved'] = @(Get-AppxPackage -Name $identity.Name).Count -eq 0
    }
    Save-Record
}
