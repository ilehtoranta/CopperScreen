[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$RomPath,
    [Parameter(Mandatory)][string]$RdbFixturePath,
    [Parameter(Mandatory)][string]$InputBackupDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ResumeAfterExportEvidence,
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
$recordPath = Join-Path $output 'installed-hdf-backup-io-record.json'
$record = [ordered]@{Protocol='copperscreen-installed-hdf-backup-io-v1';Status='RUNNING';InstalledMsixAcceptance=$false;PackageSha256=(Get-FileHash $package).Hash;
    Identity=$identity;RomSha256=(Get-FileHash $rom).Hash;QualificationId=$contract.qualificationId;FixtureSha256=$fixtureHash;
    FullRomPhases=6;FullRomBoots=7;HostSemanticCases=0;CompiledMc68000Cases=0;Phases=@();StoreCertified=$false}
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
            foreach ($file in $terminatedAt.protectedFiles.PSObject.Properties) {
                if ((Get-FileHash -LiteralPath (Join-Path $result.userDataDirectory $file.Name)).Hash -cne $file.Value) { throw 'Interrupted operation changed disk/settings.' }
            }
            if ((Get-FileHash -LiteralPath (Join-Path $inputs 'existing-backup.copperhdf')).Hash -cne $terminatedAt.backupHash) { throw 'Interrupted operation changed existing backup.' }
            $item=Get-Item -LiteralPath $terminatedAt.staged
            $result | Add-Member -NotePropertyName UnpublishedArtifacts -NotePropertyValue @(if ($item.PSIsContainer) { Get-ChildItem -LiteralPath $item.FullName -Recurse -File | ForEach-Object { @{Path=$_.FullName;Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} } } else { @{Path=$item.FullName;Bytes=$item.Length;Sha256=(Get-FileHash -LiteralPath $item.FullName).Hash} })
            Save-Record
        }
        $expectedPrivate = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Packages/'+$installed.PackageFamilyName+'/LocalState/CopperScreen/RdbProof/')
        if (!$result.userDataDirectory.StartsWith($expectedPrivate,[StringComparison]::OrdinalIgnoreCase)) { throw 'Proof did not use the installed package family LocalState.' }
        foreach ($binary in $result.binaries) {
            if (!$binary.path.StartsWith($installed.InstallLocation+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $binary.path).Hash -cne $binary.sha256) { throw 'Loaded binary does not match installed immutable package.' }
        }
        if ((Get-FileHash $fixture).Hash -cne $fixtureHash -or (Get-FileHash $rom).Hash -ine $contract.sha256) { throw 'An original input changed.' }
        Write-Host "PASS installed RDB $stage / $phase (PID $activated, exit $exitCode, expected interruption: $interrupt)"
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
    if ($ResumeAfterExportEvidence) {
        $priorRoot=(Resolve-Path -LiteralPath $ResumeAfterExportEvidence).Path
        $prior=Get-Content -LiteralPath (Join-Path $priorRoot 'installed-hdf-backup-io-record.json') -Raw | ConvertFrom-Json
        if ($prior.Status -ne 'FAIL' -or !$prior.OwnedRegistrationRemoved -or $prior.PackageSha256 -cne $record.PackageSha256 -or $prior.Phases.Count -ne 3 -or $prior.Error -notlike '*progress.json*') { throw 'Resume requires the exact retained progress-write failure after both export interruptions.' }
        if (!$prior.Phases[0].allIoFailuresPassed -or !$prior.Phases[0].postIoFullRomReadback -or !$prior.Phases[1].ForcedTermination -or !$prior.Phases[2].ForcedTermination) { throw 'Prior completed checks are missing.' }
        foreach ($item in $prior.Phases) { Copy-Item -LiteralPath (Join-Path $priorRoot $item.phase) -Destination (Join-Path $output $item.phase) -Recurse }
        $null=New-Item -ItemType Directory -Path (Split-Path $script:privateRoot) -Force
        if (Test-Path -LiteralPath $script:privateRoot) { throw 'Never overwrite an existing machine during resume.' }
        Copy-Item -LiteralPath (Join-Path $priorRoot 'package-private-data') -Destination $script:privateRoot -Recurse
        $record.Phases=@($prior.Phases)
        $record['ResumedAfterControllerProgressWriteFailure']=$priorRoot
        Save-Record
    }
    else { Invoke-RdbPhase 'io-failures' 'io-failures' 'backup-io' }
    $result=$record.Phases[0]
    if (!$result.allIoFailuresPassed -or !$result.postIoFullRomReadback -or $result.ioCases.Count -ne 5) { throw 'All five graceful I/O failures and fresh native readback are required.' }
    $interruptions=if ($ResumeAfterExportEvidence) { @('io-restore-partial','io-restore-flushed') } else { @('io-export-partial','io-export-flushed','io-restore-partial','io-restore-flushed') }
    foreach ($phase in $interruptions) {
        Invoke-RdbPhase $phase $phase 'backup-io' $true
    }
    Invoke-RdbPhase 'io-recovery' 'io-recovery' 'backup-io'
    $baseline=$record.Phases[0].protectedFilesBefore
    foreach ($file in $baseline.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $record.Phases[-1].userDataDirectory $file.Name)).Hash -cne $file.Value) { throw 'Recovery changed existing disks/settings.' }
    }
    foreach ($key in $inputHashes.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $inputs $key)).Hash -cne $inputHashes[$key]) { throw 'Existing backup changed.' } }
    if ($record.Phases[-1].liveSha256 -cne $fixtureHash -or $record.Phases[-1].savedSha256AfterWindowClose -cne $fixtureHash) { throw 'Recovery did not preserve the bootable disk.' }
    $record['InstalledBackupIoAcceptance'] = $true
    $record['DiskFullIsDiagnosticFaultInjection'] = $true
    $record['ActualWindowsAccessDenied'] = $true
    $record['ExpectedForcedTerminations'] = 4
    $record['GuestFlushBeforeHostSaveOrDispose'] = $true
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

