#requires -Version 5.1
<#
.SYNOPSIS
Runs the four initial utility scenarios on the bounded PAL A500 configuration.
.DESCRIPTION
Writes diagnostic captures and identities, not automated compatibility verdicts
or performance acceptance. Omit both CopperStart parameters to record unavailable
paired coverage. DevelopmentCopperStartRomPath explicitly selects an unqualified
rebuild with its matching build manifest, without changing app firmware pins.
Uses protected DF0 only.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$NativeRomPath,
    [string]$CopperStartRomPath,
    [string]$DevelopmentCopperStartRomPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Both', 'Native', 'CopperStart')][string]$RomSelection = 'Both',
    [ValidateRange(7200, 100000)][int]$Frames = 7200,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence directory.' }
$nativeRom = (Resolve-Path -LiteralPath $NativeRomPath).Path
$nativeHash = (Get-FileHash -LiteralPath $nativeRom).Hash.ToLowerInvariant()
if ((Get-Item -LiteralPath $nativeRom).Length -ne 262144 -or
    $nativeHash -ne 'ee05862d8102a08436ac4056da7d549db31625c7d47b24dfb7b3c9a5c113ca53') {
    throw 'This protocol requires the documented native Kickstart 1.3 reference ROM.'
}
$firmware = Get-Content (Join-Path $repository 'CopperScreen/Firmware/CopperStart.json') -Raw | ConvertFrom-Json
if ($CopperStartRomPath -and $DevelopmentCopperStartRomPath) { throw 'Choose either the qualified ROM or an explicitly identified development rebuild.' }
$roms = @([pscustomobject]@{ id = 'native'; path = $nativeRom; sha256 = $nativeHash })
$copperCoverage = 'UNAVAILABLE: exact qualified A500 ROM not supplied'
if ($CopperStartRomPath) {
    $copperRom = (Resolve-Path -LiteralPath $CopperStartRomPath).Path
    $copperHash = (Get-FileHash -LiteralPath $copperRom).Hash.ToLowerInvariant()
    if ((Get-Item -LiteralPath $copperRom).Length -ne $firmware.bytes -or $copperHash -ne $firmware.sha256) {
        throw 'Supply the exact qualified A500 CopperStart ROM. Other ROMs need a separate protocol.'
    }
    $roms += [pscustomobject]@{ id = 'copperstart'; path = $copperRom; sha256 = $copperHash }
    $copperCoverage = 'QUALIFIED_ROM_EXECUTED_REQUIRES_REVIEW'
}
if ($DevelopmentCopperStartRomPath) {
    $copperRom = (Resolve-Path -LiteralPath $DevelopmentCopperStartRomPath).Path
    $copperHash = (Get-FileHash -LiteralPath $copperRom).Hash.ToLowerInvariant()
    $rebuildManifestPath = Join-Path (Split-Path -Parent $copperRom) 'disk-boot-manifest.json'
    $rebuild = Get-Content -LiteralPath $rebuildManifestPath -Raw | ConvertFrom-Json
    if ((Get-Item -LiteralPath $copperRom).Length -ne 524288 -or $copperHash -ne $rebuild.sha256 -or
        $rebuild.fileSystemProfile -ne 'Classic' -or $rebuild.a1200 -or $rebuild.storageQualification -or $rebuild.extensionRom) {
        throw 'Development replay requires a matching standalone 512 KiB A500 Classic build manifest.'
    }
    $roms += [pscustomobject]@{ id = 'development-copperstart'; path = $copperRom; sha256 = $copperHash }
    $copperCoverage = 'UNQUALIFIED_DEVELOPMENT_REBUILD_EXECUTED_REQUIRES_REVIEW'
}
if ($RomSelection -eq 'Native') {
    $roms = @($roms | Where-Object id -eq 'native')
    $copperCoverage = 'NOT_RUN: native-only selection'
}
elseif ($RomSelection -eq 'CopperStart') {
    $roms = @($roms | Where-Object id -ne 'native')
    if (-not $roms.Count) { throw 'CopperStart selection requires a qualified or explicit development ROM.' }
}
$cases = @(
    @{ id = 'protracker'; utility = 'protracker-23f'; disk = 'media/utilities/originals/PT23F.ADF'; scenario = 'utilities-protracker-editor-v1.json' },
    @{ id = 'xcopy'; utility = 'xcopy-521'; disk = 'media/utilities/ready/xcopy_v5.21_en_1991-11-17_c911/xcopy_v5.21_en_1991-11-17_c911.adf'; scenario = 'utilities-xcopy-launch-v1.json' },
    @{ id = 'testkit'; utility = 'amiga-test-kit'; disk = 'media/utilities/ready/AmigaTestKit-1.21/AmigaTestKit.adf'; scenario = 'utilities-testkit-audio-v2.json' },
    @{ id = 'sysinfo'; utility = 'sysinfo-44'; disk = 'media/utilities/originals/sysinfo_4.4.adf'; scenario = 'utilities-sysinfo-speed-v2.json' }
)
Push-Location $repository
try {
    & (Join-Path $PSScriptRoot 'get-utility-test-media.ps1') -VerifyOnly -Id @($cases.utility)
    New-Item -ItemType Directory -Path $output | Out-Null
    if (-not $SkipBuild) {
        & dotnet build CopperScreen.slnx -c Release *> (Join-Path $output 'build.log')
        if ($LASTEXITCODE -ne 0) { throw "Build failed; inspect $output/build.log" }
    }
    $runnerDirectory = Join-Path $repository 'CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0'
    $runner = Join-Path $runnerDirectory 'CopperMod.Amiga.Lightweight.Runner.exe'
    $binaries = @('CopperMod.Amiga.Lightweight.Runner.dll', 'CopperMod.Amiga.Lightweight.dll', 'Copper68k.dll', 'CopperDisk.dll') |
        ForEach-Object { @{ file = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $runnerDirectory $_)).Hash.ToLowerInvariant() } }
    $results = @()
    foreach ($rom in $roms) {
        foreach ($case in $cases) {
            $disk = Join-Path $repository $case.disk
            $script = Join-Path $repository "CopperMod.Amiga.Lightweight.Runner/Workloads/$($case.scenario)"
            $before = (Get-FileHash -LiteralPath $disk).Hash.ToLowerInvariant()
            $runDirectory = Join-Path $output "$($rom.id)-$($case.id)"
            $arguments = @('--rom', $rom.path, '--disk', $disk, '--frames', $Frames,
                '--cpu', '68000', '--chip-ram-kib', '512', '--slow-ram-kib', '512', '--fast-ram-kib', '0',
                '--agnus', '8371', '--denise', '8362', '--drives', '1', '--boot-probe', $runDirectory,
                '--boot-probe-extended', '--boot-probe-interval', '100', '--input-script', $script)
            Write-Host "Running $($rom.id) / $($case.id), $Frames fields..."
            & $runner @arguments *> "$runDirectory.log"
            $exitCode = $LASTEXITCODE
            $after = (Get-FileHash -LiteralPath $disk).Hash.ToLowerInvariant()
            $completed = Test-Path -LiteralPath (Join-Path $runDirectory ('frame-{0:D6}.json' -f $Frames))
            $results += [ordered]@{
                rom = $rom.id; utility = $case.utility; exitCode = $exitCode
                execution = $(if ($exitCode -eq 0 -and $completed) { 'COMPLETED_REQUIRES_REVIEW' } else { 'INCOMPLETE' })
                romSha256 = $rom.sha256; disk = $case.disk; diskSha256Before = $before; diskSha256After = $after
                mediaUnchanged = ($before -eq $after); inputScript = $case.scenario
                inputScriptSha256 = (Get-FileHash -LiteralPath $script).Hash.ToLowerInvariant()
                arguments = $arguments; evidenceDirectory = $runDirectory
            }
            if ($before -ne $after) { throw 'Protected media changed; inspect the evidence before continuing.' }
            $results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'runs.json') -Encoding utf8
        }
    }
    [ordered]@{
        protocol = 'amiga-utilities-a500-exploratory-v2'; createdUtc = [DateTime]::UtcNow.ToString('o')
        gitHead = (& git rev-parse HEAD); workingTreeDirty = (@(& git status --porcelain).Count -gt 0)
        build = $(if ($SkipBuild) { 'EXISTING_RELEASE_OUTPUT' } else { 'RELEASE_BUILD_PASSED' })
        binaries = $binaries; collection = 'amiga-utilities-v1'; framesPerRun = $Frames
        romSelection = $RomSelection
        configuration = 'PAL 68000 interpreter, conservative-batch, OCS 8371/8362, 512 KiB Chip + 512 KiB slow, no Fast/RTC/host directories, one protected DD DF0'
        copperStartQualificationId = $firmware.qualificationId
        copperStartCoverage = $copperCoverage
        developmentBuildManifest = $(if ($DevelopmentCopperStartRomPath) { $rebuildManifestPath } else { $null })
        results = $results; scope = 'Exploratory runner diagnostics. Not app UI, hardware conformance, full utility qualification or host performance acceptance.'
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'qualification.json') -Encoding utf8
    Write-Host "Evidence: $output. Review captures and individual operations before assigning compatibility outcomes."
}
finally { Pop-Location }
