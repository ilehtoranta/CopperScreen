param(
    [Parameter(Mandatory)][string]$Path,
    [ValidateRange(1, 8)][int]$Width = 3,
    [ValidateRange(1, 255)][int]$WaitPosition = 0x23,
    [switch]$SevenPlanes,
    [switch]$CpuPressure,
    [switch]$Fill,
    [switch]$Overlap,
    [ValidateSet('AD', 'BC')][string]$LiveModulo,
    [ValidateSet('Full', 'Alternate')][string]$LiveMinterm,
    [switch]$LiveData,
    [switch]$Ocs
)

$ErrorActionPreference = 'Stop'
if ($Overlap -and $LiveModulo) { throw 'Choose either the overlap probe or a live modulo probe.' }
if ($LiveMinterm -and ($Overlap -or $LiveModulo)) {
    throw 'Choose one probe for overlap, live modulo or live minterm writes.'
}
if ($LiveData -and ($Overlap -or $LiveModulo -or $LiveMinterm)) {
    throw 'Choose one probe for overlap, live modulo, live minterm or live data writes.'
}

$asm = [Collections.Generic.List[uint16]]::new()
$copper = [Collections.Generic.List[uint16]]::new()
function Word([uint16]$Value) { $asm.Add($Value) }
function WriteWord([uint16]$Value, [uint32]$Address) {
    Word 0x33FC; Word $Value; Word ($Address -shr 16); Word ($Address -band 0xFFFF)
}
function WriteCopperWord([uint16]$Register, [uint16]$Value) { $copper.Add($Register); $copper.Add($Value) }
function Pointer([uint16]$Register, [uint32]$Address) {
    WriteCopperWord $Register ($Address -shr 16); WriteCopperWord ($Register + 2) ($Address -band 0xFFFF)
}

Word 0x2C78; Word 0x0004          # MOVEA.L $4.W,A6
Word 0x4BFA; Word 0x0006         # LEA supervisorCode(PC),A5
Word 0x4EAE; Word 0xFFE2         # JSR Supervisor(A6)
Word 0x46FC; Word 0x2700         # MOVE.W #$2700,SR
if (!$Ocs) {
    Word $(if ($CpuPressure) { 0x7000 } else { 0x7001 })
    Word 0x4E7B; Word 0x0002     # MOVEC D0,CACR
}
WriteWord 0x7FFF 0xDFF09A
WriteWord 0x7FFF 0xDFF09C
WriteWord 0x7FFF 0xDFF096
WriteWord 0x8002 0xDFF02E

if ($SevenPlanes) {
    WriteCopperWord 0x008E 0x2C81
    WriteCopperWord 0x0090 0xF4C1
    WriteCopperWord 0x0092 0x0038
    WriteCopperWord 0x0094 0x00D0
    WriteCopperWord 0x0100 0x7401
    WriteCopperWord 0x01FC 0
    for ($plane = 0; $plane -lt 7; $plane++) { Pointer (0x00E0 + 4 * $plane) (0x20000 + 0x4000 * $plane) }
}
WriteCopperWord 0x0042 $(if ($Fill) { 0x0012 } else { 0 })
WriteCopperWord 0x0044 0xFFFF
WriteCopperWord 0x0046 0xFFFF
foreach ($register in 0x0060, 0x0062, 0x0064, 0x0066) { WriteCopperWord $register 0 }
foreach ($register in 0x0048, 0x004C, 0x0050, 0x0054) { WriteCopperWord $register 1 }

