using System.Net;
using System.Net.Http;

namespace WinTiles.Tests;

/// <summary>
/// 验证 GitHub Release 更新检查直接基于 releases/latest 页面时仍能稳定工作。
/// </summary>
public sealed class GitHubReleaseUpdateServiceTests
{
    /// <summary>
    /// 当 releases/latest 已跳转到具体 tag 页面时，应直接从最终地址识别版本，并提取 zip 下载链接。
    /// </summary>
    [Fact]
    public async Task CheckForUpdatesAsync_returns_update_when_latest_release_redirects_to_tag_page()
    {
        var redirectedUri = new Uri("https://github.com/Arihara-Satoru/WinTiles/releases/tag/v9.9.9");
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <html>
                      <body>
                        <a href="/Arihara-Satoru/WinTiles/releases/download/v9.9.9/WinTiles-v9.9.9-win-x64.zip">download</a>
                      </body>
                    </html>
                    """),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, redirectedUri)
            });
        using var httpClient = new HttpClient(handler);
        var service = new GitHubReleaseUpdateService("Arihara-Satoru", "WinTiles", httpClient, new Version(0, 1, 0));

        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Release);
        Assert.Equal("v9.9.9", result.Release.TagName);
        Assert.Equal(new Version(9, 9, 9), result.Release.Version);
        Assert.Equal("https://github.com/Arihara-Satoru/WinTiles/releases/tag/v9.9.9", result.Release.HtmlUrl);
        Assert.Equal(
            "https://github.com/Arihara-Satoru/WinTiles/releases/download/v9.9.9/WinTiles-v9.9.9-win-x64.zip",
            result.Release.DownloadUrl);
        Assert.Equal("WinTiles-v9.9.9-win-x64.zip", result.Release.AssetName);
    }

    /// <summary>
    /// 当最终请求地址拿不到 tag 时，应允许从页面 HTML 中兜底提取版本信息。
    /// </summary>
    [Fact]
    public async Task CheckForUpdatesAsync_falls_back_to_html_when_final_uri_does_not_include_tag()
    {
        var latestUri = new Uri("https://github.com/Arihara-Satoru/WinTiles/releases/latest");
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <html>
                      <body>
                        <a href="/Arihara-Satoru/WinTiles/releases/tag/v1.2.3">release</a>
                        <a href="/Arihara-Satoru/WinTiles/releases/download/v1.2.3/WinTiles-v1.2.3-win-x64.zip">download</a>
                      </body>
                    </html>
                    """),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, latestUri)
            });
        using var httpClient = new HttpClient(handler);
        var service = new GitHubReleaseUpdateService("Arihara-Satoru", "WinTiles", httpClient, new Version(0, 1, 0));

        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Release);
        Assert.Equal("v1.2.3", result.Release.TagName);
        Assert.Equal(new Version(1, 2, 3), result.Release.Version);
        Assert.Equal("https://github.com/Arihara-Satoru/WinTiles/releases/tag/v1.2.3", result.Release.HtmlUrl);
        Assert.Equal(
            "https://github.com/Arihara-Satoru/WinTiles/releases/download/v1.2.3/WinTiles-v1.2.3-win-x64.zip",
            result.Release.DownloadUrl);
        Assert.Equal("WinTiles-v1.2.3-win-x64.zip", result.Release.AssetName);
    }

    /// <summary>
    /// 当主页面只暴露 expanded_assets 片段入口时，应继续请求片段并提取真实的 zip 下载地址。
    /// </summary>
    [Fact]
    public async Task CheckForUpdatesAsync_fetches_expanded_assets_when_main_html_does_not_embed_zip_link()
    {
        var requestCount = 0;
        var latestUri = new Uri("https://github.com/Arihara-Satoru/WinTiles/releases/latest");
        var tagUri = new Uri("https://github.com/Arihara-Satoru/WinTiles/releases/tag/v0.1.6");
        var expandedAssetsUri = new Uri("https://github.com/Arihara-Satoru/WinTiles/releases/expanded_assets/v0.1.6");
        var handler = new StubHttpMessageHandler(request =>
        {
            requestCount++;
            if (request.RequestUri is not null &&
                request.RequestUri.AbsoluteUri.Equals(expandedAssetsUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        <div>
                          <a href="/Arihara-Satoru/WinTiles/releases/download/v0.1.6/WinTiles-v0.1.6-win-x64.zip">download</a>
                        </div>
                        """),
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, expandedAssetsUri)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <html>
                      <body>
                        <include-fragment src="https://github.com/Arihara-Satoru/WinTiles/releases/expanded_assets/v0.1.6"></include-fragment>
                      </body>
                    </html>
                    """),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, tagUri)
            };
        });
        using var httpClient = new HttpClient(handler);
        var service = new GitHubReleaseUpdateService("Arihara-Satoru", "WinTiles", httpClient, new Version(0, 1, 0));

        var result = await service.CheckForUpdatesAsync();

        Assert.Equal(2, requestCount);
        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Release);
        Assert.Equal("v0.1.6", result.Release.TagName);
        Assert.Equal(
            "https://github.com/Arihara-Satoru/WinTiles/releases/download/v0.1.6/WinTiles-v0.1.6-win-x64.zip",
            result.Release.DownloadUrl);
        Assert.Equal("WinTiles-v0.1.6-win-x64.zip", result.Release.AssetName);
    }

    /// <summary>
    /// 用可编程方式伪造 HTTP 响应，避免测试依赖真实网络环境。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        /// <summary>
        /// 初始化伪造响应处理器。
        /// </summary>
        /// <param name="responseFactory">根据请求生成响应的委托。</param>
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        /// <summary>
        /// 按测试预设返回 HTTP 响应。
        /// </summary>
        /// <param name="request">当前发送的请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>伪造的 HTTP 响应。</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
