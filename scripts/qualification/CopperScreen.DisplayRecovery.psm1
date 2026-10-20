Set-StrictMode -Version Latest
function Initialize-CopperScreenDisplayRecovery {
 if ('CopperScreenDisplayRecovery' -as [type]) { return }
 Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
public sealed class CopperScreenDisplayRecovery : IDisposable {
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint Notify(IntPtr context,uint type,IntPtr setting);
 [StructLayout(LayoutKind.Sequential)] struct Subscription {public IntPtr Callback,Context;}
 public sealed class Change {public string Utc {get;set;} public int State {get;set;} }
 readonly Notify callback;
 readonly ConcurrentQueue<Change> changes=new();
 readonly Guid displayGuid=new("6FE69556-704A-47A0-8F24-C28D936FDA47");
 IntPtr registration; int state=-1,errors;
 public int State => Volatile.Read(ref state);
 public int Errors => Volatile.Read(ref errors);
 public Change[] Changes => changes.ToArray();
 public CopperScreenDisplayRecovery() {
  callback=OnChange;
  var subscription=new Subscription{Callback=Marshal.GetFunctionPointerForDelegate(callback)};
  var guid=displayGuid;
  var result=PowerSettingRegisterNotification(ref guid,2,ref subscription,out registration);
  if(result!=0)throw new System.ComponentModel.Win32Exception((int)result);
 }
 uint OnChange(IntPtr context,uint type,IntPtr setting) {
  try {if(type==0x8013 && setting!=IntPtr.Zero && Marshal.PtrToStructure<Guid>(setting)==displayGuid && Marshal.ReadInt32(setting,16)==4) {
   var value=Marshal.ReadInt32(setting,20);Volatile.Write(ref state,value);
   changes.Enqueue(new Change{Utc=DateTime.UtcNow.ToString("o"),State=value});
  }}catch{Interlocked.Increment(ref errors);}return 0;
 }
 public bool RequestOff() => SendMessageTimeout(new IntPtr(0xffff),0x112,new IntPtr(0xf170),new IntPtr(2),2,1000,out _)!=IntPtr.Zero;
 public static bool RequestOn() => SetThreadExecutionState(2)!=0;
 public static ulong[] CpuTimes() {if(!GetSystemTimes(out var idle,out var kernel,out var user))throw new System.ComponentModel.Win32Exception();return new[]{idle,kernel,user};}
 public void Dispose(){if(registration!=IntPtr.Zero){PowerSettingUnregisterNotification(registration);registration=IntPtr.Zero;}GC.KeepAlive(callback);}
 [DllImport("powrprof.dll")] static extern uint PowerSettingRegisterNotification(ref Guid guid,uint flags,ref Subscription recipient,out IntPtr registration);
 [DllImport("powrprof.dll")] static extern uint PowerSettingUnregisterNotification(IntPtr registration);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wparam,IntPtr lparam,uint flags,uint timeout,out IntPtr result);
 [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
}
'@
}
Export-ModuleMember -Function Initialize-CopperScreenDisplayRecovery
