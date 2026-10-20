$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot '../msix/CopperScreen.Msix.psm1') -Force
$checks = 0
function Expect-Rejection([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw 'An invalid package operation was accepted.' }
    $script:checks++
}
$name = 'CopperScreen.Test.' + [guid]::NewGuid().ToString('N')
Assert-CopperScreenIdentity $name 'CN=CopperScreen Installed Tests' '1.1.3.0' -InstalledTestBuild
$checks++
Expect-Rejection { Assert-CopperScreenIdentity 'CopperScreen.Store' 'CN=Publisher' '1.1.3.0' -InstalledTestBuild }
Expect-Rejection { Assert-CopperScreenIdentity $name 'CN=Publisher' '1.1.3.0' }
Expect-Rejection { Assert-CopperScreenIdentity 'CopperScreen.Store' 'CN=Publisher' '1.1.3.1' }
Expect-Rejection { Assert-CopperScreenIdentity 'CopperScreen.Store' 'CN=Publisher' '1.1.65536.0' }
Expect-Rejection { Assert-CopperScreenIdentity '../Escape' 'CN=Publisher' '1.1.3.0' }
Expect-Rejection { Read-CopperScreenXml '<!DOCTYPE Package [<!ENTITY test SYSTEM "file:///not-readable">]><Package>&test;</Package>' }
Assert-CopperScreenWackReport (Read-CopperScreenXml '<REPORT OVERALL_RESULT="PASS" />')
$checks++
Expect-Rejection { Assert-CopperScreenWackReport (Read-CopperScreenXml '<REPORT OVERALL_RESULT="FAIL"><TEST RESULT="PASS" /></REPORT>') }
Expect-Rejection { Assert-CopperScreenWackReport (Read-CopperScreenXml '<REPORT><TEST OVERALL_RESULT="PASS" /></REPORT>') }
Expect-Rejection { Assert-CopperScreenWackReport (Read-CopperScreenXml '<REPORT OVERALL_RESULT="INCOMPLETE" />') }
Expect-Rejection { Assert-CopperScreenWackReport (Read-CopperScreenXml '<REPORT OVERALL_RESULT="WARNING" />') }
$wackFixture = '<REPORT OVERALL_RESULT="WARNING" PARTIAL_RUN="FALSE"><REQUIREMENTS><REQUIREMENT><TEST NAME="Required" OPTIONAL="FALSE"><MESSAGES/><RESULT>PASS</RESULT></TEST><TEST NAME="Optional" OPTIONAL="TRUE"><MESSAGES><MESSAGE TEXT="Runtime reference"/></MESSAGES><RESULT>FAIL</RESULT></TEST></REQUIREMENT></REQUIREMENTS></REPORT>'
$summary = Get-CopperScreenWackSummary (Read-CopperScreenXml $wackFixture)
if (-not $summary.RequiredTestsPassed -or $summary.OverallResult -cne 'WARNING' -or $summary.Tests[1].Messages[0] -cne 'Runtime reference') { throw 'WACK summary lost optional failure evidence.' }
$checks++
# Actual Centennial reports can return overall PASS with an optional FAIL.
# Preserve that finding without inventing an overall failure or suppressing it.
$passingWithOptional = $wackFixture.Replace('OVERALL_RESULT="WARNING"', 'OVERALL_RESULT="PASS"')
Assert-CopperScreenWackReport (Read-CopperScreenXml $passingWithOptional)
if ((Get-CopperScreenWackSummary (Read-CopperScreenXml $passingWithOptional)).Tests[1].Result -cne 'FAIL') { throw 'Optional finding was lost from a passing report.' }
$checks++
foreach ($mutation in @(
    $wackFixture.Replace('<RESULT>PASS</RESULT>', '<RESULT>WARNING</RESULT>'),
    $wackFixture.Replace('<RESULT>PASS</RESULT>', '<RESULT>FAIL</RESULT>'),
    $wackFixture.Replace('PARTIAL_RUN="FALSE"', 'PARTIAL_RUN="TRUE"'),
    $wackFixture.Replace('OPTIONAL="TRUE"', 'OPTIONAL="UNKNOWN"'),
    $wackFixture.Replace('<RESULT>PASS</RESULT>', ''),
    '<REPORT OVERALL_RESULT="PASS" PARTIAL_RUN="FALSE" />')) {
    if ((Get-CopperScreenWackSummary (Read-CopperScreenXml $mutation)).RequiredTestsPassed) { throw 'Incomplete or failing required WACK evidence was accepted.' }
    $checks++
}
$baseline = [pscustomobject]@{ Name = $name; Publisher = 'CN=Publisher'; Version = [version]'1.1.3.0'; Architecture = 'x64'; Diagnostic = $true }
$update = [pscustomobject]@{ Name = $name; Publisher = 'CN=Publisher'; Version = [version]'1.1.4.0'; Architecture = 'x64'; Diagnostic = $true }
Assert-CopperScreenUpdatePair $baseline $update
$checks++
foreach ($mutation in @(@('Name', 'Other.Package'), @('Publisher', 'CN=Other'), @('Architecture', 'arm64'), @('Version', [version]'1.1.3.0'), @('Version', [version]'1.1.2.0'), @('Diagnostic', $false))) {
    $copy = $update.PSObject.Copy()
    $copy.($mutation[0]) = $mutation[1]
    Expect-Rejection { Assert-CopperScreenUpdatePair $baseline $copy }
}
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$executableManifest = Get-Content -LiteralPath (Join-Path $repository 'CopperScreen/app.manifest') -Raw
Assert-CopperScreenExecutableManifest (Read-CopperScreenXml $executableManifest)
$checks++
Expect-Rejection { Assert-CopperScreenExecutableManifest (Read-CopperScreenXml $executableManifest.Replace('PerMonitorV2', 'System')) }
Expect-Rejection { Assert-CopperScreenExecutableManifest (Read-CopperScreenXml $executableManifest.Replace('asInvoker', 'requireAdministrator')) }
Expect-Rejection { Assert-CopperScreenExecutableManifest (Read-CopperScreenXml $executableManifest.Replace('uiAccess="false"', 'uiAccess="true"')) }
Expect-Rejection { Assert-CopperScreenExecutableManifest (Read-CopperScreenXml '<assembly />') }
$shell = (Get-Process -Id $PID).Path
$zero = Start-Process -FilePath $shell -ArgumentList @('-NoProfile', '-Command', 'exit 0') -WindowStyle Hidden -PassThru
try {
    $result = Wait-CopperScreenProcessExit -Process $zero -TimeoutSeconds 20
    if ($result.ExitCode -ne 0) { throw 'Successful process exit was not observed.' }
    $checks++
} finally { $zero.Dispose() }
$nonzero = Start-Process -FilePath $shell -ArgumentList @('-NoProfile', '-Command', 'exit 7') -WindowStyle Hidden -PassThru
try { Expect-Rejection { Wait-CopperScreenProcessExit -Process $nonzero -TimeoutSeconds 20 } }
finally { $nonzero.Dispose() }
$timeout = Start-Process -FilePath $shell -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 10') -WindowStyle Hidden -PassThru
try { Expect-Rejection { Wait-CopperScreenProcessExit -Process $timeout -TimeoutSeconds 1 } }
finally { if (-not $timeout.HasExited) { $timeout.Kill() }; $timeout.Dispose() }
$directory = Join-Path $repository ('artifacts/msix-tool-tests-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $directory
# Synthetic ZIP tests validate the reader, not package deployment or certification.
$fixture = Join-Path $directory 'synthetic-reader-fixture.msix'
$archive = [IO.Compression.ZipFile]::Open($fixture, [IO.Compression.ZipArchiveMode]::Create)
try {
    $manifest = Read-CopperScreenXml (Get-Content -LiteralPath (Join-Path $repository 'packaging/msix/AppxManifest.xml') -Raw)
    $manifest.Package.Identity.SetAttribute('Name', $name)
    $manifest.Package.Identity.SetAttribute('Publisher', 'CN=Publisher')
    $entry = $archive.CreateEntry('AppxManifest.xml')
    $writer = [IO.StreamWriter]::new($entry.Open())
    try { $writer.Write($manifest.OuterXml) } finally { $writer.Dispose() }
    $null = $archive.CreateEntry('installed-test-build.json')
}
finally { $archive.Dispose() }
$identity = Get-CopperScreenMsixIdentity $fixture
if ($identity.Name -cne $name -or $identity.Signed -or -not $identity.Diagnostic) { throw 'Synthetic package identity was misread.' }
$checks++
New-CopperScreenMsixAssets (Join-Path $repository 'CopperScreen/Assets/CopperScreen.ico') (Join-Path $directory 'Assets')
foreach ($asset in @(@('StoreLogo.png', 50), @('Square44x44Logo.png', 44), @('Square150x150Logo.png', 150))) {
    $image = [Drawing.Image]::FromFile((Join-Path $directory ('Assets/' + $asset[0])))
    try { if ($image.Width -ne $asset[1] -or $image.Height -ne $asset[1]) { throw 'Invalid package icon dimensions.' } }
    finally { $image.Dispose() }
    $checks++
}
$root = Join-Path $directory 'PackageData'
$private = Join-Path $root 'InstalledTests/run'
$original = Join-Path $directory 'external/original.adf'
$export = Join-Path $directory 'external/backup.copperos'
$saved = Join-Path $private 'CopperOS/Workspaces/persistence/System.adf'
$hash = 'a' * 64
$media = @{
    Phase='media-seed'; MediaMode=$true; UserDataRoot=$root; UserDataDirectory=$private; InstallDirectory=(Join-Path $directory 'WindowsApps/app')
    CopperOsStorage=@{passed=$true;engine='Lightweight';legacy=$false;hostDosCalls=$false}
    MediaPersistence=@{Protocol='copperscreen-installed-media-v1';Passed=$true;Phase='seed';Originals=@{$original=$hash};PersistentFiles=@{$saved=$hash};Exports=@{$export=$hash}}
} | ConvertTo-Json -Depth 8 | ConvertFrom-Json
Assert-CopperScreenMediaPhase $media 'media-seed'
$checks++
$later = $media | ConvertTo-Json -Depth 8 | ConvertFrom-Json
Assert-CopperScreenMediaUpdate $media $later
$checks++
foreach ($mutation in @('hash','missing','private-export','engine','native-failed','host-shim','media-failed','empty')) {
    $copy = $media | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    switch ($mutation) {
        'hash' { $copy.MediaPersistence.Originals.$original = 'b' * 64 }
        'missing' { $copy.MediaPersistence.PersistentFiles = [pscustomobject]@{} }
        'private-export' { $copy.MediaPersistence.Exports = [pscustomobject]@{$saved=$hash} }
        'engine' { $copy.CopperOsStorage.engine = 'Legacy' }
        'native-failed' { $copy.CopperOsStorage.passed = $false }
        'host-shim' { $copy.CopperOsStorage.hostDosCalls = $true }
        'media-failed' { $copy.MediaPersistence.Passed = $false }
        'empty' { $copy.MediaPersistence.Originals = [pscustomobject]@{} }
    }
    if ($mutation -in @('hash','missing')) { Expect-Rejection { Assert-CopperScreenMediaUpdate $media $copy } }
    else { Expect-Rejection { Assert-CopperScreenMediaPhase $copy 'media-seed' } }
}
$capture = Join-Path $directory 'layout-capture-fixture.txt'
'Synthetic capture presence control; not rendered UI evidence.' | Set-Content -LiteralPath $capture
$layout = @{protocol='copperscreen-installed-launcher-layout-v1';passed=$true;bootMethod='CopperOS';cases=@(
    foreach ($size in @(@(800,600),@(1280,720),@(960,640))) {
        @{requestedWidth=$size[0];requestedHeight=$size[1];clientWidth=$size[0];clientHeight=$size[1];renderScaling=1;
          viewportX=0;viewportY=100;viewportWidth=700;viewportHeight=400;extentWidth=700;extentHeight=800;offsetY=400;
          targetX=50;targetY=450;targetWidth=200;targetHeight=40;targetInScrollY=350;capture=$capture}
    }
)} | ConvertTo-Json -Depth 8 | ConvertFrom-Json
Assert-CopperScreenLauncherLayout $layout
$checks++
foreach ($mutation in @('protocol','failed','boot','missing-case','size','scale','zero-target','viewport','horizontal','client-target','scroll-target','nan','missing-number','capture')) {
    $copy = $layout | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    switch ($mutation) {
        'protocol' { $copy.protocol = 'unknown' }
        'failed' { $copy.passed = $false }
        'boot' { $copy.bootMethod = 'Other' }
        'missing-case' { $copy.cases = @($copy.cases[0]) }
        'size' { $copy.cases[0].clientHeight = 601.5 }
        'scale' { $copy.cases[0].renderScaling = 0 }
        'zero-target' { $copy.cases[0].targetHeight = 0 }
        'viewport' { $copy.cases[0].viewportHeight = 501.5 }
        'horizontal' { $copy.cases[0].extentWidth = 702 }
        'client-target' { $copy.cases[0].targetY = 590 }
        'scroll-target' { $copy.cases[0].targetInScrollY = 390 }
        'nan' { $copy.cases[0].targetY = [double]::NaN }
        'missing-number' { $copy.cases[0].PSObject.Properties.Remove('targetHeight') }
        'capture' { $copy.cases[0].capture = Join-Path $directory 'absent.png' }
    }
    Expect-Rejection { Assert-CopperScreenLauncherLayout $copy }
}
Write-Output "MSIX tooling controls: $checks passed. Synthetic fixtures are not installed-package evidence."