if ($LiveData) {
    # D-only area blits use a manual source latch with that channel's DMA disabled.
    for ($test = 0; $test -lt 12; $test++) {
        $line = (64 + 6 * $test) -shl 8
        $target = 0x16100 + 0x200 * $test
        $bank = $test -shr 2
        $control = (0x31F0, 0x01CC, 0x01AA)[$bank]
        $control1 = (0x0000, 0x5000, 0x0000)[$bank]
        $dataRegister = (0x0074, 0x0072, 0x0070)[$bank]
        WriteCopperWord ($line + 1) 0xFFFE
        WriteCopperWord 0x0040 $control
        WriteCopperWord 0x0042 $control1
        WriteCopperWord 0x0044 0xFF0F
        WriteCopperWord 0x0046 0xF0FF
        WriteCopperWord $dataRegister 0x1234
        WriteCopperWord 0x0056 ($target -band 0xFFFF)
        WriteCopperWord 0x009C 0x0040
        WriteCopperWord ($line + 0x29) 0xFFFE
        WriteCopperWord 0x0058 0x0104
        WriteCopperWord ($line + 0x35 + ($test % 4) * 2) 0xFFFE
        WriteCopperWord $dataRegister 0xABCD
    }
} elseif ($LiveMinterm) {
    # Sweep control writes over A+D, shifted ABCD and descending fill.
    $source = 0x24000
    foreach ($register in 0x0048, 0x004C, 0x0050) { WriteCopperWord $register 2 }
    $patterns = @(0xA105, 0x0248, 0x5A00, 0x1030, 0x8000, 0x0001, 0x44C0, 0x0091,
                  0x0200, 0xA00A, 0x6000, 0x0016, 0x1001, 0x0814, 0x0004, 0x5000)
    for ($i = 0; $i -lt $patterns.Count; $i++) { WriteWord $patterns[$i] ($source + 2 * $i) }

    for ($test = 0; $test -lt 12; $test++) {
        $line = (64 + 6 * $test) -shl 8
        $target = 0x16100 + 0x200 * $test
        $bank = $test -shr 2
        $control = (0x09FF, 0x3FCC, 0x09F0)[$bank]
        $next = (0x0900, 0x3FF0, 0x090F)[$bank]
        $offset = if ($bank -eq 2) { 30 } else { 0 }
        WriteCopperWord ($line + 1) 0xFFFE
        WriteCopperWord 0x0040 $control
        WriteCopperWord 0x0042 ((0, 0x5000, 0x0012)[$bank])
        WriteCopperWord 0x0052 (($source + $offset) -band 0xFFFF)
        WriteCopperWord 0x004E ($source -band 0xFFFF)
        WriteCopperWord 0x004A ($source -band 0xFFFF)
        WriteCopperWord 0x0056 (($target + $offset) -band 0xFFFF)
        WriteCopperWord 0x009C 0x0040
        WriteCopperWord ($line + 0x2B) 0xFFFE
        WriteCopperWord 0x0058 0x0104
        WriteCopperWord ($line + (0x39, 0x3F, 0x3B)[$bank] + ($test % 4) * 2) 0xFFFE
        $register = if (!$Ocs -and $LiveMinterm -eq 'Alternate' -and ($test % 2) -eq 1) {
            0x005A
        } else {
            0x0040
        }
        WriteCopperWord $register $next
    }
} elseif ($LiveModulo) {
    # Four rows per blit expose writes before and after the first row boundary.
    $allSources = $LiveModulo -eq 'BC'
    for ($test = 0; $test -lt 12; $test++) {
        $line = (64 + 6 * $test) -shl 8
        $data = 0x16000 + $(if ($allSources) { 0x400 } else { 0x200 }) * $test
        WriteCopperWord ($line + 1) 0xFFFE
        if ($allSources) {
            WriteCopperWord 0x0040 0x0FF0
            WriteCopperWord 0x0052 ($data -band 0xFFFF)
            WriteCopperWord 0x004E (($data + 0x100) -band 0xFFFF)
            WriteCopperWord 0x004A (($data + 0x200) -band 0xFFFF)
            WriteCopperWord 0x0056 (($data + 0x300) -band 0xFFFF)
            WriteCopperWord 0x0062 2
            WriteCopperWord 0x0060 4
            $firstModulo = 0x0062; $secondModulo = 0x0060
            $writeBeam = 0x31
        } else {
            WriteCopperWord 0x0040 0x09F0
            WriteCopperWord 0x0052 ($data -band 0xFFFF)
            WriteCopperWord 0x0056 (($data + 0x100) -band 0xFFFF)
            WriteCopperWord 0x0064 2
            WriteCopperWord 0x0066 4
            $firstModulo = 0x0064; $secondModulo = 0x0066
            $writeBeam = 0x29
        }
        WriteCopperWord 0x009C 0x0040
        WriteCopperWord ($line + 0x23) 0xFFFE
        WriteCopperWord 0x0058 0x0104
        WriteCopperWord ($line + $writeBeam + ($test % 6) * 4) 0xFFFE
        WriteCopperWord $(if ($test -lt 6) { $firstModulo } else { $secondModulo }) 0x0010
    }
} else {
    $channels = if ($Overlap) { @(9, 15) } else { 0..15 }
    foreach ($mode in $channels) {
        $line = 64 + 6 * $mode
        $data = 0x16000 + 0x80 * $mode
        $d = if ($Overlap) { $data + 2 } else { $data + 0x60 }
        WriteCopperWord (($line -shl 8) + 1) 0xFFFE
        WriteCopperWord 0x0040 (($mode -shl 8) + 0x00F0)
        WriteCopperWord 0x0052 ($data -band 0xFFFF)
        WriteCopperWord 0x004E (($data + 0x20) -band 0xFFFF)
        WriteCopperWord 0x004A (($data + 0x40) -band 0xFFFF)
        WriteCopperWord 0x0056 ($d -band 0xFFFF)
        WriteCopperWord 0x009C 0x0040
        WriteCopperWord (($line -shl 8) + ($WaitPosition -bor 1)) 0xFFFE
        WriteCopperWord 0x0058 (0x0040 + $Width)
        if ($Overlap) {
            WriteWord 0x1111 $data
            WriteWord 0x2222 ($data + 2)
            WriteWord 0x3333 ($data + 4)
            WriteWord 0x4444 ($data + 6)
        }
    }
}

$listBase = 0x10000
if ($Overlap) {
    $endAddress = $listBase + 2 * ($copper.Count + 4)
    Pointer 0x0080 $endAddress
}
WriteCopperWord 0xFFFF 0xFFFE

Word 0x41FA                      # LEA copperList(PC),A0
$offsetIndex = $asm.Count
Word 0
Word 0x227C; Word 1; Word 0      # MOVEA.L #$10000,A1
Word 0x303C; Word ($copper.Count - 1)
Word 0x32D8                      # MOVE.W (A0)+,(A1)+
Word 0x51C8; Word 0xFFFC         # DBF D0 to MOVE.W
WriteWord ($listBase -shr 16) 0xDFF080
WriteWord ($listBase -band 0xFFFF) 0xDFF082
WriteWord 0 0xDFF088
WriteWord 0xC040 0xDFF09A
WriteWord $(if ($SevenPlanes) { 0x83C0 } else { 0x82C0 }) 0xDFF096
if ($CpuPressure) {
    Word 0x207C; Word 4; Word 0  # MOVEA.L #$40000,A0
    Word 0x3010; Word 0x60FC     # Repeat MOVE.W (A0),D0
} else {
    Word 0x4E72; Word 0x2700     # STOP #$2700, no recurring CPU chip bus reads
}
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
