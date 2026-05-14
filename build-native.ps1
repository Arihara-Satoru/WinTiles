param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$Platform = "x64"
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe was not found."
}

# Use VS2022 Build Tools so the native projects can target v143 consistently.
$msbuild = & $vswhere -latest -products * -version '[17.0,18.0)' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild.exe was not found."
}

$projects = @(
    (Join-Path $repoRoot "native\TileHost\TileHost.vcxproj"),
    (Join-Path $repoRoot "native\WinTiles.PinHelper\WinTiles.PinHelper.vcxproj")
)

foreach ($project in $projects) {
    & $msbuild $project /m /nologo /p:Configuration=$Configuration /p:Platform=$Platform /t:Build
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $project"
    }
}

$toolsDirectory = Join-Path $repoRoot "WinTiles\Tools"
New-Item -ItemType Directory -Force -Path $toolsDirectory | Out-Null

Copy-Item (Join-Path $repoRoot "native\TileHost\bin\$Platform\$Configuration\TileHost.exe") $toolsDirectory -Force
Copy-Item (Join-Path $repoRoot "native\WinTiles.PinHelper\bin\$Platform\$Configuration\WinTiles.PinHelper.exe") $toolsDirectory -Force

Write-Host "Copied native tools to $toolsDirectory"
