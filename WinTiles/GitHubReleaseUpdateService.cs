using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;

namespace WinTiles;

/// <summary>
/// 负责通过 GitHub Releases 页面检查新版本，避免依赖匿名 REST API。
/// </summary>
public sealed class GitHubReleaseUpdateService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly string _owner;
    private readonly string _repository;
    private readonly Version _currentVersion;
    private readonly string _currentVersionText;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 初始化更新检查服务。
    /// </summary>
    /// <param name="owner">GitHub 仓库所有者名称。</param>
    /// <param name="repository">GitHub 仓库名称。</param>
    public GitHubReleaseUpdateService(string owner, string repository)
        : this(owner, repository, SharedHttpClient, null)
    {
    }

    /// <summary>
    /// 初始化更新检查服务，并允许在测试中注入自定义的 HttpClient。
    /// </summary>
    /// <param name="owner">GitHub 仓库所有者名称。</param>
    /// <param name="repository">GitHub 仓库名称。</param>
    /// <param name="httpClient">用于发起网络请求的 HttpClient。</param>
    public GitHubReleaseUpdateService(string owner, string repository, HttpClient httpClient)
        : this(owner, repository, httpClient, null)
    {
    }

    /// <summary>
    /// 初始化更新检查服务，并允许在测试中同时注入自定义 HttpClient 和当前版本号。
    /// </summary>
    /// <param name="owner">GitHub 仓库所有者名称。</param>
    /// <param name="repository">GitHub 仓库名称。</param>
    /// <param name="httpClient">用于发起网络请求的 HttpClient。</param>
    /// <param name="currentVersionOverride">用于覆盖当前程序版本号的测试值；为空时自动解析真实版本。</param>
    public GitHubReleaseUpdateService(string owner, string repository, HttpClient httpClient, Version? currentVersionOverride)
    {
        _owner = owner;
        _repository = repository;
        _httpClient = httpClient;
        _currentVersion = currentVersionOverride ?? ResolveCurrentVersion();
        _currentVersionText = _currentVersion.ToString(3);
    }

    /// <summary>
    /// 检查当前应用是否存在可用更新。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含当前版本、目标版本和错误信息的检查结果。</returns>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateLatestReleasePageRequestMessage();
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return CreateFailureResult(
                    $"检查更新失败：更新源 {_owner}/{_repository} 返回 404。请确认当前运行的是最新构建，或检查仓库是否仍然公开可访问。");
            }

            if (!response.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    $"检查更新失败：GitHub Release 页面返回 {(int)response.StatusCode} {response.ReasonPhrase}。");
            }

            var release = await ParseLatestReleaseFromPageAsync(response, cancellationToken).ConfigureAwait(false);
            if (release is null)
            {
                return CreateFailureResult("检查更新失败：已连接 GitHub Release 页面，但没有识别到可用的版本信息。");
            }

            return CreateSuccessResult(release);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return CreateFailureResult($"检查更新失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 从 releases/latest 页面响应中提取最新版本信息和可下载的 zip 资源。
    /// </summary>
    /// <param name="response">网页方式返回的响应。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>解析出的最新版本信息；如果无法识别则返回空。</returns>
    private async Task<UpdateReleaseInfo?> ParseLatestReleaseFromPageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var finalUri = response.RequestMessage?.RequestUri;
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var release = TryParseReleaseFromPage(finalUri, html);
        if (release is null || !string.IsNullOrWhiteSpace(release.DownloadUrl))
        {
            return release;
        }

        var expandedAssetsUrl = TryParseExpandedAssetsUrl(html, release.TagName);
        if (string.IsNullOrWhiteSpace(expandedAssetsUrl))
        {
            return release;
        }

        var assetInfo = await TryFetchZipAssetFromExpandedAssetsAsync(expandedAssetsUrl, release.TagName, cancellationToken).ConfigureAwait(false);
        if (assetInfo.DownloadUrl is null)
        {
            return release;
        }

        return new UpdateReleaseInfo
        {
            TagName = release.TagName,
            Version = release.Version,
            HtmlUrl = release.HtmlUrl,
            DownloadUrl = assetInfo.DownloadUrl,
            AssetName = assetInfo.AssetName
        };
    }

    /// <summary>
    /// 同时利用最终跳转地址和页面 HTML 提取 Release 信息。
    /// </summary>
    /// <param name="requestUri">最终响应地址。</param>
    /// <param name="html">页面 HTML 内容。</param>
    /// <returns>解析出的 Release 信息；失败时返回空。</returns>
    private UpdateReleaseInfo? TryParseReleaseFromPage(Uri? requestUri, string html)
    {
        if (!TryParseReleaseTagName(requestUri, html, out var tagName, out var htmlUrl) || string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        var version = TryParseVersion(tagName);
        if (version is null)
        {
            return null;
        }

        var (downloadUrl, assetName) = TryParseZipAssetFromHtml(html, tagName);
        return new UpdateReleaseInfo
        {
            TagName = tagName,
            Version = version,
            HtmlUrl = htmlUrl,
            DownloadUrl = downloadUrl,
            AssetName = assetName
        };
    }

    /// <summary>
    /// 优先从最终跳转地址解析 tag；如果拿不到，再从页面 HTML 中提取 tag 链接。
    /// </summary>
    /// <param name="requestUri">最终响应地址。</param>
    /// <param name="html">页面 HTML 内容。</param>
    /// <param name="tagName">解析到的 tag 名称。</param>
    /// <param name="htmlUrl">对应的 Release 页面地址。</param>
    /// <returns>解析成功则返回 true。</returns>
    private bool TryParseReleaseTagName(Uri? requestUri, string html, out string? tagName, out string htmlUrl)
    {
        if (TryParseTagNameFromUri(requestUri, out tagName, out htmlUrl))
        {
            return true;
        }

        return TryParseTagNameFromHtml(html, out tagName, out htmlUrl);
    }

    /// <summary>
    /// 根据最终跳转到的 tag 页面地址解析版本标签。
    /// </summary>
    /// <param name="requestUri">最终响应地址。</param>
    /// <param name="tagName">解析到的 tag 名称。</param>
    /// <param name="htmlUrl">对应的 Release 页面地址。</param>
    /// <returns>解析成功则返回 true。</returns>
    private bool TryParseTagNameFromUri(Uri? requestUri, out string? tagName, out string htmlUrl)
    {
        tagName = null;
        htmlUrl = $"https://github.com/{_owner}/{_repository}/releases";
        if (requestUri is null)
        {
            return false;
        }

        var segments = requestUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5 || !string.Equals(segments[^2], "tag", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        tagName = Uri.UnescapeDataString(segments[^1]);
        htmlUrl = requestUri.GetLeftPart(UriPartial.Path);
        return !string.IsNullOrWhiteSpace(tagName);
    }

    /// <summary>
    /// 作为兜底，从页面 HTML 中提取 `/releases/tag/xxx` 链接里的版本标签。
    /// </summary>
    /// <param name="html">Release 页面 HTML 内容。</param>
    /// <param name="tagName">解析到的 tag 名称。</param>
    /// <param name="htmlUrl">对应的 Release 页面地址。</param>
    /// <returns>解析成功则返回 true。</returns>
    private bool TryParseTagNameFromHtml(string html, out string? tagName, out string htmlUrl)
    {
        tagName = null;
        htmlUrl = $"https://github.com/{_owner}/{_repository}/releases";
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var marker = $"/{_owner}/{_repository}/releases/tag/";
        var markerIndex = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var tagStartIndex = markerIndex + marker.Length;
        var tagEndIndex = html.IndexOfAny(['"', '\'', '?', '#', '<'], tagStartIndex);
        if (tagEndIndex < 0)
        {
            tagEndIndex = html.Length;
        }

        var encodedTagName = html[tagStartIndex..tagEndIndex];
        if (string.IsNullOrWhiteSpace(encodedTagName))
        {
            return false;
        }

        tagName = WebUtility.HtmlDecode(Uri.UnescapeDataString(encodedTagName));
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        htmlUrl = $"https://github.com/{_owner}/{_repository}/releases/tag/{Uri.EscapeDataString(tagName)}";
        return true;
    }

    /// <summary>
    /// 从 Release 页面 HTML 中提取第一个 zip 包下载地址，便于继续走应用内静默升级。
    /// </summary>
    /// <param name="html">Release 页面 HTML 内容。</param>
    /// <param name="tagName">当前 Release 的 tag 名称。</param>
    /// <returns>下载地址和资源名称。</returns>
    private (string? DownloadUrl, string? AssetName) TryParseZipAssetFromHtml(string html, string tagName)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return (null, null);
        }

        var marker = $"/{_owner}/{_repository}/releases/download/{tagName}/";
        var searchStartIndex = 0;
        while (searchStartIndex < html.Length)
        {
            var markerIndex = html.IndexOf(marker, searchStartIndex, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return (null, null);
            }

            var pathEndIndex = html.IndexOfAny(['"', '\'', '?', '#', '<'], markerIndex);
            if (pathEndIndex < 0)
            {
                pathEndIndex = html.Length;
            }

            var encodedRelativePath = WebUtility.HtmlDecode(html[markerIndex..pathEndIndex]);
            var relativePath = Uri.UnescapeDataString(encodedRelativePath);
            if (relativePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var assetName = relativePath[(relativePath.LastIndexOf('/') + 1)..];
                return ($"https://github.com{relativePath}", assetName);
            }

            searchStartIndex = markerIndex + marker.Length;
        }

        return (null, null);
    }

    /// <summary>
    /// 从 Release 页面 HTML 中提取 expanded_assets 片段地址，供后续加载真实资源列表。
    /// </summary>
    /// <param name="html">Release 页面 HTML 内容。</param>
    /// <param name="tagName">当前 Release 的 tag 名称。</param>
    /// <returns>expanded_assets 片段地址；提取失败时返回空。</returns>
    private string? TryParseExpandedAssetsUrl(string html, string tagName)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var marker = $"/{_owner}/{_repository}/releases/expanded_assets/{tagName}";
        var markerIndex = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var urlEndIndex = html.IndexOfAny(['"', '\'', '?', '#', '<', '&'], markerIndex);
        if (urlEndIndex < 0)
        {
            urlEndIndex = html.Length;
        }

        var relativePath = WebUtility.HtmlDecode(html[markerIndex..urlEndIndex]);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return relativePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : $"https://github.com{relativePath}";
    }

    /// <summary>
    /// 请求 GitHub 的 expanded_assets 片段，并从中提取 zip 下载地址。
    /// </summary>
    /// <param name="expandedAssetsUrl">expanded_assets 片段地址。</param>
    /// <param name="tagName">当前 Release 的 tag 名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提取到的 zip 下载地址和资源名称。</returns>
    private async Task<(string? DownloadUrl, string? AssetName)> TryFetchZipAssetFromExpandedAssetsAsync(
        string expandedAssetsUrl,
        string tagName,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequestMessage(expandedAssetsUrl, "text/fragment+html");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return (null, null);
        }

        var expandedAssetsHtml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return TryParseZipAssetFromHtml(expandedAssetsHtml, tagName);
    }

    /// <summary>
    /// 创建 GitHub 最新 Release 页面请求消息。
    /// </summary>
    /// <returns>带有必要请求头的 HTTP 请求。</returns>
    private HttpRequestMessage CreateLatestReleasePageRequestMessage()
    {
        return CreateRequestMessage(
            $"https://github.com/{_owner}/{_repository}/releases/latest",
            "text/html");
    }

    /// <summary>
    /// 创建统一的 HTTP 请求，并补齐 GitHub 需要的 User-Agent 与 Accept 头。
    /// </summary>
    /// <param name="requestUri">目标地址。</param>
    /// <param name="acceptMediaType">期望的响应类型。</param>
    /// <returns>可直接发送的 HTTP 请求。</returns>
    private HttpRequestMessage CreateRequestMessage(string requestUri, string acceptMediaType)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd($"WinTiles/{_currentVersionText}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptMediaType));
        return request;
    }

    /// <summary>
    /// 统一创建成功结果。
    /// </summary>
    /// <param name="release">解析得到的最新 Release。</param>
    /// <returns>更新检查结果。</returns>
    private UpdateCheckResult CreateSuccessResult(UpdateReleaseInfo release)
    {
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

    /// <summary>
    /// 创建统一的失败结果。
    /// </summary>
    /// <param name="errorMessage">需要展示给用户的错误信息。</param>
    /// <returns>更新检查结果。</returns>
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

    /// <summary>
    /// 创建默认的共享 HttpClient。
    /// </summary>
    /// <returns>复用的 HttpClient 实例。</returns>
    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// 解析当前程序版本号，优先使用 InformationalVersion，以兼容发布时注入的版本标签。
    /// </summary>
    /// <returns>当前程序版本。</returns>
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

    /// <summary>
    /// 把常见的 tag 版本格式规范化为 System.Version。
    /// </summary>
    /// <param name="rawVersion">原始版本字符串。</param>
    /// <returns>成功解析出的版本；失败时返回空。</returns>
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
