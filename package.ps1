param([string]$Version='0.5.1')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'test.ps1')
$name="WT_Tactical_Overlay_v${Version}_windows_x64"
$directory=Join-Path $PSScriptRoot "dist\$name"
$archive=$directory+'.zip'
if((Test-Path -LiteralPath $directory) -or (Test-Path -LiteralPath $archive)){throw 'Release destination already exists; choose another version or preserve it before rebuilding.'}
New-Item -ItemType Directory -Path $directory,(Join-Path $directory 'bin'),(Join-Path $directory 'data'),(Join-Path $directory 'docs')|Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\WtTacticalOverlay.exe') -Destination (Join-Path $directory 'bin\WtTacticalOverlay.exe')
foreach($file in @('maps.json','recognition.json','coverage.json')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot "data\$file") -Destination (Join-Path $directory 'data')}
foreach($file in @('Start-Overlay.cmd','Start-External.cmd','README.md','COVERAGE.zh-CN.md','THIRD-PARTY-NOTICES.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $directory}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\VALIDATION.md') -Destination (Join-Path $directory 'docs')
Compress-Archive -LiteralPath $directory -DestinationPath $archive -CompressionLevel Optimal
Get-FileHash -LiteralPath $archive -Algorithm SHA256|Select-Object Path,Hash
