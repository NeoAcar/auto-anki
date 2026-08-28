[CmdletBinding()]
param(
    [string]$Version = 'v1.0.0',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$publishPath = Join-Path $repoRoot 'artifacts\publish'
$releaseRoot = Join-Path $repoRoot 'artifacts\release'
$packageName = "AutoAnki-$Version-win-x64"
$stagingPath = Join-Path $releaseRoot $packageName
$archivePath = Join-Path $releaseRoot "$packageName.zip"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'publish.ps1')
}

$publishedExecutable = Join-Path $publishPath 'AutoAnki.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Published executable not found at $publishedExecutable"
}

$resolvedReleaseRoot = [IO.Path]::GetFullPath($releaseRoot)
$resolvedArtifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if (-not $resolvedReleaseRoot.StartsWith(
        $resolvedArtifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to package outside the artifacts directory: $resolvedReleaseRoot"
}

if (Test-Path -LiteralPath $stagingPath) {
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
Copy-Item -LiteralPath $publishedExecutable -Destination (Join-Path $stagingPath 'AutoAnki.exe')
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\Install-AutoAnki.ps1') -Destination $stagingPath
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\uninstall.ps1') -Destination (Join-Path $stagingPath 'Uninstall-AutoAnki.ps1')
Copy-Item -LiteralPath (Join-Path $repoRoot 'packaging\README.txt') -Destination $stagingPath
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $stagingPath

Compress-Archive -Path (Join-Path $stagingPath '*') -DestinationPath $archivePath -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
Set-Content -LiteralPath "$archivePath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($archivePath))"
Write-Host "Created release package: $archivePath"
