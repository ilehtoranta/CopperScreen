$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../qualification/CopperScreen.Soak.psm1') -Force
$root=Join-Path ([IO.Path]::GetTempPath()) ('copperscreen-soak-controls-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root|Out-Null
$path=Join-Path $root 'synthetic.jsonl'
$base=@(foreach($time in @(0,5)){
    @{elapsedSeconds=$time;privateBytes=100000;workingSetBytes=200000;managedBytes=50000;state=@{FrameNumber=1+$time*50;IsPaused=$false;FaultMessage=$null}}
})
function Save($samples){$samples|ForEach-Object {$_|ConvertTo-Json -Depth 5 -Compress}|Set-Content $path}
Save $base
$null=Assert-CopperScreenSoakTrace $path 10 -Smoke
$checks=1
foreach($case in @('short-hour','missing','gap','reverse','frozen','paused','fault','negative','nan','late-start','early-end')){
 $samples=$base|ConvertTo-Json -Depth 6|ConvertFrom-Json
 switch($case){
  'missing' {$samples=@($samples[0])}
  'gap' {$samples[1].elapsedSeconds=31}
  'reverse' {$samples[1].elapsedSeconds=-1}
  'frozen' {$samples[1].state.FrameNumber=$samples[0].state.FrameNumber}
  'paused' {$samples[1].state.IsPaused=$true}
  'fault' {$samples[1].state.FaultMessage='native fault'}
  'negative' {$samples[1].privateBytes=-1}
  'nan' {$samples[1].privateBytes='NaN'}
  'late-start' {$samples[0].elapsedSeconds=6;$samples[1].elapsedSeconds=9}
  'early-end' {$samples[1].elapsedSeconds=1}
 }
 Save $samples
 $rejected=$false
 try{if($case -eq 'short-hour'){$null=Assert-CopperScreenSoakTrace $path 3600}else{$null=Assert-CopperScreenSoakTrace $path 10 -Smoke}}catch{$rejected=$true}
 if(!$rejected){throw ('Invalid synthetic soak accepted: '+$case)}
 $checks++
}
$ordinaryOverhead=@(for($time=0d;$time -lt 3600;$time+=5.05){
    @{elapsedSeconds=$time;privateBytes=100000;workingSetBytes=200000;managedBytes=50000;state=@{FrameNumber=1+[long]($time*50);IsPaused=$false;FaultMessage=$null}}
})
Save $ordinaryOverhead
$null=Assert-CopperScreenSoakTrace $path 3600
$checks++
"Soak tooling controls: $checks passed. Synthetic traces are not installed-session evidence."
