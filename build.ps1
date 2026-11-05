param([switch]$TestsOnly)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$bin=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$refs=@('/r:System.Web.Extensions.dll','/r:System.Net.Http.dll',"/r:$wpf\WindowsBase.dll","/r:$wpf\PresentationCore.dll","/r:$wpf\PresentationFramework.dll",'/r:System.Xaml.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
& $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$bin\CoreTests.exe" @refs "$PSScriptRoot\src\Core.cs" "$PSScriptRoot\tests\CoreTests.cs"
if($LASTEXITCODE -ne 0){throw 'Core tests compilation failed'}
& $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$bin\GridMappingTests.exe" /r:System.Web.Extensions.dll "$PSScriptRoot\src\Core.cs" "$PSScriptRoot\tests\GridMappingTests.cs"
if($LASTEXITCODE -ne 0){throw 'Grid mapping tests compilation failed'}
& $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$bin\TacticalZoneTests.exe" /r:System.Web.Extensions.dll "$PSScriptRoot\src\Core.cs" "$PSScriptRoot\tests\TacticalZoneTests.cs"
if($LASTEXITCODE -ne 0){throw 'Tactical zone tests compilation failed'}
& $compiler /nologo /utf8output /target:exe /platform:x64 "/out:$bin\LibraryContentTests.exe" /r:System.Web.Extensions.dll "$PSScriptRoot\src\Core.cs" "$PSScriptRoot\tests\LibraryContentTests.cs"
if($LASTEXITCODE -ne 0){throw 'Library content tests compilation failed'}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
& $compiler /nologo /utf8output /optimize+ /target:exe /platform:x64 /main:UiCalibrationTests "/out:$bin\UiCalibrationTests.exe" @refs @sources "$PSScriptRoot\tests\UiCalibrationTests.cs"
if($LASTEXITCODE -ne 0){throw 'UI calibration tests compilation failed'}
if(-not $TestsOnly){
  & $compiler /nologo /utf8output /optimize+ /target:winexe /platform:x64 "/out:$bin\WtTacticalOverlay.exe" @refs @sources
  if($LASTEXITCODE -ne 0){throw 'Application compilation failed'}
}
