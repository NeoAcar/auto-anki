[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$publishPath = Join-Path $repoRoot 'artifacts\publish'
$installRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AutoAnki\app'
$executable = Join-Path $installRoot 'AutoAnki.exe'
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'AutoAnki.lnk'

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'publish.ps1')
}

$publishedExecutable = Join-Path $publishPath 'AutoAnki.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable)) {
    throw "Published executable not found at $publishedExecutable"
}

if (Get-Process -Name 'AutoAnki' -ErrorAction SilentlyContinue) {
    throw 'Exit AutoAnki from its tray menu before installing or updating it.'
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -LiteralPath $publishedExecutable -Destination $executable -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($startMenu)
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = $installRoot
$shortcut.Description = 'Add selected text to Anki'
$shortcut.Save()

Start-Process -FilePath $executable -WindowStyle Hidden
Write-Host "Installed AutoAnki to $executable"
