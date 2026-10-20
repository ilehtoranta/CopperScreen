[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunnerPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runner = (Resolve-Path -LiteralPath $RunnerPath).Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('copper-ffs-cli-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $scratch
function Invoke-Case([string]$Name, [string[]]$Arguments, [string]$Expected, [bool]$Success) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.ArgumentList.Add($runner)
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw "$Name timed out" }
    $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    $code = $process.ExitCode
    $process.Dispose()
    if (($code -eq 0) -ne $Success -or !$output.Contains($Expected)) { throw "$Name failed (exit $code): $output" }
    Write-Output "PASS $Name"
}
try {
    $image = Join-Path $scratch 'image with spaces.hdf'
    [IO.File]::WriteAllBytes($image, [byte[]]::new(1933312))
    # Original host configuration fixture only; this does not execute CopperStart.
    $rom = Join-Path $scratch 'fixture.rom'
    $bytes = [byte[]]::new(524288)
    $bytes[1] = 7; $bytes[5] = 0xF8; $bytes[6] = 1; $bytes[256] = 0x60; $bytes[257] = 0xFE
    [IO.File]::WriteAllBytes($rom, $bytes)
    $base = @('--rom', $rom, '--frames', '0')
    Invoke-Case 'read-only explicit partition' ($base + @('--hdf-ffs', $image, 'DH0:2:32:4:58')) 'readOnly=True startBytes=131072 partitionBytes=1802240' $true
    Invoke-Case 'writable explicit partition' ($base + @('--hdf-ffs-writable', $image, 'DH0:2:32:4:58')) 'readOnly=False startBytes=131072 partitionBytes=1802240' $true
    Invoke-Case 'missing geometry' ($base + @('--hdf-ffs', $image)) 'requires an image path' $false
    Invoke-Case 'zero heads' ($base + @('--hdf-ffs', $image, 'DH0:0:32:4:58')) 'Enter heads' $false
    Invoke-Case 'reversed cylinders' ($base + @('--hdf-ffs', $image, 'DH0:2:32:58:4')) 'Enter heads' $false
    Invoke-Case '4 GiB endpoint needs full backing' ($base + @('--hdf-ffs', $image, 'DH0:2:32:0:131071')) 'entire configured FFS partition' $false
    Invoke-Case 'overflowing geometry' ($base + @('--hdf-ffs', $image, 'DH0:4294967295:4294967295:0:4294967295')) '32-bit block count' $false
    Invoke-Case 'short backing image' ($base + @('--hdf-ffs', $image, 'DH0:2:32:4:259')) 'entire configured FFS partition' $false
    Invoke-Case 'multiple disks' ($base + @('--hdf-ffs', $image, 'DH0:2:32:4:58', '--hdf-ro', $image)) 'exactly one hard disk' $false
    Invoke-Case 'help' @('--help') 'DEVICE:HEADS:BLOCKS-PER-TRACK:FIRST-CYLINDER:LAST-CYLINDER' $true
    Invoke-Case 'storage snapshots require diagnostic mode' ($base + @('--boot-probe-storage')) 'Boot probe capture options require --boot-probe' $false
} finally {
    # Delete only the exact temporary directory created above.
    if ((Split-Path -Parent ([IO.Path]::GetFullPath($scratch))) -ne ([IO.Path]::GetTempPath()).TrimEnd('\', '/')) { throw 'Unexpected temporary directory' }
    Remove-Item -LiteralPath $scratch -Recurse -Force
}
