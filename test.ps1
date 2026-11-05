$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -TestsOnly
$library=Join-Path $PSScriptRoot 'data\maps.json'
$script:suites=[ordered]@{}
function Run-Suite([string]$name,[scriptblock]$action){
    $lines=@(& $action)
    $lines|Write-Output
    $result=@($lines|Where-Object {$_ -match '^RESULT \d+ passed, \d+ failed$'})
    if($result.Count -ne 1 -or $result[0] -notmatch '^RESULT (\d+) passed, (\d+) failed$'){throw "Missing test result: $name"}
    $passed=[int]$Matches[1];$failed=[int]$Matches[2]
    $script:suites[$name]=[ordered]@{Passed=$passed;Failed=$failed}
    if($failed -ne 0){throw "$name tests failed"}
}
Run-Suite 'Core' {& "$PSScriptRoot\bin\CoreTests.exe" $library}
Run-Suite 'GridMapping' {& "$PSScriptRoot\bin\GridMappingTests.exe"}
Run-Suite 'TacticalZones' {& "$PSScriptRoot\bin\TacticalZoneTests.exe"}
Run-Suite 'LibraryContent' {& "$PSScriptRoot\bin\LibraryContentTests.exe" $library}
Run-Suite 'UiCalibration' {& "$PSScriptRoot\bin\UiCalibrationTests.exe"}
Run-Suite 'Recognition' {& "$PSScriptRoot\tools\Test-Recognition.ps1"}
$total=0;foreach($suite in $script:suites.Values){$total+=$suite.Passed}
[ordered]@{RunAt=[DateTimeOffset]::Now.ToString('o');Passed=$total;Failed=0;Suites=$script:suites;LibrarySHA256=(Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'test-results.json') -Encoding utf8
