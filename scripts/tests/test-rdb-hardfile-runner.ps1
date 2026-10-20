[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunnerPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runner = (Resolve-Path -LiteralPath $RunnerPath).Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('copper-rdb-cli-' + [Guid]::NewGuid().ToString('N'))
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
    $image = Join-Path $scratch 'RDB with spaces.hdf'
    $rdb = [byte[]]::new(512)
    function Put([byte[]]$Bytes, [int]$At, [uint32]$Value) {
        for ($i=0; $i -lt 4; $i++) { $Bytes[$At+$i]=[byte](($Value -shr (24-$i*8)) -band 255) }
    }
    Put $rdb 0 0x5244534B; Put $rdb 4 128; Put $rdb 12 7; Put $rdb 16 512
    foreach ($offset in @(24,28,32,36)) { Put $rdb $offset ([uint32]::MaxValue) }
    Put $rdb 64 1; Put $rdb 68 32; Put $rdb 72 2
    $sum=[uint64]0
    for ($at=0; $at -lt 512; $at+=4) {
        $word=[uint64]0
        for ($i=0; $i -lt 4; $i++) { $word=($word -shl 8)+$rdb[$at+$i] }
        $sum=($sum+$word)%4294967296
    }
    Put $rdb 8 ([uint32]((4294967296-$sum)%4294967296))
    [IO.File]::WriteAllBytes($image,$rdb)
    $before=(Get-FileHash $image).Hash
    # Original configuration fixtures. Zero frames means no guest OS is executed.
    $rom=Join-Path $scratch 'fixture.rom'; $bytes=[byte[]]::new(524288)
    $bytes[1]=7; $bytes[5]=0xF8; $bytes[6]=1; $bytes[256]=0x60; $bytes[257]=0xFE
    [IO.File]::WriteAllBytes($rom,$bytes)
    $base=@('--rom',$rom,'--frames','0')
    Invoke-Case 'read-only RDB default' ($base+@('--hdf-rdb',$image)) 'layout=RDB readOnly=True partitions=from-image' $true
    Invoke-Case 'writable RDB selection' ($base+@('--hdf-rdb-writable',$image)) 'layout=RDB readOnly=False partitions=from-image' $true
    Invoke-Case 'missing RDB argument' ($base+@('--hdf-rdb')) 'requires an existing RDB image path' $false
    Invoke-Case 'missing native ROM' @('--hdf-rdb',$image,'--frames','0') 'requires a native --rom' $false
    $invalid=Join-Path $scratch 'not-rdb.hdf'; [IO.File]::WriteAllBytes($invalid,[byte[]]::new(512))
    Invoke-Case 'no automatic fallback' ($base+@('--hdf-rdb',$invalid)) 'no valid RDB' $false
    Invoke-Case 'missing RDB image' ($base+@('--hdf-rdb',(Join-Path $scratch 'missing.hdf'))) 'not found' $false
    [IO.File]::WriteAllBytes($invalid,[byte[]]::new(513))
    Invoke-Case 'unaligned image' ($base+@('--hdf-rdb',$invalid)) 'multiple of 512' $false
    Invoke-Case 'RDB help' @('--help') '--hdf-rdb <existing-image>' $true
    if ((Get-FileHash $image).Hash -ne $before) { throw 'Configuration checks changed the RDB image.' }
} finally {
    # Delete only the exact temporary directory created above.
    if ((Split-Path -Parent ([IO.Path]::GetFullPath($scratch))) -ne ([IO.Path]::GetTempPath()).TrimEnd('\', '/')) { throw 'Unexpected temporary directory' }
    Remove-Item -LiteralPath $scratch -Recurse -Force
}
