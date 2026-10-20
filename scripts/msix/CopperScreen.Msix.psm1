Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-CopperScreenSdkTool {
    param([Parameter(Mandatory)][string]$Name, [string]$WindowsSdkBin)
    if ($WindowsSdkBin) {
        $explicit = Join-Path $WindowsSdkBin $Name
        if (-not (Test-Path -LiteralPath $explicit -PathType Leaf)) { throw "Windows SDK tool not found: $explicit" }
        return (Resolve-Path -LiteralPath $explicit).Path
    }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
    $versions = @(Get-ChildItem -LiteralPath (Join-Path $kits 'bin') -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } | Sort-Object { [version]$_.Name } -Descending)
    foreach ($version in $versions) {
        $candidate = Join-Path $version.FullName "x64/$Name"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    $fallback = Join-Path $kits "App Certification Kit/$Name"
    if (Test-Path -LiteralPath $fallback -PathType Leaf) { return $fallback }
    throw "Install the Windows SDK with $Name, or supply -WindowsSdkBin pointing to its x64 tool directory."
}

function Read-CopperScreenXml {
    param([Parameter(Mandatory)][string]$Text)
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create([IO.StringReader]::new($Text), $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        return ,$document
    }
    finally { $reader.Dispose() }
}

function Get-CopperScreenMsixIdentity {
    param([Parameter(Mandatory)][string]$PackagePath)
    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
    try {
        $entry = $zip.GetEntry('AppxManifest.xml')
        if (-not $entry) { throw "No AppxManifest.xml in $PackagePath" }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $manifest = Read-CopperScreenXml $reader.ReadToEnd() } finally { $reader.Dispose() }
        $identity = $manifest.DocumentElement.SelectSingleNode('*[local-name()="Identity"]')
        [pscustomobject]@{
            Name = $identity.GetAttribute('Name')
            Publisher = $identity.GetAttribute('Publisher')
            Version = [version]$identity.GetAttribute('Version')
            Architecture = $identity.GetAttribute('ProcessorArchitecture')
            Signed = $null -ne $zip.GetEntry('AppxSignature.p7x')
            Diagnostic = $null -ne $zip.GetEntry('installed-test-build.json')
        }
    }
    finally { $zip.Dispose() }
}

function Assert-CopperScreenIdentity {
    param([string]$Name, [string]$Publisher, [string]$Version, [switch]$InstalledTestBuild)
    if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$') { throw 'Package identity must contain 3-50 letters, digits, periods or hyphens.' }
    if ($Publisher -notmatch '^CN=\S' -or $Publisher -match 'Configure Publisher') { throw 'Supply the exact Publisher distinguished name from Partner Center, or a matching test certificate subject.' }
    if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw 'Use a four-part package version with revision zero (for example 1.1.3.0).' }
    $parsed = [version]$Version
    if ($parsed.Major -lt 1 -or @($parsed.Major, $parsed.Minor, $parsed.Build) -gt 65535) { throw 'Package version components must fit 16 bits and major must be nonzero.' }
    $testIdentity = $Name -match '^CopperScreen\.Test\.[a-f0-9]{32}$'
    if ($InstalledTestBuild -and -not $testIdentity) { throw 'Diagnostic packages require a unique CopperScreen.Test.<32 lowercase hex digits> identity.' }
    if (-not $InstalledTestBuild -and $Name.StartsWith('CopperScreen.Test.', [StringComparison]::OrdinalIgnoreCase)) { throw 'Test identities require -InstalledTestBuild and cannot be used for Store artifacts.' }
}

function Assert-CopperScreenUpdatePair {
    param($Baseline, $Update)
    if (-not $Baseline.Diagnostic -or -not $Update.Diagnostic -or $Baseline.Name -notmatch '^CopperScreen\.Test\.[a-f0-9]{32}$') {
        throw 'Installed tests accept only uniquely identified diagnostic packages; never a customer/Store package.'
    }
    foreach ($field in @('Name', 'Publisher', 'Architecture')) {
        if ($Baseline.$field -cne $Update.$field) { throw "Update must preserve $field." }
    }
    if ($Update.Version -le $Baseline.Version) { throw 'Update package version must strictly increase.' }
}

function New-CopperScreenMsixAssets {
    param([string]$IconPath, [string]$Destination)
    Add-Type -AssemblyName System.Drawing
    $null = New-Item -ItemType Directory -Path $Destination -Force
    $icon = [Drawing.Icon]::new($IconPath, [Drawing.Size]::new(256, 256))
    $source = $icon.ToBitmap()
    try {
        foreach ($asset in @(@('StoreLogo.png', 50), @('Square44x44Logo.png', 44), @('Square150x150Logo.png', 150))) {
            $bitmap = [Drawing.Bitmap]::new([int]$asset[1], [int]$asset[1])
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($source, 0, 0, $bitmap.Width, $bitmap.Height)
                $bitmap.Save((Join-Path $Destination $asset[0]), [Drawing.Imaging.ImageFormat]::Png)
            }
            finally { $graphics.Dispose(); $bitmap.Dispose() }
        }
    }
    finally { $source.Dispose(); $icon.Dispose() }
}

