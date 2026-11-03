param(
    [Parameter(Mandatory)][string]$SpeedscopePath,
    [Parameter(Mandatory)][string]$RunnerLogPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$trace=Get-Content $SpeedscopePath -Raw | ConvertFrom-Json
$line=@(Get-Content $RunnerLogPath | Where-Object { $_ -clike 'engine=lightweight-a500 *' })
if ($line.Count -ne 1 -or $line[0] -cnotmatch ' warmup=14520 frames=3600 completed=18120 fps=(\d+(?:[.,]\d+)?) ') { throw 'Expected one complete retained native workload.' }
$fps=[double]::Parse($Matches[1].Replace(',','.'),[Globalization.CultureInfo]::InvariantCulture)
$frames=@($trace.shared.frames | ForEach-Object name)
$profiles=@($trace.profiles | Where-Object { $_.type -ceq 'evented' -and $_.unit -ceq 'milliseconds' -and
    @($_.events | Where-Object { $frames[$_.frame] -match 'LightweightA500Machine.ExecuteFrame\(' }).Count -gt 0 })
if ($profiles.Count -ne 1) { throw 'Expected one engine-thread evented millisecond profile.' }
$profile=$profiles[0]
$lastFrame=(@($profile.events | Where-Object { $frames[$_.frame] -match 'LightweightA500Machine.ExecuteFrame\(' }) | Measure-Object at -Maximum).Maximum
# Same conservative interior-tail selection as the October profile: half the
# runner's measured duration, at most 30s, ending 1s before the last engine sample.
# Sampling lacks explicit measurement markers, so this is approximate diagnosis.
$end=$lastFrame-1000
$begin=$end-[math]::Min(30000,0.5*3600/$fps*1000)
$stack=[Collections.Generic.List[int]]::new()
$totals=@{}; $totalMs=0.0; $slices=0; $maxSlice=0.0
$previous=[double]$profile.startValue
foreach ($event in $profile.events) {
    $at=[double]$event.at
    if ($at -lt $previous) { throw 'Nonmonotonic trace.' }
    $overlap=[math]::Min($at,$end)-[math]::Max($previous,$begin)
    if ($overlap -gt 0 -and $stack.Count -gt 0) {
        $names=@($stack | ForEach-Object { $frames[$_] })
        if ($names -ccontains 'CPU_TIME' -and @($names | Where-Object { $_ -match 'LightweightA500Machine.ExecuteFrame\(' }).Count -gt 0) {
            $totalMs+=$overlap; $slices++; $maxSlice=[math]::Max($maxSlice,$at-$previous)
            $methods=@($names | Where-Object { $_ -cne 'CPU_TIME' })
            $leaf=$methods[-1]
            foreach ($name in @($methods | Select-Object -Unique)) {
                if (-not $totals.ContainsKey($name)) { $totals[$name]=[ordered]@{Name=$name;InclusiveMs=0.0;ExclusiveMs=0.0;InclusiveSlices=0;ExclusiveSlices=0} }
                $totals[$name].InclusiveMs+=$overlap; $totals[$name].InclusiveSlices++
            }
            $totals[$leaf].ExclusiveMs+=$overlap; $totals[$leaf].ExclusiveSlices++
        }
    }
    if ($event.type -ceq 'O') { $stack.Add([int]$event.frame) }
    elseif ($event.type -ceq 'C') {
        if ($stack.Count -eq 0 -or $stack[$stack.Count-1] -ne [int]$event.frame) { throw 'Non-LIFO trace.' }
        $stack.RemoveAt($stack.Count-1)
    } else { throw 'Unexpected event type.' }
    $previous=$at
}
if ($stack.Count -ne 0 -or $totalMs -le 0) { throw 'Unclosed or empty trace.' }
$rows=@($totals.Values | ForEach-Object { [pscustomobject]@{
    Name=$_.Name; InclusiveMs=$_.InclusiveMs; ExclusiveMs=$_.ExclusiveMs
    InclusivePercent=100*$_.InclusiveMs/$totalMs; ExclusivePercent=100*$_.ExclusiveMs/$totalMs
    InclusiveSlices=$_.InclusiveSlices; ExclusiveSlices=$_.ExclusiveSlices
} })
$result=[ordered]@{
    Protocol='lemmings-interior-tail-profile-v1'; DiagnosticOnly=$true; ThroughputConclusion=$false
    SpeedscopeSha256=(Get-FileHash $SpeedscopePath).Hash; RunnerLogSha256=(Get-FileHash $RunnerLogPath).Hash
    WindowStartMs=$begin; WindowEndMs=$end; AttributedCpuTimeMs=$totalMs; Slices=$slices; MaximumSampleIntervalMs=$maxSlice
    Limits='Approximate measured-tail window; sampled managed stacks include inlined code and descheduling gaps. Inclusive parents overlap children. Counts and attributed time are diagnostic, not a confidence bound or causal speedup.'
    TopExclusive=@($rows | Sort-Object ExclusiveMs -Descending | Select-Object -First 25)
    InterruptSampling=@($rows | Where-Object Name -match 'HasRecognizedInterrupt|SampleNext|DispatchPendingCpuInterrupt')
    AllMethods=$rows
}
$result | ConvertTo-Json -Depth 8 | Set-Content $OutputPath
$result.TopExclusive | Select-Object -First 12 Name,ExclusivePercent,ExclusiveSlices | Format-Table -AutoSize
$result.InterruptSampling | Select-Object Name,InclusivePercent,ExclusivePercent,InclusiveSlices | Format-Table -AutoSize
