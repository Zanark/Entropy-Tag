[CmdletBinding()]
param(
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
$logPath = Join-Path $projectPath 'Builds\Windows-development.log'
$outputPath = Join-Path $projectPath 'Builds\Windows\EntropyTag.exe'
New-Item -ItemType Directory -Path (Split-Path $logPath) -Force | Out-Null

if (-not (Test-Path $unityPath)) {
    throw "Unity $editorVersion was not found at '$unityPath'."
}

$arguments = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-projectPath', $projectPath,
    '-executeMethod', 'EntropyTag.Editor.EntropyTagBuildCommand.BuildWindowsDevelopment',
    '-logFile', $logPath
)

$process = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru

if ($process.ExitCode -ne 0) {
    throw "Unity development build failed with exit code $($process.ExitCode). See '$logPath'."
}

if (-not (Test-Path $outputPath)) {
    throw "Unity reported success but did not create '$outputPath'."
}

Write-Host "Unity development build completed: $outputPath"
