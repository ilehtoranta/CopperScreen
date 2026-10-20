[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$MediaPath,
    [Parameter(Mandatory)][string]$InputScriptPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(10,3600)][int]$DurationSeconds = 3600,
    [ValidateSet('copperstart','copperos')][string[]]$Cases = @('copperstart','copperos'),
    [switch]$Smoke,
    [switch]$CaptureProbe
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'msix/CopperScreen.Msix.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.Soak.psm1') -Force
if(!$IsWindows){throw 'Installed soak requires Windows'}
if(!$Smoke -and $DurationSeconds -ne 3600){throw 'Acceptance requires a full 3600 seconds per session; shorter runs need -Smoke and cannot pass acceptance'}
if($CaptureProbe -and !$Smoke){throw 'Capture-layer experiments require -Smoke and do not qualify installed hours'}
if(!$Smoke -and ($Cases.Count -ne 2 -or @($Cases|Sort-Object -Unique).Count -ne 2)){throw 'Full acceptance requires both sequential cases'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count){throw 'Use a new evidence directory'}
New-Item -ItemType Directory -Path $output -Force|Out-Null
$package=(Resolve-Path -LiteralPath $PackagePath).Path
$media=(Resolve-Path -LiteralPath $MediaPath).Path
$scriptPath=(Resolve-Path -LiteralPath $InputScriptPath).Path
$identity=Get-CopperScreenMsixIdentity $package
Assert-CopperScreenIdentity $identity.Name $identity.Publisher $identity.Version -InstalledTestBuild
if(!$identity.Signed -or !$identity.Diagnostic){throw 'A signed diagnostic package is required'}
if(@(Get-AppxPackage -Name $identity.Name).Count){throw 'Refuse pre-existing registration'}
$run=$identity.Name.Substring('CopperScreen.Test.'.Length)
$original=(Get-FileHash -LiteralPath $media).Hash
if($original -cne '82DC1FAF56823653CD3B0B299E179932C1FAE117B03CAE220E121D96AE8B3A06'){throw 'Exact qualified Lotus II ADF required'}
if((Get-FileHash -LiteralPath $scriptPath).Hash -cne 'B54D47135BE66B7949A3C5FC10DAAD342801789570CE27799C6ED7CB15768FD3'){throw 'Exact input script required'}
$record=[ordered]@{Protocol='copperscreen-installed-soak-v1';Status='RUNNING';Acceptance=$false;Smoke=$Smoke.IsPresent;DurationSeconds=$DurationSeconds;ReleaseBudgets='OPEN';Engine='Lightweight';Legacy=$false;Package=$package;PackageSha256=(Get-FileHash $package).Hash;Identity=$identity;Phases=@()}
$recordPath=Join-Path $output 'soak-record.json'
function Record {$record|ConvertTo-Json -Depth 22|Set-Content $recordPath}
if(-not ('CopperScreen.SoakActivation' -as [type])){
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace CopperScreen {
 public static class SoakActivation {
  [ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IActivation {
   [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string id,[MarshalAs(UnmanagedType.LPWStr)]string args,uint options,out uint pid);
   [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,[MarshalAs(UnmanagedType.LPWStr)]string verb,out uint pid);
   [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,out uint pid);
  }
  public static uint Activate(string id,string args) {
   var manager=(IActivation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
   try {uint pid;Marshal.ThrowExceptionForHR(manager.ActivateApplication(id,args,2|4,out pid));return pid;}
   finally {Marshal.ReleaseComObject(manager);}
  }
  [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
 }
}
'@
}
$owns=$false
$awake=$false
try{
 Record
 if([CopperScreen.SoakActivation]::SetThreadExecutionState([uint32]2147483651) -eq 0){throw 'Cannot establish a scoped system/display wake request'}
 $awake=$true; $record['ScopedWakeRequest']='System and display required only while this controller runs; no power-plan changes.'
 $owns=$true; Add-AppxPackage -Path $package -ErrorAction Stop
 $installed=@(Get-AppxPackage -Name $identity.Name)
 if($installed.Count -ne 1 -or $installed[0].IsDevelopmentMode -or $installed[0].Version -ne $identity.Version){throw 'Real installation required'}
 $installed=$installed[0]
 foreach($case in $Cases){
  foreach($phase in @('soak','reopen')){
   $requestPath=Join-Path $output ($case+'-'+$phase+'-request.json')
   @{RunId=$run;Case=$case;Phase=$phase;DurationSeconds=$(if($phase -eq 'soak'){$DurationSeconds}else{10});OutputDirectory=$output;Media=$media;InputScript=$scriptPath;CaptureProbe=$CaptureProbe.IsPresent}|ConvertTo-Json|Set-Content $requestPath
   $reportPath=Join-Path $output ($case+'-'+$phase+'.json')
   $activated=[CopperScreen.SoakActivation]::Activate(($installed.PackageFamilyName+'!CopperScreen'),('--installed-soak "'+$requestPath+'"'))
   $process=Get-Process -Id $activated; $null=$process.Handle
   $timer=[Diagnostics.Stopwatch]::StartNew()
   try{
    while(!(Test-Path -LiteralPath $reportPath)){
     if($process.HasExited){throw 'Activated app exited without a report'}
     if($timer.Elapsed.TotalSeconds -gt $DurationSeconds+1800){$process.Kill();throw 'Soak phase timed out'}
     $process.Refresh()
     @{Utc=[DateTime]::UtcNow;Case=$case;Phase=$phase;ElapsedSeconds=$timer.Elapsed.TotalSeconds;Pid=$activated;WorkingSet=$process.WorkingSet64;PrivateBytes=$process.PrivateMemorySize64;Responding=$process.Responding}|ConvertTo-Json -Compress|Add-Content (Join-Path $output 'external-observer.jsonl')
     Start-Sleep -Seconds 5
    }
    $result=Get-Content $reportPath -Raw|ConvertFrom-Json
    try {$exit=Wait-CopperScreenProcessExit $process -TimeoutSeconds 60}
    catch {
     $result|Add-Member -NotePropertyName HostProcess -NotePropertyValue @{Id=$activated;ExitCode=$(if($process.HasExited){$process.ExitCode}else{$null});Error=$_.Exception.Message}
     $record.Phases+=,$result;Record;throw
    }
    $result|Add-Member -NotePropertyName HostProcess -NotePropertyValue $exit
    $record.Phases+=,$result;Record
    if(!$result.passed -or !$result.windowOpened -or $result.elevated -or $result.packageFullName -cne $installed.PackageFullName -or $result.elapsedSeconds -lt $(if($phase -eq 'soak'){$DurationSeconds}else{10})){throw ('Installed soak failed; inspect '+$reportPath)}
    if(!$result.userDataDirectory.StartsWith((Join-Path $env:LOCALAPPDATA ('Packages/'+$installed.PackageFamilyName+'/LocalState/CopperScreen/Soaks/'+$run)),[StringComparison]::OrdinalIgnoreCase)){throw 'Soak used the wrong private data root'}
    $trace=Assert-CopperScreenSoakTrace -Path (Join-Path $output ($case+'-'+$phase+'-samples.jsonl')) -Seconds $(if($phase -eq 'soak'){$DurationSeconds}else{10}) -Smoke:($Smoke -or $phase -eq 'reopen')
    $result|Add-Member -NotePropertyName TraceSummary -NotePropertyValue $trace
    Record
   } finally {if(!$process.HasExited){$process.Kill()};$process.Dispose()}
  }
  $a=$record.Phases[-2];$b=$record.Phases[-1]
  if($a.diskSha256 -cne $b.diskSha256){throw 'Saved media changed across process restart'}
  foreach($file in $a.files.PSObject.Properties){if($b.files.PSObject.Properties[$file.Name].Value -cne $file.Value){throw 'Saved profile/settings changed across process restart'}}
 }
 if((Get-FileHash -LiteralPath $media).Hash -cne $original){throw 'Original game media changed'}
 $record.Status='PASSED';$record.Acceptance=!$Smoke
}catch{$record.Status='FAILED';$record['Error']=$_.Exception.ToString();throw}
finally{
 if($awake){$null=[CopperScreen.SoakActivation]::SetThreadExecutionState([uint32]2147483648)}
 if($owns){
  foreach($p in @(Get-AppxPackage -Name $identity.Name)){
   if($p.Publisher -cne $identity.Publisher){throw 'Unexpected publisher; cleanup refused'}
   # Retain only this diagnostic run's logs/settings outside package-private storage.
   $private=Join-Path $env:LOCALAPPDATA ('Packages/'+$p.PackageFamilyName+'/LocalState/CopperScreen/Soaks/'+$run)
   if(Test-Path -LiteralPath $private){Copy-Item -LiteralPath $private -Destination (Join-Path $output 'PrivateState') -Recurse}
   Remove-AppxPackage -Package $p.PackageFullName -ErrorAction Stop
  }
  $record['Cleanup']='Owned test registration removed; private diagnostic state, originals and exports retained in evidence.'
 }
 Record
}

