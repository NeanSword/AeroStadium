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
$legacyPath = Join-Path $projectDirectory 'Assets/AeroStadium/Resources/LocalModels'
$holdPath = Join-Path $projectDirectory 'Assets/AeroStadium/LegacyModelsBuildHold'
foreach ($targetPath in @($legacyPath, $holdPath)) {
    if (-not [IO.Path]::GetFullPath($targetPath).StartsWith($projectDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Build recovery target outside this project.'
    }
}
$hadLegacy = Test-Path -LiteralPath $legacyPath
if ($Action -eq 'BuildWindows' -and ((Test-Path -LiteralPath $holdPath) -or (Test-Path -LiteralPath ($holdPath + '.meta')))) {
    throw 'A previous build left LegacyModelsBuildHold. Inspect and restore it before another build.'
}
try {
    $buildProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    $buildProcess.WaitForExit()
} finally {
    # A native Unity crash bypasses the C# scope that normally restores this folder.
    if ($Action -eq 'BuildWindows' -and $hadLegacy -and (Test-Path -LiteralPath $holdPath)) {
        if (Test-Path -LiteralPath $legacyPath) { throw 'Both fallback folders exist; inspect before recovery.' }
        Move-Item -LiteralPath $holdPath -Destination $legacyPath
        if (Test-Path -LiteralPath ($holdPath + '.meta')) {
            Move-Item -LiteralPath ($holdPath + '.meta') -Destination ($legacyPath + '.meta')
        }
    }
}
if ($buildProcess.ExitCode -ne 0) {
    throw "Unity $Action failed (exit $($buildProcess.ExitCode)). Read $logPath"
}
Write-Output "Unity $Action succeeded. Log: $logPath"
