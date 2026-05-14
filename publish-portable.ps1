param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDirectory = Join-Path $repoRoot "artifacts\publish\WinTiles"

& (Join-Path $repoRoot "build-native.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Native helper build failed."
}

dotnet publish (Join-Path $repoRoot "WinTiles\WinTiles.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Write-Host "Publish completed: $publishDirectory"
