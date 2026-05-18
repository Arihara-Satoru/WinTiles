param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$RuntimeIdentifier = "win-x64"
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDirectory = Join-Path $repoRoot "artifacts\publish\WinTiles-$RuntimeIdentifier"
$releaseDirectory = Join-Path $repoRoot "artifacts\release"

& (Join-Path $repoRoot "build-native.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Native helper build failed."
}

$publishArguments = @(
    (Join-Path $repoRoot "WinTiles\WinTiles.csproj"),
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained", "true",
    "-p:PublishSingleFile=false",
    "-o", $publishDirectory
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    # CI 会把 release 版本显式传进来，确保程序集版本和 GitHub Release 标签一致。
    $publishArguments += "-p:Version=$Version"
}

dotnet publish @publishArguments

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null

$archiveVersion = if ([string]::IsNullOrWhiteSpace($Version)) { "local" } else { "v$Version" }
$archivePath = Join-Path $releaseDirectory "WinTiles-$archiveVersion-$RuntimeIdentifier.zip"

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

# 便携版保留目录结构，压缩后可以直接上传到 GitHub Release。
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archivePath

Write-Host "Publish completed: $publishDirectory"
Write-Host "Archive completed: $archivePath"