function Test-CopperScreenDiagnosticAssembly {
    param([Parameter(Mandatory)][string]$AssemblyPath)
    $stream = [IO.File]::OpenRead($AssemblyPath)
    $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        foreach ($handle in $metadata.TypeDefinitions) {
            $definition = $metadata.GetTypeDefinition($handle)
            if (@('CopperScreenInstalledProbe', 'CopperScreenInstalledMediaProof', 'CopperOsNativeStorageProof', 'CopperScreenInstalledSoak', 'CopperScreenInstalledRdbProof', 'CopperScreenRdbImageProof') -ccontains $metadata.GetString($definition.Name)) { return $true }
        }
        return $false
    }
    finally { $pe.Dispose(); $stream.Dispose() }
}

function Assert-CopperScreenExecutableManifest {
    param([Parameter(Mandatory)][Xml.XmlDocument]$Document)
    $dpi = $Document.SelectSingleNode('//*[local-name()="dpiAwareness" and namespace-uri()="http://schemas.microsoft.com/SMI/2016/WindowsSettings"]')
    $level = $Document.SelectSingleNode('//*[local-name()="requestedExecutionLevel" and namespace-uri()="urn:schemas-microsoft-com:asm.v3"]')
    if (-not $dpi -or $dpi.InnerText.Trim() -cne 'PerMonitorV2' -or -not $level -or
        $level.GetAttribute('level') -cne 'asInvoker' -or $level.GetAttribute('uiAccess') -cne 'false') {
        throw 'The published executable must declare PerMonitorV2 DPI awareness and asInvoker without UI access.'
    }
}

function Get-CopperScreenWackSummary {
    param([Parameter(Mandatory)][Xml.XmlDocument]$Document)
    $tests = @($Document.SelectNodes('/REPORT/REQUIREMENTS/REQUIREMENT/TEST') | ForEach-Object {
        $result = $_.SelectSingleNode('RESULT')
        [pscustomobject]@{
            Name = $_.GetAttribute('NAME'); Optional = $_.GetAttribute('OPTIONAL')
            Result = $(if ($result) { $result.InnerText.Trim() } else { 'MISSING' })
            Messages = @($_.SelectNodes('MESSAGES/MESSAGE') | ForEach-Object { $_.GetAttribute('TEXT') })
        }
    })
    $required = @($tests | Where-Object { $_.Optional -ceq 'FALSE' })
    $unknown = @($tests | Where-Object { $_.Optional -cnotin @('TRUE', 'FALSE') -or $_.Result -cnotin @('PASS', 'FAIL', 'WARNING') })
    [pscustomobject]@{
        OverallResult = $Document.DocumentElement.GetAttribute('OVERALL_RESULT')
        AppType = $Document.DocumentElement.GetAttribute('APP_TYPE')
        AppName = $Document.DocumentElement.GetAttribute('APP_NAME')
        AppVersion = $Document.DocumentElement.GetAttribute('APP_VERSION')
        PartialRun = $Document.DocumentElement.GetAttribute('PARTIAL_RUN')
        RequiredTestsPassed = $required.Count -gt 0 -and $unknown.Count -eq 0 -and
            $Document.DocumentElement.GetAttribute('PARTIAL_RUN') -ceq 'FALSE' -and
            @($required | Where-Object { $_.Result -cne 'PASS' }).Count -eq 0
        TestCount = $tests.Count; Tests = $tests
    }
}

function Assert-CopperScreenWackReport {
    param([Parameter(Mandatory)][Xml.XmlDocument]$Document)
    if (-not $Document.DocumentElement.HasAttribute('OVERALL_RESULT') -or $Document.DocumentElement.GetAttribute('OVERALL_RESULT') -ine 'PASS') {
        throw 'WACK report does not contain an unambiguous overall PASS.'
    }
}

function Wait-CopperScreenProcessExit {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process, [ValidateRange(1, 120)][int]$TimeoutSeconds = 30)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    if (-not $Process.WaitForExit($TimeoutSeconds * 1000)) {
        $Process.Kill()
        throw "Diagnostic process did not exit within $TimeoutSeconds seconds."
    }
    if ($null -eq $Process.ExitCode) { throw 'Diagnostic process exit code is unavailable.' }
    if ($Process.ExitCode -ne 0) { throw "Diagnostic process exited with code $($Process.ExitCode)." }
    [pscustomobject]@{ Id = $Process.Id; ExitCode = $Process.ExitCode; WaitMilliseconds = $timer.ElapsedMilliseconds; TimeoutSeconds = $TimeoutSeconds }
}

