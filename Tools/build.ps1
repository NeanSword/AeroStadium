param(
    [ValidateSet('Prepare', 'Validation', 'BuildWindows')]
    [string]$Action = 'BuildWindows',
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw "Unity editor missing: $UnityEditor. Pass -UnityEditor with your editor path."
}
$logDirectory = Join-Path $projectDirectory 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory ($Action.ToLowerInvariant() + '.log')
$unityArguments = '-batchmode -nographics -quit -buildTarget Win64 -projectPath "' + $projectDirectory + '" -executeMethod AeroStadium.EditorTools.ProjectBuild.' + $Action + ' -logFile "' + $logPath + '"'
# Unity is a GUI application: explicitly wait for its process on Windows.
$buildProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
if ($buildProcess.ExitCode -ne 0) {
    throw "Unity $Action failed (exit $($buildProcess.ExitCode)). Read $logPath"
}
Write-Output "Unity $Action succeeded. Log: $logPath"
