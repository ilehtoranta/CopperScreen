param(
    [Parameter(Mandatory)][string]$Path,
    [switch]$DdfStopSweep,
    [switch]$DdfStartSweep
)

$ErrorActionPreference = 'Stop'
if ($DdfStopSweep -and $DdfStartSweep) { throw 'Select one DDF sweep.' }
$ddfSweep = $DdfStopSweep -or $DdfStartSweep
$asm = [Collections.Generic.List[uint16]]::new()
$copper = [Collections.Generic.List[uint16]]::new()
function Word([uint16]$Value) { $asm.Add($Value) }
function WriteWord([uint16]$Value, [uint32]$Address) {
    Word 0x33FC; Word $Value; Word ($Address -shr 16); Word ($Address -band 0xFFFF)
}
function WriteCopperWord([uint16]$Register, [uint16]$Value) { $copper.Add($Register); $copper.Add($Value) }

Word 0x2C78; Word 0x0004          # MOVEA.L $4.W,A6
Word 0x4BFA; Word 0x0006          # LEA supervisorCode(PC),A5
Word 0x4EAE; Word 0xFFE2          # JSR Supervisor(A6)
Word 0x46FC; Word 0x2700          # MOVE.W #$2700,SR
WriteWord 0x7FFF 0xDFF09A
WriteWord 0x7FFF 0xDFF09C
WriteWord 0x7FFF 0xDFF096

WriteCopperWord 0x008E 0x2C81
WriteCopperWord 0x0090 0xB4C1
WriteCopperWord 0x0092 0x0038
WriteCopperWord 0x0094 $(if ($ddfSweep) { 0x0070 } else { 0x0050 })
WriteCopperWord 0x0100 0x2201
WriteCopperWord 0x0106 0x0C00
WriteCopperWord 0x01FC $(if ($ddfSweep) { 0 } else { 3 })
WriteCopperWord 0x00E0 1
WriteCopperWord 0x00E4 1

if ($ddfSweep) {
    # Move the comparator in both directions around its sampling edge.
    if ($DdfStartSweep) {
        $register = 0x0092
        $early = 0x0038
        $late = 0x0050
        $wait = 0x2D
    } else {
        $register = 0x0094
        $early = 0x0048
        $late = 0x0070
        $wait = 0x3D
    }
    for ($test = 0; $test -lt 12; $test++) {
        $line = (64 + 4 * $test) -shl 8
        $bank = 0x2000 + $test * 0x800
        $movesLater = $test -ge 6
        WriteCopperWord ($line + 1) 0xFFFE
        WriteCopperWord $register $(if ($movesLater) { $early } else { $late })
        WriteCopperWord 0x00E2 $bank
        WriteCopperWord 0x00E6 ($bank + 0x400)
        WriteCopperWord 0x0108 0
        WriteCopperWord 0x010A 0
        WriteCopperWord ($line + $wait + ($test % 6) * 2) 0xFFFE
        WriteCopperWord $register $(if ($movesLater) { $late } else { $early })
    }
} else {
    # Separate bitplane banks expose which modulo won on the next scanline.
    for ($test = 0; $test -lt 14; $test++) {
        $line = (64 + 4 * $test) -shl 8
        $bank = 0x2000 + $test * 0x800
        WriteCopperWord ($line + 1) 0xFFFE
        WriteCopperWord 0x00E2 $bank
        WriteCopperWord 0x00E6 ($bank + 0x400)
        WriteCopperWord 0x0108 0
        WriteCopperWord 0x010A 8
        WriteCopperWord ($line + 0x45 + $test * 2) 0xFFFE
        WriteCopperWord 0x0108 0x20
        WriteCopperWord 0x010A 0x70
    }
}
WriteCopperWord 0xFFFF 0xFFFE

Word 0x41FA                      # LEA copperList(PC),A0
$offsetIndex = $asm.Count
Word 0
Word 0x227C; Word 1; Word 0       # MOVEA.L #$10000,A1
Word 0x303C; Word ($copper.Count - 1)
Word 0x32D8                       # MOVE.W (A0)+,(A1)+
Word 0x51C8; Word 0xFFFC          # DBF D0 to MOVE.W
WriteWord 1 0xDFF080
WriteWord 0 0xDFF082
WriteWord 0 0xDFF088
WriteWord 0x8380 0xDFF096
Word 0x4E72; Word 0x2700          # STOP #$2700
$asm[$offsetIndex] = [uint16](2 * ($asm.Count - $offsetIndex))
$asm.AddRange($copper)

if ($asm.Count -gt 506) { throw "Boot code is $($asm.Count * 2) bytes" }
$disk = [byte[]]::new(901120)
[Text.Encoding]::ASCII.GetBytes('DOS').CopyTo($disk, 0)
$disk[10] = 3
$disk[11] = 112
for ($index = 0; $index -lt $asm.Count; $index++) {
    $disk[12 + 2 * $index] = [byte]($asm[$index] -shr 8)
    $disk[13 + 2 * $index] = [byte]($asm[$index] -band 0xFF)
}
$checksum = [uint64]0
for ($index = 0; $index -lt 1024; $index += 4) {
    $word = ([uint64]$disk[$index] -shl 24) -bor ([uint64]$disk[$index + 1] -shl 16) -bor
        ([uint64]$disk[$index + 2] -shl 8) -bor $disk[$index + 3]
    $checksum += $word
    $checksum = ($checksum -band 0xFFFFFFFFL) + ($checksum -shr 32)
}
$checksum = 0xFFFFFFFFL - $checksum
for ($index = 0; $index -lt 4; $index++) {
    $disk[4 + $index] = [byte](($checksum -shr (24 - 8 * $index)) -band 0xFF)
}
[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($Path), $disk)
