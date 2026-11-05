param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$destination=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path $destination -Force | Out-Null
# Extract exact current production bodies; do not maintain a second reader implementation.
$cad=Get-Content -LiteralPath (Join-Path $projectRoot 'BeamSideCadCommand.cs') -Raw
$readerStart=$cad.IndexOf('        internal static List<ContourSegment> ReadBeamSideSegments(')
$readerEnd=$cad.IndexOf('        private static BeamSideGeometry SelectBeamSideGeometry(', $readerStart)
if($readerStart -lt 0 -or $readerEnd -le $readerStart){throw 'Production reader boundaries changed; review extraction.'}
$state=Get-Content -LiteralPath (Join-Path $projectRoot 'BeamSideDrawState.cs') -Raw
$unitStart=$state.IndexOf('    internal sealed class BeamSideUnitTransform')
$unitEnd=$state.IndexOf('    internal static class BeamSideDrawRecord', $unitStart)
if($unitStart -lt 0 -or $unitEnd -le $unitStart){throw 'Production unit boundaries changed; review extraction.'}
$generated='using System;using System.Collections.Generic;using Autodesk.AutoCAD.DatabaseServices;using Autodesk.AutoCAD.Geometry;namespace WallDetailTool { public partial class Commands {'+
    $cad.Substring($readerStart,$readerEnd-$readerStart)+'}'+$state.Substring($unitStart,$unitEnd-$unitStart)+'}'
$generatedPath=Join-Path $destination 'ProductionReader.cs'
Set-Content -LiteralPath $generatedPath -Value $generated -Encoding UTF8
$msbuild='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $PSScriptRoot 'TopSlabReaderContractTests.csproj') /t:Rebuild /p:Configuration=Release /p:Platform=x64 "/p:OutputPath=$destination\" "/p:GeneratedReader=$generatedPath" /nologo /verbosity:minimal
if($LASTEXITCODE -ne 0){throw 'Reader contract build failed.'}
& (Join-Path $destination 'TopSlabReaderContractTests.exe')
if($LASTEXITCODE -ne 0){throw 'Reader contract tests failed.'}
