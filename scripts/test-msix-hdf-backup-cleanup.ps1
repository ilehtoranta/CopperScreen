[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$RdbFixturePath,
    [Parameter(Mandatory)][string]$InputBackupDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(60,1800)][int]$PhaseTimeoutSeconds = 900,
    [switch]$KeepInstalled
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'msix/CopperScreen.Msix.psm1') -Force
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$rom = (Resolve-Path -LiteralPath $RomPath).Path
$fixture = (Resolve-Path -LiteralPath $RdbFixturePath).Path
$identity = Get-CopperScreenMsixIdentity $package
if (!$identity.Diagnostic -or !$identity.Signed -or $identity.Name -notmatch '^CopperScreen\.Test\.[0-9a-f]{32}$') { throw 'Supply a new signed diagnostic MSIX identity.' }
if (@(Get-AppxPackage -Name $identity.Name).Count) { throw 'The diagnostic identity is already installed; existing installs are never replaced.' }
if ((Get-AuthenticodeSignature $package).Status -ne 'Valid') { throw 'The signed MSIX must already be trusted. This script never changes certificate trust.' }
$contract = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperStart.json') -Raw | ConvertFrom-Json
if ((Get-Item $rom).Length -ne $contract.bytes -or (Get-FileHash $rom).Hash -ine $contract.sha256) { throw 'Supply the current pinned raw ROM.' }
$fixtureHash = '000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368'
if ((Get-FileHash $fixture).Hash -cne $fixtureHash) { throw 'Supply the qualified original rdb-both-written.hdf. It is never modified.' }
$inputs = (Resolve-Path -LiteralPath $InputBackupDirectory).Path
$inputHashes = [ordered]@{}
foreach ($name in @('existing-backup.copperhdf')) { $inputHashes[$name]=(Get-FileHash -LiteralPath (Join-Path $inputs $name)).Hash }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a new or empty evidence directory.' }
$null = New-Item -ItemType Directory -Path $output -Force
$recordPath = Join-Path $output 'installed-hdf-backup-cleanup-record.json'
$record = [ordered]@{Protocol='copperscreen-installed-hdf-backup-cleanup-v1';Status='RUNNING';InstalledMsixAcceptance=$false;PackageSha256=(Get-FileHash $package).Hash;
    Identity=$identity;RomSha256=(Get-FileHash $rom).Hash;QualificationId=$contract.qualificationId;FixtureSha256=$fixtureHash;
    FullRomPhases=5;FullRomBoots=5;HostSemanticCases=0;CompiledMc68000Cases=0;Phases=@();StoreCertified=$false}