function Assert-CopperScreenMediaPhase {
    param([Parameter(Mandatory)]$Report, [Parameter(Mandatory)][string]$Phase)
    if ($Report.Phase -cne $Phase -or -not $Report.MediaMode -or -not $Report.MediaPersistence.Passed -or
        $Report.MediaPersistence.Protocol -cne 'copperscreen-installed-media-v1' -or
        $Report.MediaPersistence.Phase -cne $Phase.Substring(6) -or
        -not $Report.CopperOsStorage.passed -or $Report.CopperOsStorage.engine -cne 'Lightweight' -or
        $Report.CopperOsStorage.legacy -or $Report.CopperOsStorage.hostDosCalls) { throw 'Incomplete P1-06 installed media/native evidence.' }
    $maps = @('Originals', 'Exports')
    if ($Phase -cne 'media-fresh') { $maps += 'PersistentFiles' }
    foreach ($name in $maps) {
        $properties = @($Report.MediaPersistence.$name.PSObject.Properties)
        if (-not $properties.Count) { throw 'Media evidence has an empty file map.' }
        foreach ($file in $properties) {
            if (-not [IO.Path]::IsPathFullyQualified($file.Name) -or $file.Value -cnotmatch '^[0-9a-f]{64}$') { throw 'Invalid media evidence path/hash.' }
            $private = $file.Name.StartsWith($Report.UserDataDirectory.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
            $installed = $file.Name.StartsWith($Report.InstallDirectory.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
            $packageRoot = $file.Name.StartsWith($Report.UserDataRoot.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
            if ($installed -or ($name -ceq 'PersistentFiles' -and -not $private) -or ($name -cne 'PersistentFiles' -and $packageRoot)) { throw 'Media evidence mixed protected exports with package-private or installed files.' }
        }
    }
}

function Assert-CopperScreenMediaUpdate {
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$After)
    if ($Before.UserDataDirectory -cne $After.UserDataDirectory) { throw 'P1-06 update changed the package-private media root.' }
    foreach ($name in @('Originals', 'PersistentFiles', 'Exports')) {
        foreach ($file in $Before.MediaPersistence.$name.PSObject.Properties) {
            $later = $After.MediaPersistence.$name.PSObject.Properties[$file.Name]
            if ($null -eq $later -or $later.Value -cne $file.Value) { throw 'Restart/update changed a protected original, persisted workspace or export.' }
        }
    }
}

function Assert-CopperScreenLauncherLayout {
    param([Parameter(Mandatory)]$Report)
    if ($Report.protocol -cne 'copperscreen-installed-launcher-layout-v1' -or -not $Report.passed -or
        $Report.bootMethod -cne 'CopperOS' -or @($Report.cases).Count -ne 3) { throw 'Incomplete installed CopperOS launcher evidence.' }
    $sizes = @('800x600', '1280x720', '960x640')
    for ($index = 0; $index -lt 3; $index++) {
        $case = $Report.cases[$index]
        foreach ($name in @('requestedWidth','requestedHeight','clientWidth','clientHeight','renderScaling','viewportX','viewportY',
            'viewportWidth','viewportHeight','extentWidth','extentHeight','offsetY','targetX','targetY','targetWidth','targetHeight','targetInScrollY')) {
            if ($null -eq $case.PSObject.Properties[$name] -or $null -eq $case.$name -or
                -not [double]::IsFinite([double]$case.$name)) { throw 'Missing or invalid installed layout measurement.' }
        }
        if ("$($case.requestedWidth)x$($case.requestedHeight)" -cne $sizes[$index] -or
            [Math]::Abs($case.clientWidth - $case.requestedWidth) -gt 1 -or [Math]::Abs($case.clientHeight - $case.requestedHeight) -gt 1 -or
            $case.renderScaling -le 0 -or $case.viewportWidth -le 0 -or $case.viewportHeight -le 0 -or
            $case.viewportX -lt 0 -or $case.viewportY -lt 0 -or $case.viewportX + $case.viewportWidth -gt $case.clientWidth + 1 -or
            $case.viewportY + $case.viewportHeight -gt $case.clientHeight + 1 -or $case.extentWidth -gt $case.viewportWidth + 1 -or
            $case.targetWidth -le 0 -or $case.targetHeight -le 0 -or $case.targetX -lt 0 -or $case.targetY -lt 0 -or
            $case.targetX + $case.targetWidth -gt $case.clientWidth + 1 -or $case.targetY + $case.targetHeight -gt $case.clientHeight + 1 -or
            $case.targetInScrollY -lt -1 -or $case.targetInScrollY + $case.targetHeight -gt $case.viewportHeight + 1 -or
            -not [IO.Path]::IsPathFullyQualified($case.capture) -or -not (Test-Path -LiteralPath $case.capture -PathType Leaf)) {
            throw 'Installed launcher viewport or profile action is clipped, or its capture is missing.'
        }
    }
}

Export-ModuleMember -Function Find-CopperScreenSdkTool, Read-CopperScreenXml, Get-CopperScreenMsixIdentity, Assert-CopperScreenIdentity, Assert-CopperScreenUpdatePair, New-CopperScreenMsixAssets, Test-CopperScreenDiagnosticAssembly, Assert-CopperScreenExecutableManifest, Get-CopperScreenWackSummary, Assert-CopperScreenWackReport, Wait-CopperScreenProcessExit, Assert-CopperScreenMediaPhase, Assert-CopperScreenMediaUpdate, Assert-CopperScreenLauncherLayout
