Set-StrictMode -Version Latest

function Get-CopperScreenBuildNodeSnapshot {
    $all = @(Get-CimInstance Win32_Process -ErrorAction Stop)
    @($all | Where-Object Name -ceq 'MSBuild.exe' | ForEach-Object {
        $node = $_
        $parent = @($all | Where-Object ProcessId -eq $node.ParentProcessId)
        $hasCounters = $null -ne $node.UserModeTime -and $null -ne $node.KernelModeTime -and $null -ne $node.CreationDate
        [pscustomobject]@{
            Id = [int]$node.ProcessId
            ParentId = [int]$node.ParentProcessId
            VisualStudioParent = $parent.Count -eq 1 -and $parent[0].Name -ceq 'devenv.exe'
            ReusableNode = $null -ne $node.CommandLine -and
                $node.CommandLine -match '(?i)(?:^|\s)/nodemode:1(?:\s|$)' -and
                $node.CommandLine -match '(?i)(?:^|\s)/nodeReuse:true(?:\s|$)'
            HasCounters = $hasCounters
            CreationTicks = if ($hasCounters) { $node.CreationDate.ToUniversalTime().Ticks } else { 0 }
            Cpu100ns = if ($hasCounters) { [decimal]$node.UserModeTime + [decimal]$node.KernelModeTime } else { -1 }
        }
    })
}

function Get-CopperScreenIdleBuildNodeIds {
    param([AllowEmptyCollection()][object[]]$Before, [AllowEmptyCollection()][object[]]$After)
    foreach ($node in $After) {
        $previous = @($Before | Where-Object Id -eq $node.Id)
        if ($previous.Count -ne 1) { continue }
        $old = $previous[0]
        # Only an unchanged reusable VS child with complete, stable native CPU
        # counters is idle in this observed interval. CPU activity, new/reused
        # PIDs, missing counters or a different parent remain fail-closed jobs.
        if ($node.HasCounters -and $old.HasCounters -and
            $node.ReusableNode -and $old.ReusableNode -and
            $node.VisualStudioParent -and $old.VisualStudioParent -and
            $node.ParentId -eq $old.ParentId -and
            $node.CreationTicks -eq $old.CreationTicks -and
            $node.Cpu100ns -ge 0 -and $node.Cpu100ns -eq $old.Cpu100ns) {
            $node.Id
        }
    }
}

function Assert-CopperScreenIdleBuildNodeControls {
    $idle = [pscustomobject]@{Id=10;ParentId=20;VisualStudioParent=$true;ReusableNode=$true;HasCounters=$true;CreationTicks=30;Cpu100ns=100}
    if (@(Get-CopperScreenIdleBuildNodeIds @($idle) @($idle)).Count -ne 1) { throw 'Idle reusable node was rejected.' }
    foreach ($change in @(
        @{Cpu100ns=101}, @{Cpu100ns=99}, @{ParentId=21}, @{CreationTicks=31},
        @{VisualStudioParent=$false}, @{ReusableNode=$false}, @{HasCounters=$false}
    )) {
        $copy = [ordered]@{}
        foreach ($property in $idle.PSObject.Properties) { $copy[$property.Name] = $property.Value }
        foreach ($key in $change.Keys) { $copy[$key] = $change[$key] }
        if (@(Get-CopperScreenIdleBuildNodeIds @($idle) @([pscustomobject]$copy)).Count) { throw 'Active/unknown node was treated as idle.' }
    }
    if (@(Get-CopperScreenIdleBuildNodeIds @() @($idle)).Count) { throw 'Missing node telemetry was accepted.' }
}

Export-ModuleMember -Function Get-CopperScreenBuildNodeSnapshot, Get-CopperScreenIdleBuildNodeIds, Assert-CopperScreenIdleBuildNodeControls