function Write-ProofJson([string]$path, $value) {
    $temporary = $path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
    try {
        [IO.File]::WriteAllText($temporary, ($value | ConvertTo-Json -Depth 40))
        for ($attempt=0; $attempt -lt 30; $attempt++) {
            try { [IO.File]::Move($temporary,$path,$true); return }
            catch [IO.IOException] { if ($attempt -eq 29) { throw }; Start-Sleep -Milliseconds 100 }
        }
    }
    finally { if (Test-Path -LiteralPath $temporary) { [IO.File]::Delete($temporary) } }
}
function Save-Record { Write-ProofJson $recordPath $record }
function Copy-ProofFile([string]$source, [string]$destination) {
    # A terminated process can still have filesystem teardown in flight.
    for ($attempt=0; $attempt -lt 100; $attempt++) {
        try { Copy-Item -LiteralPath $source -Destination $destination -ErrorAction Stop; return }
        catch [IO.IOException] { if ($attempt -eq 99) { throw }; Start-Sleep -Milliseconds 100 }
    }
}
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
function Invoke-RdbPhase([string]$stage, [string]$phase, $scenario = $null, [bool]$interrupt = $false) {
    $stageOutput = Join-Path $output $stage
    $null = New-Item -ItemType Directory -Path $stageOutput -Force
        $requestPath = Join-Path $stageOutput ($phase+'-request.json')
        @{RunId=$identity.Name.Substring(18);Phase=$phase;RomPath=$rom;FixturePath=$fixture;OutputDirectory=$stageOutput;Scenario=$scenario;FailureInputsDirectory=$inputs} | ConvertTo-Json | Set-Content $requestPath -Encoding utf8
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
        Write-ProofJson (Join-Path $output 'progress.json') @{ReinstallStage=$stage;Phase=$phase;Pid=$activated;PackageFullName=$installed.PackageFullName;StartedUtc=[datetime]::UtcNow.ToString('o')}
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $barrierPath = Join-Path $stageOutput ($phase+'-barrier.json')
        $terminatedAt = $null
        while (!$active.HasExited) {
            if ($interrupt -and (Test-Path -LiteralPath $barrierPath)) {
                $terminatedAt = Get-Content -LiteralPath $barrierPath -Raw | ConvertFrom-Json
                if ($terminatedAt.name -cne $phase -or $terminatedAt.bytes -lt 0) { throw 'Unexpected interruption barrier.' }
                $active.Kill(); $active.WaitForExit(10000) | Out-Null
                if (!$active.HasExited) { throw 'Owned process did not terminate.' }
                break
            }
            if ($clock.Elapsed.TotalSeconds -gt $PhaseTimeoutSeconds) { throw "Installed RDB phase $phase timed out." }
            Start-Sleep -Milliseconds 300
        }
        $exitCode = $active.ExitCode
        $result = Get-Content (Join-Path $stageOutput ($phase+'.json')) -Raw | ConvertFrom-Json
        $result | Add-Member -NotePropertyName HostProcess -NotePropertyValue @{Id=$activated;ExitCode=$exitCode;Executable=$expectedExe;ElapsedSeconds=$clock.Elapsed.TotalSeconds}
        $result | Add-Member -NotePropertyName ReinstallStage -NotePropertyValue $stage
        $record.Phases += $result
        Save-Record
        if (!$interrupt -and ($exitCode -ne 0 -or !$result.passed -or !$result.windowOpened -or $result.elevated -or $result.packageFullName -cne $installed.PackageFullName)) { throw "Installed phase $phase failed; inspect its retained report." }
        if ($interrupt) {
            if (!$terminatedAt -or $result.ioCases.Count -ne 0) { throw 'An expected barrier interruption was not proved.' }
            $result | Add-Member -NotePropertyName ForcedTermination -NotePropertyValue $true
            $result | Add-Member -NotePropertyName InterruptedAt -NotePropertyValue $terminatedAt
            Save-Record
            foreach ($file in $terminatedAt.protectedFiles.PSObject.Properties) {
                if ((Get-FileHash -LiteralPath (Join-Path $result.userDataDirectory $file.Name)).Hash -cne $file.Value) { throw 'Interrupted operation changed disk/settings.' }
            }
            if ((Get-FileHash -LiteralPath (Join-Path $inputs 'existing-backup.copperhdf')).Hash -cne $terminatedAt.backupHash) { throw 'Interrupted operation changed existing backup.' }
            $item=Get-Item -LiteralPath $terminatedAt.staged
            $result | Add-Member -NotePropertyName UnpublishedArtifacts -NotePropertyValue @(if ($item.PSIsContainer) { Get-ChildItem -LiteralPath $item.FullName -Recurse -File | ForEach-Object { @{Path=$_.FullName;Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} } } else { @{Path=$item.FullName;Bytes=$item.Length;Sha256=(Get-FileHash -LiteralPath $item.FullName).Hash} })
            $proofFiles=Join-Path $stageOutput 'abandoned-files'
            $proofRecords=Join-Path $stageOutput 'abandoned-records'
            $null=New-Item -ItemType Directory -Path $proofFiles,$proofRecords
            foreach ($artifact in $result.UnpublishedArtifacts) { Copy-ProofFile $artifact.Path (Join-Path $proofFiles ([IO.Path]::GetFileName($artifact.Path))) }
            $journalRoot=Join-Path $result.userDataDirectory 'HardDiskBackupOperations'
            $journals=@(Get-ChildItem -LiteralPath $journalRoot -Filter '*.journal' -File)
            if ($journals.Count -ne 1) { throw 'Expected exactly one interrupted ownership journal.' }
            Copy-ProofFile $journals[0].FullName $proofRecords
            $result | Add-Member -NotePropertyName OwnershipJournal -NotePropertyValue $journals[0].FullName
            $result | Add-Member -NotePropertyName OwnershipJournalSha256 -NotePropertyValue (Get-FileHash -LiteralPath $journals[0].FullName).Hash
            Save-Record
        }
        $expectedCleanup=if ($record.Phases.Count -eq 1) { 0 } else { 1 }
        if ($result.startupBackupCleanup.Cleaned -ne $expectedCleanup -or $result.startupBackupCleanup.Deferred -ne 0) { throw 'Startup did not clean exactly the prior interrupted operation.' }
        if ($record.Phases.Count -gt 1) {
            $previous=$record.Phases[-2]
            if ((Test-Path -LiteralPath $previous.InterruptedAt.staged) -or (Test-Path -LiteralPath $previous.OwnershipJournal)) { throw 'Prior temporary artifact or journal survived automatic startup cleanup.' }
        }
        if ($record.Contains('PreservationSentinels')) {
            foreach ($sentinel in $record.PreservationSentinels.Keys) { if ((Get-FileHash -LiteralPath $sentinel).Hash -cne $record.PreservationSentinels[$sentinel]) { throw 'Cleanup changed an existing published or untracked file.' } }
        }
        Save-Record
        $expectedPrivate = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Packages/'+$installed.PackageFamilyName+'/LocalState/CopperScreen/RdbProof/')
        if (!$result.userDataDirectory.StartsWith($expectedPrivate,[StringComparison]::OrdinalIgnoreCase)) { throw 'Proof did not use the installed package family LocalState.' }
        foreach ($binary in $result.binaries) {
            if (!$binary.path.StartsWith($installed.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $binary.path).Hash -cne $binary.sha256) { throw 'Loaded binary does not match installed immutable package.' }
        }
        if ((Get-FileHash $fixture).Hash -cne $fixtureHash -or (Get-FileHash $rom).Hash -ine $contract.sha256) { throw 'An original input changed.' }
        Write-Host "PASS installed cleanup $stage / $phase (PID $activated, exit $exitCode, expected interruption: $interrupt)"
        $active.Dispose(); $script:active = $null
}
try {
    Save-Record
    Add-AppxPackage -Path $package -ErrorAction Stop
    $owned = $true
    $installed = @(Get-AppxPackage -Name $identity.Name)
    if ($installed.Count -ne 1 -or $installed[0].IsDevelopmentMode -or $installed[0].Version -ne $identity.Version) { throw 'Expected a real signed baseline MSIX.' }
    $installed = $installed[0]
    $firstInstalled = $installed
    $record['FirstInstalledPackage'] = @{FullName=$installed.PackageFullName;Family=$installed.PackageFamilyName;Location=$installed.InstallLocation;IsDevelopmentMode=$installed.IsDevelopmentMode;Status=$installed.Status.ToString()}
    $script:privateRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Packages/'+$installed.PackageFamilyName+'/LocalState/CopperScreen/RdbProof/'+$identity.Name.Substring(18))
    $record['InputBackupDirectory'] = $inputs
    $record['InputBackupSha256'] = $inputHashes
    Save-Record
    foreach ($phase in @('io-export-partial','io-export-flushed','io-restore-partial','io-restore-flushed')) {
        Invoke-RdbPhase $phase $phase 'backup-cleanup' $true
        if ($phase -eq 'io-export-partial') {
            $data=$record.Phases[0].userDataDirectory
            $untracked=Join-Path $data ('HardDiskRestores/.restore-'+[guid]::NewGuid().ToString('N'))
            $published=Join-Path $data ('HardDiskRestores/'+[guid]::NewGuid().ToString('N'))
            $null=New-Item -ItemType Directory -Path $untracked,$published
            $lookalike=Join-Path $inputs ('io-export-partial/.hdf-backup-'+[guid]::NewGuid().ToString('N')+'.tmp')
            $sentinels=[ordered]@{}
            foreach ($sentinel in @($lookalike,(Join-Path $untracked 'keep.hdf'),(Join-Path $published 'unit-0.hdf'))) {
                Copy-Item -LiteralPath $fixture -Destination $sentinel
                $sentinels[$sentinel]=(Get-FileHash -LiteralPath $sentinel).Hash
            }
            $config=Join-Path $published 'profile.json'; Copy-Item -LiteralPath (Join-Path $data 'profile.json') -Destination $config
            $sentinels[$config]=(Get-FileHash -LiteralPath $config).Hash
            $record['PreservationSentinels']=$sentinels
            Save-Record
        }
    }
    Invoke-RdbPhase 'io-recovery' 'io-recovery' 'backup-cleanup'
    $baseline=$record.Phases[0].protectedFilesBefore
    foreach ($file in $baseline.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $record.Phases[-1].userDataDirectory $file.Name)).Hash -cne $file.Value) { throw 'Recovery changed existing disks/settings.' }
    }
    foreach ($key in $inputHashes.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $inputs $key)).Hash -cne $inputHashes[$key]) { throw 'Existing backup changed.' } }
    if ($record.Phases[-1].liveSha256 -cne $fixtureHash -or $record.Phases[-1].savedSha256AfterWindowClose -cne $fixtureHash) { throw 'Recovery did not preserve the bootable disk.' }
    $record['InstalledBackupCleanupAcceptance'] = $true
    $record['AutomaticStartupCleanups'] = 4
    $record['ExpectedForcedTerminations'] = 4
    $record['GuestFlushBeforeHostSaveOrDispose'] = $true
    $record.Status = 'PASS' 
    $record.InstalledMsixAcceptance = $true
}
catch { $record.Status='FAIL';$record['Error']=$_.Exception.ToString();throw }
finally {
  try {
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
  }
  catch { $record.Status='FAIL';$record['RetentionOrRemovalError']=$_.Exception.ToString();throw }
  finally { Save-Record }
}

