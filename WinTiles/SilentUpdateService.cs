using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace WinTiles;

public sealed class SilentUpdateService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly string _applicationDirectory;
    private readonly string _mainExecutablePath;

    public SilentUpdateService(string applicationDirectory, string mainExecutablePath)
    {
        _applicationDirectory = applicationDirectory;
        _mainExecutablePath = mainExecutablePath;
    }

    public async Task<SilentUpdatePreparationResult> PrepareUpdateAsync(
        UpdateReleaseInfo release,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(release.DownloadUrl))
            {
                return new SilentUpdatePreparationResult
                {
                    Release = release,
                    Message = "找到新版本了，但当前 Release 没有可下载的 zip 包。"
                };
            }

            if (!CanWriteToApplicationDirectory(out var writeFailureMessage))
            {
                return new SilentUpdatePreparationResult
                {
                    Release = release,
                    Message = writeFailureMessage
                };
            }

            var packageDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinTiles",
                "Updates",
                release.TagName);
            Directory.CreateDirectory(packageDirectory);

            var packageFileName = string.IsNullOrWhiteSpace(release.AssetName)
                ? $"WinTiles-{release.TagName}.zip"
                : release.AssetName;
            var packagePath = Path.Combine(packageDirectory, packageFileName);
            if (!File.Exists(packagePath) || new FileInfo(packagePath).Length == 0)
            {
                using var response = await SharedHttpClient.GetAsync(
                    release.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var targetStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
            }

            return new SilentUpdatePreparationResult
            {
                IsReadyToInstall = true,
                Release = release,
                Message = $"新版本 {release.Version.ToString(3)} 已下载完成，是否立即重启并完成升级？",
                PackagePath = packagePath
            };
        }
        catch (Exception exception)
        {
            return new SilentUpdatePreparationResult
            {
                Release = release,
                Message = $"新版本已发现，但下载更新包失败：{exception.Message}"
            };
        }
    }

    public bool TryStartInstaller(SilentUpdatePreparationResult preparedUpdate, int currentProcessId, out string? errorMessage)
    {
        errorMessage = null;
        if (!preparedUpdate.IsReadyToInstall || string.IsNullOrWhiteSpace(preparedUpdate.PackagePath))
        {
            errorMessage = "当前还没有准备好的更新包。";
            return false;
        }

        try
        {
            var scriptDirectory = Path.Combine(Path.GetTempPath(), "WinTiles");
            Directory.CreateDirectory(scriptDirectory);

            var scriptPath = Path.Combine(scriptDirectory, $"install-update-{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptPath, BuildInstallerScript());

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                WorkingDirectory = _applicationDirectory
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-WindowStyle");
            startInfo.ArgumentList.Add("Hidden");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-ZipPath");
            startInfo.ArgumentList.Add(preparedUpdate.PackagePath);
            startInfo.ArgumentList.Add("-TargetDir");
            startInfo.ArgumentList.Add(_applicationDirectory);
            startInfo.ArgumentList.Add("-RestartExe");
            startInfo.ArgumentList.Add(_mainExecutablePath);
            startInfo.ArgumentList.Add("-WaitPid");
            startInfo.ArgumentList.Add(currentProcessId.ToString());

            Process.Start(startInfo);
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = $"启动自动升级程序失败：{exception.Message}";
            return false;
        }
    }

    private bool CanWriteToApplicationDirectory(out string failureMessage)
    {
        try
        {
            var probePath = Path.Combine(_applicationDirectory, $".update-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            failureMessage = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            failureMessage = $"发现新版本，但当前目录不可写，无法静默升级：{exception.Message}";
            return false;
        }
    }

    private static string BuildInstallerScript()
    {
        return """
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,
    [Parameter(Mandatory = $true)]
    [string]$TargetDir,
    [Parameter(Mandatory = $true)]
    [string]$RestartExe,
    [Parameter(Mandatory = $true)]
    [int]$WaitPid
)

$ErrorActionPreference = 'Stop'

try {
    Wait-Process -Id $WaitPid -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800

    $stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("WinTiles-Update-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null

    Expand-Archive -Path $ZipPath -DestinationPath $stagingRoot -Force
    Copy-Item -Path (Join-Path $stagingRoot '*') -Destination $TargetDir -Recurse -Force

    Start-Process -FilePath $RestartExe -WorkingDirectory $TargetDir
}
catch {
}
finally {
    if (Test-Path $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
    }
}
""";
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }
}
