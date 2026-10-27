param([string]$OutputPath='bin\V04WindowFinal\')
$ErrorActionPreference='Stop'
$msbuild='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
foreach($name in @('BeamSideWindowTests','NodeDrawWindowTests')) {
    & $msbuild (Join-Path $PSScriptRoot "$name.csproj") /t:Rebuild /p:Configuration=Release /p:Platform=x64 "/p:OutputPath=$OutputPath" "/p:BaseIntermediateOutputPath=obj\V04Window_$name\" /nologo /verbosity:minimal
    if($LASTEXITCODE -ne 0){throw "Build failed: $name"}
    & (Join-Path (Join-Path $PSScriptRoot $OutputPath) "$name.exe")
    if($LASTEXITCODE -ne 0){throw "Test failed: $name"}
}
