param([Parameter(Mandatory)][string]$Path)

$ErrorActionPreference = 'Stop'
$words = [Collections.Generic.List[uint16]]::new()
function Word([uint16]$value) { $words.Add($value) }
function WriteWord([uint16]$value, [uint32]$address) {
    Word 0x33FC; Word $value; Word ($address -shr 16); Word ($address -band 0xFFFF)
}

Word 0x2C78; Word 0x0004    # MOVEA.L $4.W,A6
Word 0x4BFA; Word 0x0006    # LEA supervisor(PC),A5
Word 0x4EAE; Word 0xFFE2    # JSR Supervisor(A6)
Word 0x46FC; Word 0x2700    # MOVE.W #$2700,SR
WriteWord 0x7FFF 0xDFF09A
WriteWord 0x7FFF 0xDFF09C
Word 0x41FA                # LEA handler(PC),A0
$handler = $words.Count
Word 0
Word 0x21C8; Word 0x006C    # MOVE.L A0,$6C.W
Word 0x42B9; Word 7; Word 0 # CLR.L $70000
WriteWord 0xC020 0xDFF09A
Word 0x46FC; Word 0x2000

Word 0x0839; Word 5; Word 0x00DF; Word 0xF01F # BTST.B #5,$DFF01F
Word 0x67F6                 # BEQ poll
WriteWord 0x0020 0xDFF09C
Word 0x5279; Word 7; Word 0 # ADDQ.W #1,$70000
Word 0x60E6                 # BRA poll

$words[$handler] = [uint16](2 * ($words.Count - $handler))
WriteWord 0x0020 0xDFF09C
Word 0x5279; Word 7; Word 2 # ADDQ.W #1,$70002
Word 0x4E73                 # RTE

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
