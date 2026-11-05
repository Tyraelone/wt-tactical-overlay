param([string]$CapturedMap,[string]$ExpectedMap)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=Join-Path $root 'src'
& $compiler /nologo /optimize+ /target:exe /platform:x64 "/out:$root\bin\RecognitionTests.exe" /r:System.Drawing.dll /r:System.Web.Extensions.dll "$source\Core.cs" "$source\AutoRecognition.cs" "$root\tests\RecognitionTests.cs"
if($LASTEXITCODE -ne 0){throw 'Recognition test compilation failed'}
if($CapturedMap){& "$root\bin\RecognitionTests.exe" "$root\data\recognition.json" $CapturedMap $ExpectedMap}else{& "$root\bin\RecognitionTests.exe"}
if($LASTEXITCODE -ne 0){throw 'Recognition tests failed'}
