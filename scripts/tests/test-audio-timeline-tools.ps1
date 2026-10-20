$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$directory=Join-Path ([IO.Path]::GetTempPath()) ('copperscreen-timeline-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $directory|Out-Null
$reader=Join-Path $PSScriptRoot ../qualification/Read-CopperScreenAudioTimeline.ps1
try {
 $owner=@{sequence=1;value=@{Kind=2;Start=100;Reserved=0;GuestDone=0;Submitted=0;End=200;CpuStart100ns=10000;CpuEnd100ns=20000;Field=8;QueueBefore=2;QueueAfter=0;MissingBefore=0;MissingAfter=5;Detail=1;Gc0=0;Gc1=0;Gc2=0}}
 $callback=@{sequence=1;value=@{Start=150;End=151;QueueBefore=0;QueueAfter=0;RequestedValues=8;MissingValues=5;CumulativeMissingValues=5;ExpectAudio=$true}}
 $trace=@{nativeStopped=$true;timeline=@{protocol='copperscreen-audio-owner-timeline-v1';qpcFrequency=1000;anchorQpc=0;owner=@{total=1;overwritten=0;incomplete=0;records=@($owner)};callbacks=@{total=1;overwritten=0;incomplete=0;records=@($callback)}}}
 $path=Join-Path $directory trace.json;$output=Join-Path $directory analysis.json
 $trace|ConvertTo-Json -Depth 12|Set-Content $path
 $null=& $reader -Path $path -OutputPath $output
 $a=Get-Content $output -Raw|ConvertFrom-Json
 if($a.retainedMissingValues -ne 5 -or $a.episodes.Count -ne 1 -or $a.episodes[0].ownerIntervals[0].kind -ne 2 -or $a.episodes[0].ownerIntervals[0].wallMs -ne 100 -or $a.episodes[0].ownerIntervals[0].cpuMs -ne 1){throw 'Command/underrun correlation or units incorrect'}
 $trace.timeline.callbacks.records[0].value.MissingValues=0
 $trace|ConvertTo-Json -Depth 12|Set-Content $path
 $null=& $reader -Path $path -OutputPath (Join-Path $directory zero.json)
 $a=Get-Content (Join-Path $directory zero.json) -Raw|ConvertFrom-Json
 if($a.retainedMissingValues -ne 0 -or $a.episodes.Count){throw 'Zero loss was not preserved'}
 $trace.timeline.owner.records[0].value.Kind=4
 $trace|ConvertTo-Json -Depth 12|Set-Content $path
 $null=& $reader -Path $path -OutputPath (Join-Path $directory unavailable.json)
 $a=Get-Content (Join-Path $directory unavailable.json) -Raw|ConvertFrom-Json
 if($null -ne $a.longestOwners[0].queueBefore -or $null -ne $a.longestOwners[0].missingBefore){throw 'Recovery placeholders were treated as measured zeroes'}
 $trace.timeline.firstIncidentQpc=150;$trace.timeline.finalCumulativeMissingValues=12
 $trace.timeline.owner.observedTotal=6;$trace.timeline.owner.notRecordedAfterIncident=5
 $trace.timeline.callbacks.observedTotal=8;$trace.timeline.callbacks.notRecordedAfterIncident=7
 $trace|ConvertTo-Json -Depth 12|Set-Content $path
 $null=& $reader -Path $path -OutputPath (Join-Path $directory incident.json)
 $a=Get-Content (Join-Path $directory incident.json) -Raw|ConvertFrom-Json
 if($a.finalCumulativeMissingValues -ne 12 -or $a.ownerNotRecordedAfterIncident -ne 5 -or $a.callbacksNotRecordedAfterIncident -ne 7 -or $a.callbackObservedTotal -ne 8 -or $a.firstIncidentQpc -ne 150){throw 'Incident retention hid final counters or unrecorded coverage'}
 $trace.timeline.owner.incomplete=1;$trace|ConvertTo-Json -Depth 12|Set-Content $path
 $rejected=$false
 try{$null=& $reader -Path $path -OutputPath (Join-Path $directory invalid.json)}catch{$rejected=$_.Exception.Message -match 'complete trace'}
 if(!$rejected){throw 'Incomplete trace accepted'}
 'PASS: correlation/units, zero-loss input, unavailable recovery counters, first-incident coverage/final counter and incomplete-trace rejection'
} finally {
 # Only this exact, newly created fixture directory is owned by the test.
 $resolved=[IO.Path]::GetFullPath($directory)
 if(!$resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected fixture cleanup path'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
