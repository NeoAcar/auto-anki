[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repoRoot 'src\AutoAnki.App\AutoAnki.App.csproj'
$outputPath = Join-Path $repoRoot 'artifacts\publish'

dotnet test (Join-Path $repoRoot 'AutoAnki.sln') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; publish was stopped.' }

dotnet publish $projectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Write-Host "Published AutoAnki to $outputPath"
