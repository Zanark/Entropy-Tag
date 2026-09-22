[CmdletBinding()]
param(
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$TestPlatform = 'EditMode',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\EntropyTag')
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path $ProjectPath).Path
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
$versionLine = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1

if (-not $versionLine) {
    throw "Could not read Unity editor version from '$versionFile'."
}

$editorVersion = $versionLine.Matches[0].Groups[1].Value.Trim()
$unityPath = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"

if (-not (Test-Path $unityPath)) {
    throw "Unity $editorVersion was not found at '$unityPath'."
}

$resultsDirectory = Join-Path $projectPath 'TestResults'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$resultsPath = Join-Path $resultsDirectory "$TestPlatform-results.xml"
$logPath = Join-Path $resultsDirectory "$TestPlatform.log"

if (Test-Path $resultsPath) {
    Remove-Item -LiteralPath $resultsPath -Force
}

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $projectPath,
    '-runTests',
    '-testPlatform', $TestPlatform,
    '-testResults', $resultsPath,
    '-logFile', $logPath
)

$process = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru

if ($process.ExitCode -ne 0) {
    throw "Unity $TestPlatform tests failed with exit code $($process.ExitCode). See '$logPath'."
}

if (-not (Test-Path $resultsPath)) {
    throw "Unity reported success but did not create '$resultsPath'."
}

Write-Host "Unity $TestPlatform tests passed. Results: $resultsPath"
