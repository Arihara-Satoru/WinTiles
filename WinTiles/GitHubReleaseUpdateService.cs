using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace WinTiles;

public sealed class GitHubReleaseUpdateService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly string _owner;
    private readonly string _repository;
    private readonly Version _currentVersion;
    private readonly string _currentVersionText;

    public GitHubReleaseUpdateService(string owner, string repository)
    {
        _owner = owner;
        _repository = repository;
        _currentVersion = ResolveCurrentVersion();
        _currentVersionText = _currentVersion.ToString(3);
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{_owner}/{_repository}/releases?per_page=5");
            request.Headers.UserAgent.ParseAdd($"WinTiles/{_currentVersionText}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var jsonDocument = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var release = ParseLatestRelease(jsonDocument.RootElement);
            if (release is null)
            {
                return CreateFailureResult("已连接 GitHub，但没有读到可识别的版本号。");
            }

            var hasUpdate = release.Version > _currentVersion;
            return new UpdateCheckResult
            {
                CurrentVersion = _currentVersion,
                CurrentVersionText = _currentVersionText,
                IsUpdateAvailable = hasUpdate,
                Release = release,
                SummaryText = hasUpdate
                    ? $"发现新版本 {release.Version.ToString(3)}，当前版本 {_currentVersionText}。"
                    : $"当前已是最新版本 {_currentVersionText}。"
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return CreateFailureResult($"检查更新失败：{exception.Message}");
        }
    }

    private UpdateCheckResult CreateFailureResult(string errorMessage)
    {
        return new UpdateCheckResult
        {
            CurrentVersion = _currentVersion,
            CurrentVersionText = _currentVersionText,
            SummaryText = $"当前版本 {_currentVersionText}。",
            ErrorMessage = errorMessage
        };
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private static UpdateReleaseInfo? ParseLatestRelease(JsonElement rootElement)
    {
        if (rootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var releaseElement in rootElement.EnumerateArray())
        {
            var release = ParseRelease(releaseElement);
            if (release is not null)
            {
                return release;
            }
        }

        return null;
    }

    private static UpdateReleaseInfo? ParseRelease(JsonElement releaseElement)
    {
        if (!releaseElement.TryGetProperty("tag_name", out var tagNameElement))
        {
            return null;
        }

        var tagName = tagNameElement.GetString();
        var version = TryParseVersion(tagName);
        if (string.IsNullOrWhiteSpace(tagName) || version is null)
        {
            return null;
        }

        var htmlUrl = releaseElement.TryGetProperty("html_url", out var htmlUrlElement)
            ? htmlUrlElement.GetString()
            : null;
        var (downloadUrl, assetName) = FindPreferredAsset(releaseElement);

        return new UpdateReleaseInfo
        {
            TagName = tagName,
            Version = version,
            HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? "https://github.com/Arihara-Satoru/WinTiles/releases" : htmlUrl,
            DownloadUrl = downloadUrl,
            AssetName = assetName
        };
    }

    private static (string? DownloadUrl, string? AssetName) FindPreferredAsset(JsonElement releaseElement)
    {
        if (!releaseElement.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        foreach (var assetElement in assetsElement.EnumerateArray())
        {
            var assetName = assetElement.TryGetProperty("name", out var assetNameElement)
                ? assetNameElement.GetString()
                : null;
            var downloadUrl = assetElement.TryGetProperty("browser_download_url", out var downloadUrlElement)
                ? downloadUrlElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(assetName) &&
                assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(downloadUrl))
            {
                return (downloadUrl, assetName);
            }
        }

        return (null, null);
    }

    private static Version ResolveCurrentVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = entryAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var parsedVersion = TryParseVersion(informationalVersion);
        if (parsedVersion is not null)
        {
            return parsedVersion;
        }

        return entryAssembly.GetName().Version ?? new Version(0, 0, 0);
    }

    private static Version? TryParseVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return null;
        }

        // 同时兼容 v1.2.3、1.2.3+sha、1.2.3-beta 这类常见 release/tag 写法。
        var normalizedVersion = rawVersion.Trim();
        if (normalizedVersion.StartsWith('v') || normalizedVersion.StartsWith('V'))
        {
            normalizedVersion = normalizedVersion[1..];
        }

        var separatorIndex = normalizedVersion.IndexOfAny(['-', '+']);
        if (separatorIndex >= 0)
        {
            normalizedVersion = normalizedVersion[..separatorIndex];
        }

        return Version.TryParse(normalizedVersion, out var parsedVersion)
            ? parsedVersion
            : null;
    }
}
