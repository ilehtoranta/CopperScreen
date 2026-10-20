Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Assert-CopperScreenSoakTrace {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][int]$Seconds,[switch]$Smoke)
    if(!$Smoke -and $Seconds -ne 3600){throw 'A full installed session requires 3600 seconds'}
    $samples=@(Get-Content -LiteralPath $Path|Where-Object {$_}|ForEach-Object {$_|ConvertFrom-Json})
    # The nominal five-second delay includes observer work. Validate actual coverage
    # and the maximum gap, rather than requiring a drift-free timer tick count.
    if($samples.Count -lt [Math]::Max(2,[Math]::Ceiling($Seconds/30))){throw 'Incomplete soak sample series'}
    $last=-1d;$frame=-1L;$private=@();$working=@();$managed=@()
    foreach($sample in $samples){
        foreach($number in @($sample.elapsedSeconds,$sample.privateBytes,$sample.workingSetBytes,$sample.managedBytes,$sample.state.FrameNumber)){
            if($null -eq $number -or ![double]::IsFinite([double]$number) -or $number -lt 0){throw 'Missing or invalid soak measurement'}
        }
        if($sample.elapsedSeconds -le $last -or ($last -ge 0 -and $sample.elapsedSeconds-$last -gt 30) -or $sample.state.FrameNumber -le $frame -or
            $sample.state.IsPaused -or $sample.state.FaultMessage -or $sample.privateBytes -le 0 -or $sample.workingSetBytes -le 0){throw 'Nonmonotonic, stalled, paused, faulted or gapped soak series'}
        $last=[double]$sample.elapsedSeconds;$frame=[long]$sample.state.FrameNumber
        $private+=,[long]$sample.privateBytes;$working+=,[long]$sample.workingSetBytes;$managed+=,[long]$sample.managedBytes
    }
    if($samples[0].elapsedSeconds -gt 5 -or $last -lt $Seconds-6 -or $last -gt $Seconds+30){throw 'Trace does not cover the requested session'}
    [pscustomobject]@{Samples=$samples.Count;FirstSeconds=$samples[0].elapsedSeconds;LastSeconds=$last;FrameDelta=$samples[-1].state.FrameNumber-$samples[0].state.FrameNumber;
        PrivateFirst=$private[0];PrivateLast=$private[-1];PrivateMin=($private|Measure-Object -Minimum).Minimum;PrivateMax=($private|Measure-Object -Maximum).Maximum;
        WorkingSetFirst=$working[0];WorkingSetLast=$working[-1];WorkingSetMax=($working|Measure-Object -Maximum).Maximum;
        ManagedFirst=$managed[0];ManagedLast=$managed[-1];ManagedMax=($managed|Measure-Object -Maximum).Maximum;Budgets='OPEN';AcceptanceDuration=!$Smoke}
}
Export-ModuleMember -Function Assert-CopperScreenSoakTrace
