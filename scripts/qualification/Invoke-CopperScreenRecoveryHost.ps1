[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path -LiteralPath $RunDirectory).Path
$config=Get-Content (Join-Path $root 'config.json') -Raw|ConvertFrom-Json
if($config.Identity -notmatch '^CopperScreen\.Test\.[0-9a-f]{32}$'){throw 'Only a diagnostic run may request host recovery actions'}
$deviceId=$config.AudioDevice
$principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Host power/device helper must be elevated; installed app must not be'}
$device=Get-PnpDevice -InstanceId $deviceId -ErrorAction Stop
$endpoint=$deviceId -ceq $config.Endpoint
$expectedClass=if($endpoint){'AudioEndpoint'}else{'MEDIA'}
$expectedName=if($endpoint){'24G42E (NVIDIA High Definition Audio)'}else{'NVIDIA High Definition Audio'}
if($device.Status -cne 'OK' -or $device.Class -cne $expectedClass -or $device.FriendlyName -cne $expectedName){throw 'Pinned, initially enabled HDMI audio device/endpoint required; no display/GPU device may be changed'}
$hostRoot=Join-Path $root 'Host';New-Item -ItemType Directory -Path $hostRoot -Force|Out-Null
function Write-Record($path,$value){$text=$value|ConvertTo-Json -Depth 12;$temp=$path+'.tmp';[IO.File]::WriteAllText($temp,$text);[IO.File]::Move($temp,$path,$true)}
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class CopperScreenRecoveryPower {
 [StructLayout(LayoutKind.Sequential,Pack=1)] struct Privileges {public uint Count;public long Luid;public uint Attributes;}
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr p,uint a,out IntPtr t);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool LookupPrivilegeValue(string s,string n,out long luid);
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool AdjustTokenPrivileges(IntPtr t,bool d,ref Privileges p,uint l,IntPtr b,IntPtr r);
 [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWaitableTimer(IntPtr a,bool manual,string name);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetWaitableTimer(IntPtr t,ref long due,int period,IntPtr cb,IntPtr arg,bool resume);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool CancelWaitableTimer(IntPtr t);
 [DllImport("powrprof.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)]
 static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)]bool hibernate,[MarshalAs(UnmanagedType.U1)]bool force,[MarshalAs(UnmanagedType.U1)]bool disableWake);
 public static void SleepWithWake(string name,int seconds) {
  if(!OpenProcessToken(GetCurrentProcess(),0x28,out var token))throw new Win32Exception();
  try {
   if(!LookupPrivilegeValue(null,"SeShutdownPrivilege",out var luid))throw new Win32Exception();
   var privilege=new Privileges{Count=1,Luid=luid,Attributes=2};
   if(!AdjustTokenPrivileges(token,false,ref privilege,0,IntPtr.Zero,IntPtr.Zero) || Marshal.GetLastWin32Error()!=0)throw new Win32Exception();
  }finally{CloseHandle(token);}
  var timer=CreateWaitableTimer(IntPtr.Zero,true,name);
  if(timer==IntPtr.Zero)throw new Win32Exception();
  try {
   // Absolute UTC FILETIME continues across S3; relative timers do not on current Windows.
   long due=DateTime.UtcNow.AddSeconds(seconds).ToFileTimeUtc();
   if(!SetWaitableTimer(timer,ref due,0,IntPtr.Zero,IntPtr.Zero,true) || Marshal.GetLastWin32Error()==50)throw new Win32Exception();
   if(!SetSuspendState(false,false,false))throw new Win32Exception();
  }finally{CancelWaitableTimer(timer);CloseHandle(timer);}
 }
}
'@
& powercfg /a *> (Join-Path $hostRoot 'power-states.txt')
& powercfg /query SCHEME_CURRENT SUB_SLEEP RTCWAKE *> (Join-Path $hostRoot 'wake-timer-policy.txt')
& powercfg /requests *> (Join-Path $hostRoot 'power-requests-before.txt')
Write-Record (Join-Path $hostRoot 'ready.json') @{Ready=$true;Elevated=$true;Device=$deviceId;DeviceInitiallyEnabled=$true;PowerPlanChanged=$false;ProcessId=$PID}
$last=0;$ownedDisabled=$false;$timer=[Diagnostics.Stopwatch]::StartNew()
try{
 while($timer.Elapsed.TotalHours -lt 2){
  $commandPath=Join-Path $hostRoot 'command.json'
  if(Test-Path -LiteralPath $commandPath){
   $command=Get-Content $commandPath -Raw|ConvertFrom-Json
   if($command.Id -gt $last){
    if($command.Id -ne $last+1 -or $command.Action -notin @('sleep','device-cycle','stop')){throw 'Invalid bounded host action'}
    $result=[ordered]@{Id=$command.Id;Action=$command.Action;StartedUtc=[datetime]::UtcNow.ToString('o');Passed=$false;Device=$deviceId}
    try{
     if($command.Action -eq 'sleep'){
      Write-Record (Join-Path $hostRoot ('starting-'+$command.Id+'.json')) $result
      [CopperScreenRecoveryPower]::SleepWithWake(('Local\CopperScreen.Recovery.'+$config.Identity+'.'+$command.Id),40)
      $result['WakeTimerSeconds']=40
      & powercfg /lastwake *> (Join-Path $hostRoot ('lastwake-'+$command.Id+'.txt'))
     }
     elseif($command.Action -eq 'device-cycle'){
      if((Get-PnpDevice -InstanceId $deviceId).Status -cne 'OK'){throw 'Device state changed before test; refusing to own it'}
      $ownedDisabled=$true
      try{
       Disable-PnpDevice -InstanceId $deviceId -Confirm:$false -ErrorAction Stop
       $result['DisabledState']=(Get-PnpDevice -InstanceId $deviceId)|Select-Object Status,Problem,InstanceId
       if($result.DisabledState.Status -cne 'Error' -or [int]$result.DisabledState.Problem -ne 22){throw 'No verified PnP disabled state (problem 22); device recovery coverage is unavailable'}
       Write-Record (Join-Path $hostRoot ('disabled-'+$command.Id+'.json')) $result
       Start-Sleep -Seconds 10
      }finally{
       # A rejected disable request can leave the device unchanged. Do not mask
       # that failure by trying to enable an already enabled device.
       if((Get-PnpDevice -InstanceId $deviceId).Status -cne 'OK'){
        Enable-PnpDevice -InstanceId $deviceId -Confirm:$false -ErrorAction Stop
       }
       $ownedDisabled=$false
      }
      $wait=[Diagnostics.Stopwatch]::StartNew()
      while((Get-PnpDevice -InstanceId $deviceId).Status -cne 'OK' -and $wait.Elapsed.TotalSeconds -lt 30){Start-Sleep -Seconds 1}
      if((Get-PnpDevice -InstanceId $deviceId).Status -cne 'OK'){throw 'Audio device did not return to its original enabled state'}
      $result['RestoredState']=(Get-PnpDevice -InstanceId $deviceId)|Select-Object Status,Problem,InstanceId
     }
     $result.Passed=$true
    }catch{$result['Error']=$_.Exception.ToString()}
    $result['CompletedUtc']=[datetime]::UtcNow.ToString('o')
    $events=@(Get-WinEvent -FilterHashtable @{LogName='System';StartTime=[datetime]::Parse($result.StartedUtc).AddSeconds(-2);Id=1,42,107} -ErrorAction SilentlyContinue|Where-Object ProviderName -in @('Microsoft-Windows-Kernel-Power','Microsoft-Windows-Power-Troubleshooter')|ForEach-Object {@{RecordId=$_.RecordId;Id=$_.Id;Provider=$_.ProviderName;TimeCreated=$_.TimeCreated.ToUniversalTime().ToString('o');Xml=$_.ToXml()}})
    $result['PowerEvents']=$events
    Write-Record (Join-Path $hostRoot ('reply-'+$command.Id+'.json')) $result
    $last=$command.Id
    if($command.Action -eq 'stop'){break}
   }
  }
  Start-Sleep -Milliseconds 250
 }
}finally{
 if($ownedDisabled -and (Get-PnpDevice -InstanceId $deviceId).Status -cne 'OK'){Enable-PnpDevice -InstanceId $deviceId -Confirm:$false -ErrorAction Stop}
 Write-Record (Join-Path $hostRoot 'stopped.json') @{Stopped=$true;DeviceRestored=(Get-PnpDevice -InstanceId $deviceId).Status -ceq 'OK';Utc=[datetime]::UtcNow.ToString('o')}
}
