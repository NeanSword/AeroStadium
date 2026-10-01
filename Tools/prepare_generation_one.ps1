param(
    [string]$PythonCommand = 'python',
    [string]$PnpmCommand = 'pnpm'
)

$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$converterDirectory = Join-Path $PSScriptRoot 'glb-compat'

Push-Location $converterDirectory
try {
    & $PnpmCommand install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install the pinned GLB conversion dependencies (exit $LASTEXITCODE)."
    }
}
finally {
    Pop-Location
}

& $PythonCommand (Join-Path $PSScriptRoot 'prepare_generation_one.py')
if ($LASTEXITCODE -ne 0) {
    throw "Generation I data/model preparation failed (exit $LASTEXITCODE)."
}

Write-Output 'Generation I models and catalog are ready. Run .\Tools\build.ps1 -Action Prepare to create Unity prefabs.'
