[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath,[Parameter(Mandatory)][string]$RunDirectory,
    [ValidateSet('copperstart','copperos')][string[]]$Cases=@('copperstart','copperos'),[switch]$SkipSleep,
    [switch]$WaitForCopperStartSound,[switch]$NativeControlsOnly,[switch]$DisplayRecoveryOnly,
    [ValidateRange(8,600)][int]$RefillObservationSeconds=8,[switch]$AudioTimeline)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'msix/CopperScreen.Msix.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'qualification/CopperScreen.DisplayRecovery.psm1') -Force
if($NativeControlsOnly -and $DisplayRecoveryOnly){throw 'Choose one bounded recovery mode'}
Initialize-CopperScreenDisplayRecovery
$root=(Resolve-Path -LiteralPath $RunDirectory).Path
$config=Get-Content (Join-Path $root 'config.json') -Raw|ConvertFrom-Json
$package=(Resolve-Path -LiteralPath $PackagePath).Path
$identity=Get-CopperScreenMsixIdentity $package
if(!$identity.Signed -or !$identity.Diagnostic -or $identity.Name -cne $config.Identity){throw 'Matching signed diagnostic package required'}
if(@(Get-AppxPackage -Name $identity.Name).Count){throw 'Never replace a pre-existing package registration'}
$output=Join-Path $root 'Installed';if(Test-Path -LiteralPath $output){throw 'Use a new installed recovery output directory'}
New-Item -ItemType Directory -Path $output|Out-Null
$record=[ordered]@{Protocol='copperscreen-installed-recovery-v4';Status='RUNNING';Engine='Lightweight';Legacy=$false;ReleaseBudgets='OPEN';Package=$package;PackageSha256=(Get-FileHash $package).Hash;Identity=$identity;RequestedCases=$Cases;SleepRequested=(!$SkipSleep -and !$NativeControlsOnly -and !$DisplayRecoveryOnly);SleepTested=$false;PhysicalDeviceRequested=(!$NativeControlsOnly -and !$DisplayRecoveryOnly);PhysicalDeviceTested=$false;NativeControlsOnly=[bool]$NativeControlsOnly;DisplayRecoveryOnly=[bool]$DisplayRecoveryOnly;DisplayRequested=[bool]$DisplayRecoveryOnly;DisplayTested=$false;FullMatrixRequested=(!$NativeControlsOnly -and !$DisplayRecoveryOnly -and !$SkipSleep -and @($Cases|Sort-Object -Unique).Count -eq 2);WaitForCopperStartSound=[bool]$WaitForCopperStartSound;Cases=@();PhysicalAudibility='Separate owner observation; software counters do not establish physical sound';StoreCertified=$false}
function Write-Json($path,$value){$temporary=$path+'.tmp';$value|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $temporary;[IO.File]::Move($temporary,$path,$true)}
function Save-Record {Write-Json (Join-Path $root 'recovery-record.json') $record}
$record['RefillObservationSeconds']=$RefillObservationSeconds
$record['AudioTimelineRequested']=[bool]$AudioTimeline
function Assert-AudioTimeline([string]$phase){
 if(!$AudioTimeline){return}
 $path=Join-Path $output ($script:case+'-'+$phase+'-audio-timeline.json')
 if(!(Test-Path -LiteralPath $path)){throw 'Requested bounded audio timeline is missing'}
 $trace=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
 if(!$trace.nativeStopped -or $trace.timeline.protocol -cne 'copperscreen-audio-owner-timeline-v1' -or $trace.timeline.owner.incomplete -or $trace.timeline.callbacks.incomplete){throw 'Audio timeline snapshot was not completed'}
}
$script:hostId=0;$script:appId=0;$script:process=$null
function Wait-File($path,[int]$seconds=60){$clock=[Diagnostics.Stopwatch]::StartNew();while(!(Test-Path -LiteralPath $path)){if($script:process -and $script:process.HasExited){throw 'Installed app exited before completing its recovery phase'};if($clock.Elapsed.TotalSeconds -gt $seconds){throw ('Timeout waiting for '+$path)};Start-Sleep -Milliseconds 200};Get-Content -LiteralPath $path -Raw|ConvertFrom-Json -DateKind String}
function App-Command([string]$action){$script:appId++;Write-Json (Join-Path $output ($script:case+'-recovery-command.json')) @{Id=$script:appId;Action=$action};$reply=Wait-File (Join-Path $output ($script:case+'-recovery-reply-'+$script:appId+'.json')) 20;if(!$reply.passed -or $reply.id -ne $script:appId){throw 'App command failed'};return $reply.snapshot}
function Host-Start([string]$action){$script:hostId++;Write-Json (Join-Path $root 'Host/command.json') @{Id=$script:hostId;Action=$action};return $script:hostId}
function Host-Finish([int]$id){$reply=Wait-File (Join-Path $root ('Host/reply-'+$id+'.json')) 180;if(!$reply.Passed){throw ('Host action failed: '+$reply.Error)};return $reply}
function Request-DisplayWake {
 if(-not ('CopperScreenRecoveryDisplay' -as [type])){Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class CopperScreenRecoveryDisplay {
 [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
}
'@}
 $utc=[datetime]::UtcNow.ToString('o')
 $previous=[CopperScreenRecoveryDisplay]::SetThreadExecutionState(2)
 if(!$previous){throw 'Post-resume display-on request failed'}
 Start-Sleep -Seconds 3
 return @{Utc=$utc;Api='SetThreadExecutionState';Flags='ES_DISPLAY_REQUIRED (one-shot)';PreviousState=$previous;SettleSeconds=3;PowerPlanChanged=$false;PanelStateOrUnlockConfirmed=$false}
}
function Gate([string]$step){Write-Json (Join-Path $root 'progress.json') @{Stage='WAITING';Case=$script:case;Step=$step;Gate=(Join-Path $root ('go-'+$script:case+'-'+$step+'.json'));Utc=[datetime]::UtcNow.ToString('o')};$null=Wait-File (Join-Path $root ('go-'+$script:case+'-'+$step+'.json')) 600}
function Active-Window([string]$label,[int]$seconds=8){$a=App-Command 'snapshot';Start-Sleep -Seconds $seconds;$b=App-Command 'snapshot';if($b.state.IsPaused -or $b.state.FaultMessage -or $b.state.FrameNumber -le $a.state.FrameNumber){throw ('Guest did not run after '+$label)};if($b.audio.output.queue.callbacks -le $a.audio.output.queue.callbacks){throw ('Audio callbacks did not recover after '+$label)};if($script:case -ceq 'copperstart' -and $b.audio.output.queue.nonzeroCallbacks -le $a.audio.output.queue.nonzeroCallbacks){throw ('Audible PCM callbacks did not recover after '+$label)};if($b.audio.output.deviceState -cne 'ma_device_state_started' -or $b.audio.output.callbackAgeMilliseconds -ge 1000){throw ('Native output is not healthy after '+$label)};return @{Label=$label;Before=$a;After=$b;FrameDelta=$b.state.FrameNumber-$a.state.FrameNumber;CallbackDelta=$b.audio.output.queue.callbacks-$a.audio.output.queue.callbacks;MissingSampleDelta=$b.audio.output.queue.missingSamples-$a.audio.output.queue.missingSamples;CallbackErrorDelta=$b.audio.output.callbackErrors-$a.audio.output.callbackErrors}}
function Scheduling-Window([bool]$enabled,[string]$label){
 $null=App-Command $(if($enabled){'audio-scheduling-on'}else{'audio-scheduling-off'})
 $cpuBefore=[CopperScreenDisplayRecovery]::CpuTimes();$clock=[Diagnostics.Stopwatch]::StartNew()
 $window=Active-Window $label
 $cpuAfter=[CopperScreenDisplayRecovery]::CpuTimes();$total=($cpuAfter[1]-$cpuBefore[1])+($cpuAfter[2]-$cpuBefore[2])
 $window['MultimediaScheduling']=$enabled;$window['WallSeconds']=$clock.Elapsed.TotalSeconds
 $window['SystemCpuPercent']=if($total -gt 0){100.0*(1.0-($cpuAfter[0]-$cpuBefore[0])/$total)}else{$null}
 return $window
}
function Display-Window {
 $display=[CopperScreenDisplayRecovery]::new()
 $result=[ordered]@{Passed=$false;PowerPlanChanged=$false;SleepRequested=$false;InitialState=$display.State;Api='PowerSettingRegisterNotification / GUID_CONSOLE_DISPLAY_STATE'}
 try {
  $result['RequestedOffUtc']=[datetime]::UtcNow.ToString('o');$result['OffRequestAccepted']=$display.RequestOff()
  $clock=[Diagnostics.Stopwatch]::StartNew();while($display.State -ne 0 -and $clock.Elapsed.TotalSeconds -lt 15){Start-Sleep -Milliseconds 100}
  if($display.State -ne 0){throw 'No actual Windows display-off receipt; display-off coverage unavailable'}
  $a=App-Command 'snapshot';Start-Sleep -Seconds 8;$b=App-Command 'snapshot'
  $result['OffWindow']=@{Before=$a;After=$b;FrameDelta=$b.state.FrameNumber-$a.state.FrameNumber;SuppressedSampleDelta=$b.audio.suppressedSampleValues-$a.audio.suppressedSampleValues}
  if($display.State -ne 0 -or $b.state.IsPaused -or $b.state.FaultMessage -or $b.state.FrameNumber -le $a.state.FrameNumber){throw 'Guest did not continue with the display off, or the display woke during observation'}
  $result['RequestedOnUtc']=[datetime]::UtcNow.ToString('o')
  if(![CopperScreenDisplayRecovery]::RequestOn()){throw 'Display restore request failed'}
  $clock.Restart();while($display.State -ne 1 -and $clock.Elapsed.TotalSeconds -lt 15){Start-Sleep -Milliseconds 100}
  if($display.State -ne 1){throw 'No actual Windows display-on receipt'}
  Start-Sleep -Seconds 3
  $result['Restored']=Active-Window 'display-on';$result.Passed=$true
 }finally{
  $result['CleanupOnRequest']=[CopperScreenDisplayRecovery]::RequestOn()
  $result['Changes']=$display.Changes;$result['NotificationErrors']=$display.Errors
  Write-Json (Join-Path $root ($script:case+'-display-record.json')) $result
  $display.Dispose()
 }
 return $result
}
if(-not ('CopperScreenRecoveryActivation' -as [type])){Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class CopperScreenRecoveryActivation {
 [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
 [ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IActivation {
  [PreserveSig]int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string id,[MarshalAs(UnmanagedType.LPWStr)]string args,uint flags,out uint pid);
  [PreserveSig]int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,[MarshalAs(UnmanagedType.LPWStr)]string verb,out uint pid);
  [PreserveSig]int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr items,out uint pid);
 }
 public static uint Activate(string id,string args){var m=(IActivation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));try{uint pid;Marshal.ThrowExceptionForHR(m.ActivateApplication(id,args,6,out pid));return pid;}finally{Marshal.ReleaseComObject(m);}}
}
'@}
$owns=$false;$hostOwned=$false;$nativeAwake=$false
try{
 Save-Record
 if($NativeControlsOnly){
  if(![CopperScreenRecoveryActivation]::SetThreadExecutionState([uint32]2147483651)){throw 'Cannot establish native-control wake request'}
  $nativeAwake=$true;$record['ScopedWakeRequest']=@{StartedUtc=[datetime]::UtcNow.ToString('o');Flags='ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED';PowerPlanChanged=$false};Save-Record
 }
 if(!$NativeControlsOnly -and !$DisplayRecoveryOnly){$null=Wait-File (Join-Path $root 'Host/ready.json') 120;$hostOwned=$true}
 $owns=$true;Add-AppxPackage -Path $package -ErrorAction Stop
 $installed=@(Get-AppxPackage -Name $identity.Name)
 if($installed.Count -ne 1 -or $installed[0].IsDevelopmentMode -or $installed[0].Version -ne $identity.Version){throw 'Expected real signed installed candidate'}
 $installed=$installed[0]
 foreach($script:case in $Cases){
  $script:appId=0
  $requestPath=Join-Path $output ($script:case+'-request.json')
  Write-Json $requestPath @{RunId=$identity.Name.Substring(18);Case=$script:case;Phase='soak';DurationSeconds=1200;Recovery=$true;OutputDirectory=$output;Media=(Join-Path $config.AudioRoot 'Inputs/Lotus2.adf');InputScript=(Join-Path $config.AudioRoot 'Inputs/corpus-lotus2.json')}
  Write-Json (Join-Path $root 'progress.json') @{Stage='WARMUP';Case=$script:case;Utc=[datetime]::UtcNow.ToString('o')}
  $pidActivated=[CopperScreenRecoveryActivation]::Activate(($installed.PackageFamilyName+'!CopperScreen'),('--installed-soak "'+$requestPath+'"'))
  $script:process=Get-Process -Id $pidActivated;$null=$script:process.Handle
  $ready=Wait-File (Join-Path $output ($script:case+'-recovery-ready.json')) 1000
  $current=[ordered]@{Case=$script:case;Pid=$pidActivated;Ready=$ready;Steps=@();Status='RUNNING'}
  $record.Cases+=,$current;Save-Record
  $current['Baseline']=Active-Window 'baseline'
  if($DisplayRecoveryOnly -and $script:case -ceq 'copperstart'){
   $current['SchedulingComparison']=@((Scheduling-Window $false 'normal-1'),(Scheduling-Window $true 'games-1'),(Scheduling-Window $false 'normal-2'),(Scheduling-Window $true 'games-2'));Save-Record
  }
  $before=App-Command 'snapshot';$stopped=App-Command 'audio-stop'
  $restarted=Active-Window 'native-audio-restart'
  if($restarted.After.audio.output.recoveryAttempts -le $before.audio.output.recoveryAttempts){throw 'Native stopped sink did not exercise production recovery'}
  $current['NativeAudioRestartControl']=@{Before=$before;Stopped=$stopped;Steady=$restarted;PhysicalOsInterruption=$false};Save-Record
  $before=App-Command 'snapshot';$closed=App-Command 'audio-close'
  $record['RefillObservationSeconds']=$RefillObservationSeconds
  $reopened=Active-Window 'native-audio-reopen' $RefillObservationSeconds
  if($reopened.After.audio.output.deviceReinitializations -le $before.audio.output.deviceReinitializations){throw 'Native closed device did not exercise production reopening'}
  $current['NativeAudioReopenControl']=@{Before=$before;Closed=$closed;Steady=$reopened;PhysicalOsInterruption=$false};Save-Record
  if($DisplayRecoveryOnly){Gate 'display-off';$current['DisplayRecovery']=Display-Window;$record['DisplayTested']=$true;Save-Record}
  if(!$NativeControlsOnly -and !$DisplayRecoveryOnly){
  if(!$SkipSleep){
  Gate 'active-sleep'
  $before=App-Command 'snapshot';$hostResult=Host-Finish (Host-Start 'sleep');$displayWake=Request-DisplayWake;$after=App-Command 'snapshot'
  if($after.state.IsPaused -or $after.state.FaultMessage -or $after.state.FrameNumber -lt $before.state.FrameNumber){throw 'Active sleep corrupted session state'}
  if(@($hostResult.PowerEvents|Where-Object {$_.Provider -ceq 'Microsoft-Windows-Kernel-Power' -and $_.Id -eq 42}).Count -eq 0){throw 'No actual OS suspend event; sleep coverage is unavailable'}
  $record.SleepTested=$true
  $current.Steps+=@{Step='active-sleep';Before=$before;Host=$hostResult;DisplayWake=$displayWake;After=$after;Steady=(Active-Window 'active-sleep')};Save-Record
  $null=App-Command 'pause';Start-Sleep -Seconds 2
  Gate 'paused-sleep'
  $before=App-Command 'snapshot';$hostResult=Host-Finish (Host-Start 'sleep');$displayWake=Request-DisplayWake;$after=App-Command 'snapshot'
  if(!$before.state.IsPaused -or !$after.state.IsPaused -or $before.state.FrameNumber -ne $after.state.FrameNumber){throw 'Paused session advanced or resumed unexpectedly during S3'}
  if(@($hostResult.PowerEvents|Where-Object {$_.Provider -ceq 'Microsoft-Windows-Kernel-Power' -and $_.Id -eq 42}).Count -eq 0){throw 'No actual OS suspend event for paused session'}
  $null=App-Command 'resume';Start-Sleep -Seconds 2
  $current.Steps+=@{Step='paused-sleep';Before=$before;Host=$hostResult;DisplayWake=$displayWake;After=$after;Steady=(Active-Window 'paused-sleep')};Save-Record
  }
  Gate 'audio-device'
  $before=App-Command 'snapshot';$id=Host-Start 'device-cycle'
  $disabledPath=Join-Path $root ('Host/disabled-'+$id+'.json');$disableWait=[Diagnostics.Stopwatch]::StartNew()
  while(!(Test-Path -LiteralPath $disabledPath)){
   $hostReply=Join-Path $root ('Host/reply-'+$id+'.json')
   if(Test-Path -LiteralPath $hostReply){$failure=Get-Content $hostReply -Raw|ConvertFrom-Json;throw ('No passing device-interruption receipt was produced: '+$failure.Error)}
   if($disableWait.Elapsed.TotalSeconds -gt 40){throw 'Host did not report an actual device interruption'}
   Start-Sleep -Milliseconds 200
  }
  $during=App-Command 'snapshot';$hostResult=Host-Finish $id;Start-Sleep -Seconds 5;$after=App-Command 'snapshot'
  $record.PhysicalDeviceTested=$true
  $current.Steps+=@{Step='audio-device';Before=$before;During=$during;Host=$hostResult;After=$after;Steady=(Active-Window 'audio-device')};Save-Record
  }
  # Physical-sound observations are independent evidence. An optional operator
  # wait must not be confused with an application recovery or persistence result.
  if($script:case -ceq 'copperstart' -and $WaitForCopperStartSound){Gate 'audio-observation'}
  $null=App-Command 'checkpoint';$null=App-Command 'finish'
  $report=Wait-File (Join-Path $output ($script:case+'-soak.json')) 600
  Assert-AudioTimeline 'soak'
  $exit=Wait-CopperScreenProcessExit -Process $script:process -TimeoutSeconds 60
  if(!$report.passed -or !$report.windowOpened -or $report.elevated -or $report.packageFullName -cne $installed.PackageFullName){throw 'Installed recovery report failed or wrong identity'}
  $current['AppReport']=$report;$current['ProcessExit']=$exit
  $script:process.Dispose();$script:process=$null
  # Independently reopen through the existing native/media/profile workflow.
  Write-Json $requestPath @{RunId=$identity.Name.Substring(18);Case=$script:case;Phase='reopen';DurationSeconds=10;Recovery=$false;OutputDirectory=$output;Media=(Join-Path $config.AudioRoot 'Inputs/Lotus2.adf');InputScript=(Join-Path $config.AudioRoot 'Inputs/corpus-lotus2.json')}
  Write-Json (Join-Path $root 'progress.json') @{Stage='REOPEN';Case=$script:case;Utc=[datetime]::UtcNow.ToString('o')}
  $activated=[CopperScreenRecoveryActivation]::Activate(($installed.PackageFamilyName+'!CopperScreen'),('--installed-soak "'+$requestPath+'"'))
  $script:process=Get-Process -Id $activated;$null=$script:process.Handle
  $reopen=Wait-File (Join-Path $output ($script:case+'-reopen.json')) 1000
  Assert-AudioTimeline 'reopen'
  $reopenExit=Wait-CopperScreenProcessExit -Process $script:process -TimeoutSeconds 60
  if(!$reopen.passed -or $reopen.diskSha256 -cne $report.diskSha256){throw 'Saved media did not survive process restart'}
  foreach($f in $report.files.PSObject.Properties){if($reopen.files.($f.Name) -cne $f.Value){throw 'Profile/settings did not survive recovery/restart'}}
  $current['Reopen']=$reopen;$current['ReopenExit']=$reopenExit;$current.Status='PASSED';Save-Record
  $script:process.Dispose();$script:process=$null
 }
 $record.Status='PASSED';Save-Record
}catch{$record.Status='FAILED';$record['Error']=$_.Exception.ToString();Save-Record;throw}
finally{
 if($nativeAwake){$record.ScopedWakeRequest['ClearedUtc']=[datetime]::UtcNow.ToString('o');$record.ScopedWakeRequest['ClearResult']=[CopperScreenRecoveryActivation]::SetThreadExecutionState([uint32]2147483648)}
 if($script:process){if(!$script:process.HasExited){$script:process.Kill()};$script:process.Dispose()}
 if($hostOwned){try{$null=Host-Finish (Host-Start 'stop')}catch{$record['HostCleanupError']=$_.Exception.ToString()}}
 if($owns){foreach($p in @(Get-AppxPackage -Name $identity.Name)){
  if($p.Publisher -cne $identity.Publisher){throw 'Cleanup refused unexpected publisher'}
  $private=Join-Path $env:LOCALAPPDATA ('Packages/'+$p.PackageFamilyName+'/LocalState/CopperScreen/Soaks/'+$identity.Name.Substring(18))
  if(Test-Path -LiteralPath $private){Copy-Item -LiteralPath $private -Destination (Join-Path $root 'PrivateState') -Recurse}
  Remove-AppxPackage -Package $p.PackageFullName -ErrorAction Stop
 }}
 $record['Cleanup']=if($hostOwned){'Owned package removed; private test state, raw traces and exports retained; host helper restores original enabled audio-device state.'}else{'Owned package removed; private test state, raw traces and exports retained; no host power/device helper requested.'};Save-Record
}
