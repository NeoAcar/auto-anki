[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$packageExecutable = Join-Path $PSScriptRoot 'AutoAnki.exe'
$installRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AutoAnki\app'
$installedExecutable = Join-Path $installRoot 'AutoAnki.exe'
$startMenuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'AutoAnki.lnk'

if (-not (Test-Path -LiteralPath $packageExecutable -PathType Leaf)) {
    throw "AutoAnki.exe was not found beside this installer: $packageExecutable"
}

if (Get-Process -Name 'AutoAnki' -ErrorAction SilentlyContinue) {
    throw 'Exit AutoAnki from its tray menu before installing or updating it.'
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -LiteralPath $packageExecutable -Destination $installedExecutable -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenuShortcut)
$shortcut.TargetPath = $installedExecutable
$shortcut.WorkingDirectory = $installRoot
$shortcut.Description = 'Add selected text to Anki'
$shortcut.Save()

Start-Process -FilePath $installedExecutable -WindowStyle Hidden
Write-Host "Installed AutoAnki to $installedExecutable"
