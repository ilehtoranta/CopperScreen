Set-StrictMode -Version Latest

function Compress-CopperScreenCompletedArtifacts {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$Path
    )

    $result = [ordered]@{ Path = [IO.Path]::GetFullPath($Path); Status = 'SKIPPED'; FilesCompressed = 0; BytesConsidered = 0L; Reason = $null }
    if (-not $IsWindows) { $result.Reason = 'NTFS compression is available only on Windows.'; return [pscustomobject]$result }

    $repo = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepositoryRoot).Path).TrimEnd('\')
    $artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\')
    $target = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\')
    if (-not $target.StartsWith($artifactsRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        $result.Reason = 'Output is outside this repository artifacts directory.'
        return [pscustomobject]$result
    }

    # A second build, replay, or test may still be consuming this run directory.
    # When process inventory is unavailable, leave it untouched.
    try {
        $alternateTarget = $target.Replace('\', '/')
        $active = @(Get-CimInstance -ClassName Win32_Process -ErrorAction Stop | Where-Object {
            $_.ProcessId -ne $PID -and $_.Name -match '^(dotnet|msbuild|vstest\.console|CopperScreen)(\.exe)?$' -and $_.CommandLine -and
            ($_.CommandLine.IndexOf($target, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
             $_.CommandLine.IndexOf($alternateTarget, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        })
    } catch {
        $result.Reason = 'Could not confirm that the output is idle.'
        return [pscustomobject]$result
    }
    if ($active.Count) {
        $result.Reason = 'A running process still references this output.'
        return [pscustomobject]$result
    }

    $compact = Join-Path $env:SystemRoot 'System32/compact.exe'
    if (-not (Test-Path -LiteralPath $compact -PathType Leaf)) { $result.Reason = 'Windows compact utility is unavailable.'; return [pscustomobject]$result }
    $skipExtensions = @('.rom', '.adf', '.hdf', '.ipf', '.copperos', '.zip', '.msix', '.nupkg', '.snupkg', '.7z', '.gz', '.xz', '.png', '.jpg', '.jpeg', '.mp3', '.mp4', '.wav')
    $stack = [Collections.Generic.Stack[string]]::new()
    $stack.Push($target)
    $eligibleFiles = [Collections.Generic.List[string]]::new()
    $considered = 0L
    try {
        while ($stack.Count) {
            $directory = $stack.Pop()
            foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($directory)) {
                $attributes = [IO.File]::GetAttributes($entry)
                if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
                if (($attributes -band [IO.FileAttributes]::Directory) -ne 0) { $stack.Push($entry); continue }
                $file = [IO.FileInfo]::new($entry)
                if ($file.Length -lt 65536 -or $skipExtensions -contains $file.Extension.ToLowerInvariant() -or
                    ($attributes -band ([IO.FileAttributes]::Compressed -bor [IO.FileAttributes]::SparseFile -bor [IO.FileAttributes]::Encrypted)) -ne 0) { continue }
                $considered += $file.Length
                $eligibleFiles.Add($file.FullName)
            }
        }
    } catch {
        $result.Reason = "Output scan failed safely: $($_.Exception.Message)"
        return [pscustomobject]$result
    }

    # Batch path arguments to avoid launching one Windows utility process per file.
    # compact accepts multiple paths; a modest batch stays below command-line limits.
    $compressed = 0
    $failedBatches = 0
    for ($offset = 0; $offset -lt $eligibleFiles.Count; $offset += 64) {
        $end = [Math]::Min($offset + 63, $eligibleFiles.Count - 1)
        $batch = [string[]]$eligibleFiles.GetRange($offset, $end - $offset + 1).ToArray()
        try {
            $null = & $compact /C /A /I /Q $batch 2>&1
            if ($LASTEXITCODE -eq 0) { $compressed += $batch.Count }
            else { $failedBatches++ }
        } catch {
            $failedBatches++
        }
    }
    $result.Status = if ($failedBatches) { 'PARTIAL' } else { 'COMPLETE' }
    $result.FilesCompressed = $compressed
    $result.BytesConsidered = $considered
    $result.Reason = if ($failedBatches) {
        "$failedBatches compression batch(es) could not be confirmed; all source files were retained."
    } else {
        'Completed output compressed losslessly; source media and already-compressed formats were skipped.'
    }
    $report = [IO.Path]::Combine($target, 'artifact-compression.json')
    [IO.File]::WriteAllText($report, ($result | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    return [pscustomobject]$result
}

Export-ModuleMember -Function Compress-CopperScreenCompletedArtifacts
