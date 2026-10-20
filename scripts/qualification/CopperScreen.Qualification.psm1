Set-StrictMode -Version Latest

function Get-CopperScreenQualificationFile {
    param([Parameter(Mandatory)][string]$Path)
    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    [pscustomobject]@{ Path = $file.FullName; Bytes = $file.Length; Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
}

function Assert-CopperScreenQualificationFiles {
    param([Parameter(Mandatory)][object[]]$Files)
    foreach ($file in $Files) {
        $current = Get-CopperScreenQualificationFile $file.Path
        if ($current.Bytes -ne $file.Bytes -or $current.Sha256 -cne $file.Sha256) { throw "Frozen input changed: $($file.Path)" }
    }
}

function Read-CopperScreenBaselineSample {
    param([Parameter(Mandatory)][string]$Line, [Parameter(Mandatory)][string]$Expected)
    if ($Line -cnotmatch '^engine=lightweight-a500 .+ fps=(\d+(?:[.,]\d+)?) .+$') { throw 'Runner output has no unambiguous decimal FPS.' }
    $fps = [double]::Parse($Matches[1].Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture)
    if (-not [double]::IsFinite($fps) -or $fps -le 0) { throw 'Runner FPS must be finite and positive.' }
    $identity = $Line -creplace 'fps=\d+(?:[.,]\d+)? ', ''
    if ($identity -cne $Expected) { throw 'Complete retained workload identity differs; do not learn a new golden from this run.' }
    [pscustomobject]@{ Fps = $fps; MsPerField = 1000 / $fps; Fingerprint = $identity }
}

function Get-CopperScreenQualificationTopology {
    # Same CPU-set fields used by the retained homogeneous protocol; no historical script is modified.
    if (-not ('CopperScreenQualificationCpuSetsV1' -as [type])) {
        Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CopperScreenQualificationCpuSetsV1 {
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetSystemCpuSetInformation(IntPtr p,uint n,out uint size,IntPtr process,uint flags);
 public static string[] Read() {
  uint size; GetSystemCpuSetInformation(IntPtr.Zero,0,out size,IntPtr.Zero,0);
  if(size==0) throw new Exception("Missing topology");
  var p=Marshal.AllocHGlobal(checked((int)size));
  try {
   if(!GetSystemCpuSetInformation(p,size,out size,IntPtr.Zero,0)) throw new Exception("CPU topology unavailable");
   var rows=new System.Collections.Generic.List<string>();
   for(int offset=0;offset<size;) {
    var q=IntPtr.Add(p,offset); var length=Marshal.ReadInt32(q);
    if(length<8 || offset+length>size) throw new Exception("Invalid topology entry");
    if(Marshal.ReadInt32(q,4)==0) {
     if(length<20) throw new Exception("Truncated CPU set");
     rows.Add($"{Marshal.ReadInt16(q,12)},{Marshal.ReadByte(q,14)},{Marshal.ReadByte(q,15)},{Marshal.ReadByte(q,18)}");
    }
    offset+=length;
   }
   return rows.ToArray();
  } finally { Marshal.FreeHGlobal(p); }
 }
}
'@
    }
    @([CopperScreenQualificationCpuSetsV1]::Read() | ForEach-Object {
        $values = $_.Split(',')
        [pscustomobject]@{ Group=[int]$values[0]; Cpu=[int]$values[1]; Core=[int]$values[2]; Efficiency=[int]$values[3] }
    })
}

Export-ModuleMember -Function Get-CopperScreenQualificationFile, Assert-CopperScreenQualificationFiles, Read-CopperScreenBaselineSample, Get-CopperScreenQualificationTopology
