[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Rom,
    [Parameter(Mandatory)][string]$License
)

$ErrorActionPreference = 'Stop'
$temporary = [IO.Directory]::CreateTempSubdirectory('copperscreen-mini-ci-')
$zip = Join-Path $temporary.FullName 'bundle.zip'
$staged = Join-Path $temporary.FullName 'staged'
try {
    & "$PSScriptRoot/prepare-copperstart-mini-bundle.ps1" -Rom $Rom -License $License -OutputDirectory $staged
    [IO.Compression.ZipFile]::CreateFromDirectory($staged, $zip, [IO.Compression.CompressionLevel]::SmallestSize, $false)
    $encoded = [Convert]::ToBase64String([IO.File]::ReadAllBytes($zip))
    $partCount = 8
    $partLength = [int][Math]::Ceiling($encoded.Length / [double]$partCount)
    if ($partLength -gt 48000 -or $encoded.Length -le $partCount) {
        throw 'The archive does not fit the eight CopperStart Mini workflow secrets.'
    }

    for ($i = 0; $i -lt $partCount; $i++) {
        $offset = $i * $partLength
        $part = $encoded.Substring($offset, [Math]::Min($partLength, $encoded.Length - $offset))
        $name = 'COPPERSTART_MINI_BUNDLE_{0:D2}' -f ($i + 1)
        $part | gh secret set $name --repo ilehtoranta/CopperScreen
        if ($LASTEXITCODE -ne 0) { throw "Failed to configure $name." }
    }
} finally {
    # The only files here are the verified inputs and the temporary CI archive.
    foreach ($path in $zip, "$staged/CopperStart-Mini.rom", "$staged/LICENSE.txt") {
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path }
    }
    if (Test-Path -LiteralPath $staged -PathType Container) { [IO.Directory]::Delete($staged) }
    $temporary.Delete()
}
