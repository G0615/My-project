param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor'
)
$ErrorActionPreference = 'Stop'
$demoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $demoRoot
$verificationRoot = Join-Path $demoRoot 'Logs\Verification'
[System.IO.Directory]::CreateDirectory($verificationRoot) | Out-Null
$compilerPath = Join-Path $UnityEditor 'Data\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$runtimePath = Join-Path $UnityEditor 'Data\DotNetSdk\dotnet.exe'
$monoPath = Join-Path $UnityEditor 'Data\MonoBleedingEdge\bin\mono.exe'
$responseSources = Get-ChildItem -LiteralPath (Join-Path $demoRoot 'Library\Bee\artifacts') -Filter 'Assembly-CSharp.rsp' -Recurse
if (!$responseSources) { throw 'Unity reference response file is not available. Open and import the project first.' }
$runtimeResponse = $responseSources | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$editorResponse = Join-Path $runtimeResponse.DirectoryName 'Assembly-CSharp-Editor.rsp'

function Write-CompileResponse([string]$source, [string]$name, [bool]$editor) {
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines($source)) {
        if ($line -match '^[-/](out:|refout:|analyzer:|additionalfile:|analyzerconfig:)') { continue }
        if ($line -match 'Assembly-CSharp(?:\.ref)?\.dll' -and $line -match '^[-/]r:') {
            $lines.Add('-r:"' + (Join-Path $verificationRoot 'Assembly-CSharp.dll') + '"')
        } else { $lines.Add($line) }
    }
    $lines.Add('-out:"' + (Join-Path $verificationRoot ($name + '.dll')) + '"')
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $demoRoot 'Assets\CelebrationDemo') -Filter '*.cs' -Recurse) {
        $isEditor = $file.FullName -match '[\\/]Editor[\\/]'
        if ($isEditor -eq $editor) { $lines.Add('"' + $file.FullName + '"') }
    }
    $responsePath = Join-Path $verificationRoot ($name + '.rsp')
    [System.IO.File]::WriteAllLines($responsePath, $lines, [System.Text.UTF8Encoding]::new($false))
    return $responsePath
}

$runtimeRsp = Write-CompileResponse $runtimeResponse.FullName 'Assembly-CSharp' $false
& $runtimePath $compilerPath -noconfig ('@' + $runtimeRsp)
if ($LASTEXITCODE -ne 0) { throw 'Runtime C# compilation failed.' }
$editorRsp = Write-CompileResponse $editorResponse 'Assembly-CSharp-Editor' $true
& $runtimePath $compilerPath -noconfig ('@' + $editorRsp)
if ($LASTEXITCODE -ne 0) { throw 'Editor C# compilation failed.' }
Write-Output 'CELEBRATION_COMPILE_PASS: runtime and editor, using installed Unity references.'

# Run only the engine-independent rule suite. This does not replace Play Mode or a player build.
$entryPath = Join-Path $verificationRoot 'CoreSmokeEntry.cs'
[System.IO.File]::WriteAllText($entryPath, @'
using System;
class CoreSmokeEntry
{
    static int Main()
    {
        try { Console.WriteLine("CELEBRATION_CORE_PASS: " + CelebrationDemo.CoreSmokeTests.RunAll()); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
'@, [System.Text.UTF8Encoding]::new($false))
$coreRspLines = @(
    '-nologo', '-target:exe', '-langversion:9.0', '-define:UNITY_EDITOR',
    ('-out:"' + (Join-Path $verificationRoot 'CoreSmoke.exe') + '"'),
    ('"' + $entryPath + '"'),
    ('"' + (Join-Path $demoRoot 'Assets\CelebrationDemo\Editor\CoreSmokeTests.cs') + '"')
)
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $demoRoot 'Assets\CelebrationDemo\Core') -Filter '*.cs') {
    $coreRspLines += '"' + $file.FullName + '"'
}
$coreRsp = Join-Path $verificationRoot 'CoreSmoke.rsp'
[System.IO.File]::WriteAllLines($coreRsp, $coreRspLines, [System.Text.UTF8Encoding]::new($false))
$monoCompiler = Join-Path $UnityEditor 'Data\MonoBleedingEdge\lib\mono\4.5\csc.exe'
& $monoPath $monoCompiler ('@' + $coreRsp)
if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed.' }
& $monoPath (Join-Path $verificationRoot 'CoreSmoke.exe')
if ($LASTEXITCODE -ne 0) { throw 'Core rule verification failed.' }
