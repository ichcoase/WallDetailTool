param(
    [string]$AutoCADDir = 'D:\Software\AutoCAD 2023',
    [string]$OutputPath = 'bin\Release\'
)

$ErrorActionPreference = 'Stop'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild)) {
    throw 'MSBuild not found. Visual Studio 2022 build tools are required.'
}
& $msbuild (Join-Path $PSScriptRoot 'WallDetailTool.csproj') /t:Build /p:Configuration=Release /p:Platform=x64 "/p:AutoCADDir=$AutoCADDir" "/p:OutputPath=$OutputPath" /nologo /verbosity:minimal
exit $LASTEXITCODE
