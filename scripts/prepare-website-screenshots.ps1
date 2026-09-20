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
    @{ Name = 'lemmings-gameplay'; File = 'beam-sync-2026-09-19/native-candidate-lemmings/frame-014520.bmp'; Hash = '7F25E37ADADBDE931C93E55EABB4CD05BC1910F2B18887B56BF9009C2D588BBB' },
    @{ Name = 'full-contact-gameplay'; File = 'storage-2026-09-18/ipf-fullcontact-gameplay/frame-012000.bmp'; Hash = '3EC6CDD7135609F8EA6602BF8B1C8556866D3E343C50D02F3EE91144D0FEAEBC' },
    @{ Name = 'shadow-of-the-beast-gameplay'; File = 'beast-border-2026-09-18/native/frame-018120.bmp'; Hash = '1661C0201823DBA6BE9E4D233DB18BC8ED6A9F378F0FC4B33135FD71370806D8' },
    @{ Name = 'operation-thunderbolt-gameplay'; File = 'trace-exception-2026-09-18/thunderbolt-input/frame-007980.bmp'; Hash = '6C473C0B0F79CEC0874CCB5AA59F3888C1CE8723FACD73C0D1F4F510EDE88A08' }
)
# Hired Guns keeps its committed gameplay PNG/thumbnail: the original capture
# belongs to the earlier machine's evidence and is not present in this checkout.
foreach ($capture in $captures) {
    if ((Get-FileHash -LiteralPath (Join-Path $CaptureRoot $capture.File)).Hash -ne $capture.Hash) {
        throw "Unexpected source capture: $($capture.Name)"
    }
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
