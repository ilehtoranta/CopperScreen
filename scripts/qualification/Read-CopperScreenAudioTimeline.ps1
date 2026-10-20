[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$raw=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json -DateKind String
$t=$raw.timeline
if($t.protocol -cne 'copperscreen-audio-owner-timeline-v1'){throw 'Unknown audio timeline protocol'}
if(!$raw.nativeStopped -or $t.owner.incomplete -or $t.callbacks.incomplete){throw 'Joined, complete trace snapshot required'}
$frequency=[double]$t.qpcFrequency
function Ms([long]$ticks){1000.0*$ticks/$frequency}
$owners=@($t.owner.records|ForEach-Object {$v=$_.value;[pscustomobject]@{
 sequence=$_.sequence;kind=$v.Kind;start=$v.Start;end=$v.End;field=$v.Field;
 wallMs=(Ms ($v.End-$v.Start));cpuMs=if($v.CpuStart100ns -ge 0 -and $v.CpuEnd100ns -ge $v.CpuStart100ns){($v.CpuEnd100ns-$v.CpuStart100ns)/10000.0}else{$null};
 reserveMs=if($v.Kind -eq 1){Ms ($v.Reserved-$v.Start)}else{$null};
 guestMs=if($v.Kind -eq 1){Ms ($v.GuestDone-$v.Reserved)}else{$null};
 submitMs=if($v.Kind -eq 1){Ms ($v.Submitted-$v.GuestDone)}else{$null};
 publishMs=if($v.Kind -eq 1){Ms ($v.End-$v.Submitted)}else{$null};
 publicationCoreMs=if($v.Kind -eq 1 -and $v.PSObject.Properties.Name -contains 'PublicationCoreMilliseconds' -and $v.PublicationCoreMilliseconds -ge 0){$v.PublicationCoreMilliseconds}else{$null};
 queueBefore=if($v.Kind -ne 4){$v.QueueBefore}else{$null};queueAfter=$v.QueueAfter;
 missingBefore=if($v.Kind -ne 4){$v.MissingBefore}else{$null};missingAfter=$v.MissingAfter;detail=$v.Detail;
 gc0=$v.Gc0;gc1=$v.Gc1;gc2=$v.Gc2
}}|Sort-Object start)
$callbacks=@($t.callbacks.records|ForEach-Object {$v=$_.value;[pscustomobject]@{
 sequence=$_.sequence;start=$v.Start;end=$v.End;queueBefore=$v.QueueBefore;queueAfter=$v.QueueAfter;
 requestedValues=$v.RequestedValues;missingValues=$v.MissingValues;cumulativeMissingValues=$v.CumulativeMissingValues;expectAudio=$v.ExpectAudio
}})
$missing=@($callbacks|Where-Object missingValues -gt 0)
$episodes=[Collections.Generic.List[object]]::new()
foreach($c in $missing){
 if(!$episodes.Count -or (Ms ($c.start-$episodes[$episodes.Count-1].end)) -gt 80){$episodes.Add([pscustomobject]@{start=$c.start;end=$c.end;missingValues=[long]$c.missingValues;callbacks=1;firstCallbackSequence=$c.sequence;lastCallbackSequence=$c.sequence})}
 else{$e=$episodes[$episodes.Count-1];$e.end=$c.end;$e.missingValues+=$c.missingValues;$e.callbacks++;$e.lastCallbackSequence=$c.sequence}
}
$details=@(foreach($e in $episodes){
 $overlap=@($owners|Where-Object {$_.start -le $e.end -and $_.end -ge $e.start})
 @{startQpc=$e.start;endQpc=$e.end;sinceAnchorSeconds=($e.start-$t.anchorQpc)/$frequency;missingValues=$e.missingValues;callbacks=$e.callbacks;
 ownerIntervals=@($overlap|Sort-Object wallMs -Descending|Select-Object -First 8);
 previousOwner=@($owners|Where-Object end -lt $e.start|Select-Object -Last 1);
 nextOwner=@($owners|Where-Object start -gt $e.end|Select-Object -First 1);
 retentionNote='Missing middle ranges are explicit; an absent owner interval is unavailable coverage, not proof of idle/preemption.'}
})
$fields=@($owners|Where-Object kind -eq 1)
$bands=@(foreach($band in @(@(1,8),@(9,30),@(31,100),@(101,500),@(501,2000),@(2001,[long]::MaxValue))){
 $rows=@($fields|Where-Object {$_.field -ge $band[0] -and $_.field -le $band[1]})
 if($rows.Count){@{firstField=$rows[0].field;lastField=$rows[-1].field;count=$rows.Count;meanGuestMs=($rows.guestMs|Measure-Object -Average).Average;maxGuestMs=($rows.guestMs|Measure-Object -Maximum).Maximum;meanWallMs=($rows.wallMs|Measure-Object -Average).Average;meanCpuMs=($rows.cpuMs|Measure-Object -Average).Average;missingDuringFields=($rows|ForEach-Object {$_.missingAfter-$_.missingBefore}|Measure-Object -Sum).Sum}}
})
$result=[ordered]@{protocol='copperscreen-audio-owner-analysis-v1';input=(Resolve-Path -LiteralPath $Path).Path;inputSha256=(Get-FileHash -LiteralPath $Path).Hash;
 qpcFrequency=$frequency;ownerTotal=$t.owner.total;ownerOverwritten=$t.owner.overwritten;callbackTotal=$t.callbacks.total;callbackOverwritten=$t.callbacks.overwritten;
 firstIncidentQpc=if($t.PSObject.Properties.Name -contains 'firstIncidentQpc'){$t.firstIncidentQpc}else{$null};
 finalCumulativeMissingValues=if($t.PSObject.Properties.Name -contains 'finalCumulativeMissingValues'){$t.finalCumulativeMissingValues}elseif($callbacks.Count){$callbacks[-1].cumulativeMissingValues}else{0};
 ownerObservedTotal=if($t.owner.PSObject.Properties.Name -contains 'observedTotal'){$t.owner.observedTotal}else{$t.owner.total};
 ownerNotRecordedAfterIncident=if($t.owner.PSObject.Properties.Name -contains 'notRecordedAfterIncident'){$t.owner.notRecordedAfterIncident}else{0};
 callbackObservedTotal=if($t.callbacks.PSObject.Properties.Name -contains 'observedTotal'){$t.callbacks.observedTotal}else{$t.callbacks.total};
 callbacksNotRecordedAfterIncident=if($t.callbacks.PSObject.Properties.Name -contains 'notRecordedAfterIncident'){$t.callbacks.notRecordedAfterIncident}else{0};
 retainedMissingValues=if($missing.Count){($missing.missingValues|Measure-Object -Sum).Sum}else{0};fieldBands=$bands;longestOwners=@($owners|Sort-Object wallMs -Descending|Select-Object -First 30);episodes=$details;
 interpretation='Diagnostic wall intervals and coarse Windows thread CPU accounting; no kernel scheduling proof, no per-cycle trace, no release-budget acceptance.'}
if(Test-Path -LiteralPath $OutputPath){throw 'Preserve existing analysis; choose a new output path'}
$result|ConvertTo-Json -Depth 14|Set-Content -LiteralPath $OutputPath
@{path=$OutputPath;episodes=$episodes.Count;retainedMissingValues=$result.retainedMissingValues;ownerOverwritten=$t.owner.overwritten;callbackOverwritten=$t.callbacks.overwritten;bands=$bands}|ConvertTo-Json -Depth 5
