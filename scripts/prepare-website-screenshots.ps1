[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CaptureRoot,
    [string[]]$Names
)

# Reproduce the application's full PAL LCD viewport from these 908x313 beam captures.
# This removes hardware blanking, not game overscan, and doubles field rows exactly.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repository = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repository 'docs/assets/screenshots'
$null = New-Item -ItemType Directory -Path $output -Force
$captures = @(
    @{ Name = 'superfrog-attract'; File = 'wordsync-investigation/superfrog-boot/frame-007320.bmp'; Hash = 'AA69C97B55A113AF7AB36856B2BBCE8B40A642A5119E18E47B881DAB0FDA1389' },
    @{ Name = 'f17-challenge-race'; File = 'wordsync-investigation/f17-play/frame-018000.bmp'; Hash = '84569DF5A097362ABDCD3C2A92F0CB69CC63A3609E6596FA290EDEAEA4E6954A' },
    @{ Name = 'overdrive-race-selection'; File = 'wordsync-investigation/overdrive-disk2/frame-015300.bmp'; Hash = 'CBB1C31E590CF13649349C8B7435258A7883A2E2FE4A7D764E5BD2733C573329' },
    @{ Name = 'lemmings-gameplay'; File = 'inside-machine-investigation/lemmings-candidate/frame-014520.bmp'; Hash = '0919509F56690100BF0E74604EE95E55C3504DCF9007BD98F8E8565A9A146D58' },
    @{ Name = 'full-contact-gameplay'; File = 'storage-2026-09-18/ipf-fullcontact-gameplay/frame-012000.bmp'; Hash = '3EC6CDD7135609F8EA6602BF8B1C8556866D3E343C50D02F3EE91144D0FEAEBC' },
    @{ Name = 'shadow-of-the-beast-gameplay'; File = 'beast-border-2026-09-18/native/frame-018120.bmp'; Hash = '1661C0201823DBA6BE9E4D233DB18BC8ED6A9F378F0FC4B33135FD71370806D8' },
    @{ Name = 'operation-thunderbolt-gameplay'; File = 'trace-exception-2026-09-18/thunderbolt-input/frame-007980.bmp'; Hash = '6C473C0B0F79CEC0874CCB5AA59F3888C1CE8723FACD73C0D1F4F510EDE88A08' },
    @{ Name = 'lotus-turbo-challenge-2-gameplay'; File = 'game-corpus-2026-09-20/lotus2-gameplay/frame-013020.bmp'; Hash = '67E80A46F06F344D40735E1E390165FE176A8D692D7A95B5EC3BA293057444EE' },
    @{ Name = 'apidya-gameplay'; File = 'game-corpus-2026-09-20/apidya-gameplay/frame-008820.bmp'; Hash = '27DB1B829CE77D1AF43EF47B4B4438EDF917DD260EC3EE3035190A1EB2A5DCE7' },
    @{ Name = 'north-and-south-menu'; File = 'north-south-investigation/cp-candidate/frame-008500.bmp'; Hash = '8BD3B202C1BDDE9928E08D9DF279B3E0E93FAE366BE45456E367129FA6896862' },
    @{ Name = 'super-cars-ii-gameplay'; File = 'supercars2-investigation/driving-fixed/frame-019000.bmp'; Hash = '091D8461C89A76B378284BCDD3F67A5FED1B92C276185A2BCF766A66EBE54439' },
    @{ Name = 'major-motion-gameplay'; File = 'native-corpus-2026-09-21/major-motion-play/frame-006960.bmp'; Hash = '83815F816A4A1B653DF5C7F0462C27045A59B9B6478089A220B3F75E3F4160F3' },
    @{ Name = 'alien-breed-gameplay'; File = 'native-corpus-2026-09-21/alien-breed-gameplay/frame-016380.bmp'; Hash = '9577B2BF341D4EE094D1E7DBF5766EE0ECF19157A3D1A6AAFDCA33761E3E00CA' },
    @{ Name = 'inside-the-machine-face'; File = 'inside-machine-investigation/full-demo/frame-003840.bmp'; Hash = '14414E2A9E9859B4C350FE0B8816B1B1BBE943B0056622EE0510C6D8335AD0EF' },
    @{ Name = 'miami-chase-menu'; File = 'inside-machine-investigation/miami-chase/frame-018000.bmp'; Hash = 'AD116E700066DBE60D3FDBF664C9ED2583E8D4E4F06BF21C3281099A06AF3A21' },
    @{ Name = 'arte-flower'; File = 'native-corpus-2026-09-21/arte-boot/frame-008160.bmp'; Hash = '35D88C1F40E70307EA27E1D0177DBC15F2917B366B25EF8EF2D90A46E7010645' },
    @{ Name = 'desert-dream-title'; File = 'native-corpus-2026-09-21/desert-dream-boot/frame-016380.bmp'; Hash = 'E15FE7F8401AB34067C8107FE2344924EFD5436915F4B880B5C265349F34AD9A' }
)
# Hired Guns keeps its committed gameplay PNG/thumbnail: the original capture
# belongs to the earlier machine's evidence and is not present in this checkout.
if ($Names) {
    foreach ($name in $Names) {
        if ($name -notin $captures.Name) { throw "Unknown capture: $name" }
    }
    $captures = @($captures | Where-Object { $_.Name -in $Names })
}
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
