param(
    [string]$Version = "0.1.4",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\RamGuardian.App\RamGuardian.App.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $repoRoot "src\RamGuardian.App\bin\Release\net8.0-windows\$Runtime\publish"
$stageDir = Join-Path $artifactsRoot "RamGuardian-$Version-$Runtime"
$zipPath = Join-Path $artifactsRoot "RamGuardian-$Version-$Runtime.zip"
$rootExePath = Join-Path $repoRoot "RamGuardian.exe"

if (Test-Path $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

dotnet publish $projectPath `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $stageDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $publishDir "RamGuardian.exe") -Destination $rootExePath -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $stageDir "README.md")
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $stageDir "LICENSE")

Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output "Release package created: $zipPath"
Write-Output "Root executable updated: $rootExePath"
