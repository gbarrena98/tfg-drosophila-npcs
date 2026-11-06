param([Parameter(Mandatory = $true)][string]$UnityEditorPath)
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw 'UnityEditorPath must point to the installed Unity.exe.'
}
$LogDirectory = Join-Path $ProjectRoot 'Logs'
New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$LogFile = Join-Path $LogDirectory 'build_windows.log'
$Arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $ProjectRoot + '"'),
    '-executeMethod', 'NpcLab.EditorTools.ProjectTools.BuildWindows', '-logFile', ('"' + $LogFile + '"'))
$Process = Start-Process -FilePath $UnityEditorPath -ArgumentList $Arguments -Wait -PassThru
if ($Process.ExitCode -ne 0) { throw "Unity build failed. Read $LogFile" }
$Executable = Join-Path $ProjectRoot 'Builds/Windows/DrosophilaNavigationLab.exe'
if (-not (Test-Path $Executable)) { throw "No Player was generated. Read $LogFile" }
Write-Host "Player generated: $Executable"
Write-Host 'Open it, complete the acceptance checks, then package the whole folder.'

