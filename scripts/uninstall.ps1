[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$RemoveUserData
)

$ErrorActionPreference = 'Stop'
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
$autoAnkiRoot = [IO.Path]::GetFullPath((Join-Path $localAppData 'AutoAnki'))
$expectedParent = [IO.Path]::GetFullPath($localAppData).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $autoAnkiRoot.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($autoAnkiRoot) -ne 'AutoAnki') {
    throw "Refusing to remove unexpected path: $autoAnkiRoot"
}

if (Get-Process -Name 'AutoAnki' -ErrorAction SilentlyContinue) {
    throw 'Exit AutoAnki from its tray menu before uninstalling it.'
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -LiteralPath $runKey -Name 'AutoAnki' -ErrorAction SilentlyContinue

$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'AutoAnki.lnk'
if (Test-Path -LiteralPath $startupShortcut) {
    Remove-Item -LiteralPath $startupShortcut -Force
}

$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'AutoAnki.lnk'
if (Test-Path -LiteralPath $startMenu) {
    Remove-Item -LiteralPath $startMenu -Force
}

$appDirectory = Join-Path $autoAnkiRoot 'app'
if (Test-Path -LiteralPath $appDirectory) {
    Remove-Item -LiteralPath $appDirectory -Recurse -Force
}

if ($RemoveUserData -and (Test-Path -LiteralPath $autoAnkiRoot)) {
    Remove-Item -LiteralPath $autoAnkiRoot -Recurse -Force
    Write-Host 'AutoAnki and its local settings were removed.'
} else {
    Write-Host 'AutoAnki was removed. Local settings were preserved; use -RemoveUserData to delete them.'
}
