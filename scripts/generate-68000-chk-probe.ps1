param([Parameter(Mandatory)][string]$Path)

$ErrorActionPreference = 'Stop'
$words = [Collections.Generic.List[uint16]]::new()
function Word([uint16]$value) { $words.Add($value) }
function Set-Displacement([int]$index) {
    $words[$index] = [uint16](2 * ($words.Count - $index))
}

Word 0x2C78; Word 0x0004    # MOVEA.L $4.W,A6
Word 0x4BFA; Word 0x0006    # LEA supervisor(PC),A5
Word 0x4EAE; Word 0xFFE2    # JSR Supervisor(A6)
Word 0x46FC; Word 0x2700    # MOVE.W #$2700,SR
Word 0x41FA                # LEA handler(PC),A0
$handler = $words.Count
Word 0
Word 0x21C8; Word 0x0018    # MOVE.L A0,$18.W
Word 0x247C; Word 0x0007; Word 0 # MOVEA.L #$70000,A2

foreach ($value in 0xFF, 6, 5) {
    Word (0x7000 -bor $value) # MOVEQ #value,D0
    Word 0x43FA              # LEA resume(PC),A1
    $resume = $words.Count
    Word 0
    Word 0x41BC; Word 5       # CHK.W #5,D0
    Set-Displacement $resume
}
Word 0x34FC; Word 0xBEEF    # MOVE.W #$BEEF,(A2)+
Word 0x4E72; Word 0x2700    # STOP #$2700

Set-Displacement $handler
Word 0x34D7                # MOVE.W (A7),(A2)+
Word 0x24EF; Word 2         # MOVE.L 2(A7),(A2)+
Word 0x24C9                # MOVE.L A1,(A2)+
Word 0x4E73                # RTE

$disk = [byte[]]::new(901120)
[Text.Encoding]::ASCII.GetBytes('DOS').CopyTo($disk, 0)
$disk[10] = 3
$disk[11] = 112
for ($index = 0; $index -lt $words.Count; $index++) {
    $disk[12 + 2 * $index] = [byte]($words[$index] -shr 8)
    $disk[13 + 2 * $index] = [byte]($words[$index] -band 0xFF)
}
$checksum = [uint64]0
for ($index = 0; $index -lt 1024; $index += 4) {
    $dword = ([uint64]$disk[$index] -shl 24) -bor ([uint64]$disk[$index + 1] -shl 16) -bor
        ([uint64]$disk[$index + 2] -shl 8) -bor $disk[$index + 3]
    $checksum += $dword
    $checksum = ($checksum -band 0xFFFFFFFFL) + ($checksum -shr 32)
}
$checksum = 0xFFFFFFFFL - $checksum
for ($index = 0; $index -lt 4; $index++) {
    $disk[4 + $index] = [byte](($checksum -shr (24 - 8 * $index)) -band 0xFF)
}
[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($Path), $disk)
