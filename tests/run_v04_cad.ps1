param(
    [Parameter(Mandatory=$true)][string]$InputDwg,
    [Parameter(Mandatory=$true)][string]$PluginDirectory,
    [string]$AutoCADDirectory='D:\Software\AutoCAD 2023'
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$plugin=[IO.Path]::GetFullPath((Join-Path $PluginDirectory 'WallDetailTool.dll'))
$PluginDirectory=[IO.Path]::GetDirectoryName($plugin)
if(-not (Test-Path -LiteralPath $plugin)){throw 'Plugin DLL not found.'}
if(-not (Test-Path -LiteralPath $InputDwg)){throw 'Input DWG not found; use a disposable source or copy.'}
$root=Join-Path $PSScriptRoot ('profile_v04_cad_'+[Guid]::NewGuid().ToString('N'))
$root=[IO.Path]::GetFullPath($root)
if(-not $root.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Test output is outside workspace.'}
New-Item -ItemType Directory -Path $root | Out-Null
$copy=Join-Path $root 'V04_CAD_Copy.dwg'
Copy-Item -LiteralPath $InputDwg -Destination $copy
$msbuild='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$harnessDirectory=Join-Path $root 'Harness'
$harnessOutput=$harnessDirectory+[IO.Path]::DirectorySeparatorChar
& $msbuild (Join-Path $PSScriptRoot 'BeamSideCadTests.csproj') /t:Rebuild /p:Configuration=Release /p:Platform=x64 "/p:PluginDir=$PluginDirectory" "/p:AutoCADDir=$AutoCADDirectory" "/p:OutputPath=$harnessOutput" /nologo /verbosity:minimal
if($LASTEXITCODE -ne 0){throw 'CAD test assembly build failed.'}
$harness=Join-Path $harnessDirectory 'BeamSideCadTests.dll'
$script=Join-Path $root 'integration.scr'
$lines=@('_NETLOAD',('"'+$plugin.Replace('\','/')+'"'),'_NETLOAD',('"'+$harness.Replace('\','/')+'"'),'WDTBEAMSIDEAUTOTEST','_.QUIT','_Y','')
[IO.File]::WriteAllLines($script,$lines,[Text.Encoding]::ASCII)
$stdout=Join-Path $root 'stdout.log';$stderr=Join-Path $root 'stderr.log'
Push-Location -LiteralPath $root
try {
    & (Join-Path $AutoCADDirectory 'accoreconsole.exe') /i $copy /s $script /isolate ('WDT_V04_'+[Guid]::NewGuid().ToString('N')) $root /l en-US > $stdout 2> $stderr
    $code=$LASTEXITCODE
} finally { Pop-Location }
$bytes=[IO.File]::ReadAllBytes($stdout);$log=[Text.Encoding]::Unicode.GetString($bytes)
if($log -notmatch 'BEAM_SIDE_CAD_'){$log=[IO.File]::ReadAllText($stdout).Replace([string][char]0,'')}
Write-Output $log
if($code -ne 0 -or $log -notmatch 'BEAM_SIDE_CAD_PASS'){throw "CAD runtime did not pass; evidence retained in $root"}
Write-Output "CAD evidence: $root"
