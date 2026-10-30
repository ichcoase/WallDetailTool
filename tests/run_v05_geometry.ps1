param([string]$OutputPath='bin\V05Calculation\')
$ErrorActionPreference='Stop'
$msbuild='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
# Standalone .NET executables only. Never starts AutoCAD/Core Console.
$names=@('SketchDiagramTests','UsabilityTests','RectangleInputTests','EndLengthAnnotationTests','CantileverGeometryTests','CantileverWindowTests','CantileverAnnotationTests','UpstandGeometryStage1Tests','SlabUpstandGeometryTests',
    'UpstandRebarStage2Tests','SlabUpstandRebarTests','UpstandAnnotationStage3Tests','HangingNodeTests',
    'NodeDrawWindowTests','BeamSideContourTests','BeamSideDotsTests','BeamSideRebarTests','BeamSideWindowTests')
foreach($name in $names) {
    & $msbuild (Join-Path $PSScriptRoot "$name.csproj") /t:Build /p:Configuration=Release /p:Platform=x64 "/p:OutputPath=$OutputPath" /nologo /verbosity:minimal
    if($LASTEXITCODE -ne 0) { throw "Build failed: $name" }
    & (Join-Path (Join-Path $PSScriptRoot $OutputPath) "$name.exe")
    if($LASTEXITCODE -ne 0) { throw "Test failed: $name" }
}
