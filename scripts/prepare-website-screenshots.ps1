[CmdletBinding()]
param([Parameter(Mandatory)][string]$CaptureRoot)

# Reproduce the application's full PAL LCD viewport from these 908x313 beam captures.
# This removes hardware blanking, not game overscan, and doubles field rows exactly.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repository = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repository 'docs/assets/screenshots'
$null = New-Item -ItemType Directory -Path $output -Force
$captures = @(
    @{ Name = 'lemmings-gameplay'; File = 'hiredguns-sprite-repaired-probe/lemmings/frame-014520.bmp' },
    @{ Name = 'hired-guns-gameplay'; File = 'hiredguns-sprite-scheduled-probe/final-training/frame-020648.bmp' },
    @{ Name = 'hired-guns-menu'; File = 'hiredguns-sprite-repaired-probe/menu/frame-009000.bmp' }
)
foreach ($capture in $captures) {
    $source = [Drawing.Bitmap]::new((Join-Path $CaptureRoot $capture.File))
    try {
        if ($source.Width -ne 908 -or $source.Height -ne 313) { throw 'Unexpected capture geometry' }
        foreach ($thumbnail in @($false, $true)) {
            $width = if ($thumbnail) { 356 } else { 712 }
            $height = 570 * $width / 712
            $result = [Drawing.Bitmap]::new([int]$width, [int]$height)
            try {
                for ($y = 0; $y -lt $height; $y++) {
                    for ($x = 0; $x -lt $width; $x++) {
                        $sx = 196 + [int][Math]::Floor($x * 712 / $width)
                        $sy = 26 + [int][Math]::Floor($y * 285 / $height)
                        $result.SetPixel($x, $y, $source.GetPixel($sx, $sy))
                    }
                }
                $suffix = if ($thumbnail) { '-thumb' } else { '' }
                $result.Save((Join-Path $output ($capture.Name + $suffix + '.png')), [Drawing.Imaging.ImageFormat]::Png)
            } finally { $result.Dispose() }
        }
        Write-Output "$($capture.Name): $((Get-FileHash -LiteralPath (Join-Path $CaptureRoot $capture.File)).Hash)"
    } finally { $source.Dispose() }
}
