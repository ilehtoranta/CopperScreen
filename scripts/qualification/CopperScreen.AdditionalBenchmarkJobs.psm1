Set-StrictMode -Version Latest

function Test-CopperScreenAdditionalBenchmarkJob {
    param([Parameter(Mandatory)]$Process)
    if ($Process.Name -match '^(cl|clang|clang-cl|UnrealHeaderTool|UnrealBuildTool|UnrealPak|UnrealEditor-Cmd|ExactBootProbe|CopperStartInputProbe)(\.exe)?$') { return $true }
    if ($Process.Name -cne 'dotnet.exe') { return $false }
    if ($null -eq $Process.CommandLine) { throw 'Missing command line for dotnet process; job classification unavailable.' }
    return $Process.CommandLine -match '(?i)(CopperSharp\.Compiler\.Cli\.dll|ExactBootProbe\.dll|CopperStartInputProbe\.dll|UnrealBuildTool\.dll)(?:["\s]|$)|AutomationTool\.dll.*(?:BuildCookRun|BuildGraph)'
}

function Get-CopperScreenAdditionalBenchmarkJobs {
    @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object {
        Test-CopperScreenAdditionalBenchmarkJob $_
    } | ForEach-Object { "$($_.Name)[$($_.ProcessId)]" })
}

function Assert-CopperScreenAdditionalBenchmarkJobControls {
    foreach ($command in @(
        'dotnet "C:/CopperSharp.Compiler.Cli.dll" input.dll',
        'dotnet C:/ExactBootProbe.dll input.rom',
        'dotnet C:/CopperStartInputProbe.dll input.rom',
        'dotnet AutomationTool.dll BuildCookRun -project=example',
        'dotnet C:/UnrealBuildTool.dll target'
    )) {
        if (-not (Test-CopperScreenAdditionalBenchmarkJob ([pscustomobject]@{Name='dotnet.exe';CommandLine=$command}))) { throw 'Additional job positive control failed.' }
    }
    foreach ($name in @('cl.exe','UnrealHeaderTool.exe','UnrealEditor-Cmd.exe','UnrealPak.exe')) {
        if (-not (Test-CopperScreenAdditionalBenchmarkJob ([pscustomobject]@{Name=$name;CommandLine='example'}))) { throw 'Native job control failed.' }
    }
    foreach ($process in @(
        [pscustomobject]@{Name='dotnet.exe';CommandLine='dotnet web-service.dll'},
        [pscustomobject]@{Name='dotnet.exe';CommandLine='dotnet CopperMod.Amiga.Lightweight.Runner.dll'},
        [pscustomobject]@{Name='devenv.exe';CommandLine='devenv solution.slnx'},
        [pscustomobject]@{Name='MSBuild.exe';CommandLine='/nodemode:1 /nodeReuse:true'},
        [pscustomobject]@{Name='UnrealEditor.exe';CommandLine='editor project'}
    )) {
        if (Test-CopperScreenAdditionalBenchmarkJob $process) { throw 'Normal application or separately governed node misclassified.' }
    }
}

Export-ModuleMember -Function Test-CopperScreenAdditionalBenchmarkJob, Get-CopperScreenAdditionalBenchmarkJobs, Assert-CopperScreenAdditionalBenchmarkJobControls
