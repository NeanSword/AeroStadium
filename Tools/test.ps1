param(
    [switch]$UnityTests,
    [switch]$SkipCore,
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe',
    [string]$DotNet = 'C:\Program Files\dotnet\dotnet.exe'
)
$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ($SkipCore -and -not $UnityTests) { throw '-SkipCore requires -UnityTests.' }
$checksProject = Join-Path $projectDirectory 'Tests\CoreChecks\CoreChecks.csproj'
$catalogPath = Join-Path $projectDirectory 'Assets\AeroStadium\Resources\Data\catalog.json'
if (-not $SkipCore) {
    if (-not (Test-Path -LiteralPath $DotNet -PathType Leaf)) { throw ".NET SDK missing: $DotNet" }
    $coreReportDirectory = Join-Path $projectDirectory 'Builds\Reports'
    New-Item -ItemType Directory -Path $coreReportDirectory -Force | Out-Null
    & $DotNet run --project $checksProject --configuration Release -- $catalogPath | Tee-Object -FilePath (Join-Path $coreReportDirectory 'core-checks.log')
    if ($LASTEXITCODE -ne 0) { throw 'Combat engine checks failed.' }
}
if ($UnityTests) {
    if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity editor missing: $UnityEditor" }
    $reportsDirectory = Join-Path $projectDirectory 'Builds\Reports'
    $logDirectory = Join-Path $projectDirectory 'Logs'
    New-Item -ItemType Directory -Path $reportsDirectory, $logDirectory -Force | Out-Null
    $resultPath = Join-Path $reportsDirectory 'input-tests.xml'
    $logPath = Join-Path $logDirectory 'input-tests.log'
    # The test runner exits itself; -quit would interrupt tests before completion.
    $testArguments = '-batchmode -nographics -projectPath "' + $projectDirectory + '" -runTests -testPlatform EditMode -testResults "' + $resultPath + '" -logFile "' + $logPath + '"'
    $testProcess = Start-Process -FilePath $UnityEditor -ArgumentList $testArguments -WindowStyle Hidden -PassThru
    $testProcess.WaitForExit()
    if ($testProcess.ExitCode -ne 0) { throw "Unity tests failed (exit $($testProcess.ExitCode)). Read $logPath" }
    if (-not (Test-Path -LiteralPath $resultPath)) { throw 'Unity did not create the test report.' }
    [xml]$report = Get-Content -LiteralPath $resultPath -Raw
    if ($report.'test-run'.result -ne 'Passed' -or [int]$report.'test-run'.total -lt 1) {
        throw "Unity test report is not a pass: $resultPath"
    }
    Write-Output "Unity input tests passed. Report: $resultPath"
}
