using WinTiles.Core.Models;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class TileClickActionServiceTests
{
    [Fact]
    public void Validate_accepts_http_url_action()
    {
        var result = TileClickActionService.Validate(new TileClickAction
        {
            Type = TileClickActionType.OpenUrl,
            Url = "https://example.com/dashboard"
        });

        Assert.True(result.IsValid);
        Assert.True(result.HasConfiguredAction);
        Assert.Contains("打开网页", result.SummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_non_http_url_action()
    {
        var result = TileClickActionService.Validate(new TileClickAction
        {
            Type = TileClickActionType.OpenUrl,
            Url = "mailto:test@example.com"
        });

        Assert.False(result.IsValid);
        Assert.Contains("仅支持 http:// 或 https://", result.ValidationMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_missing_application_path()
    {
        var result = TileClickActionService.Validate(new TileClickAction
        {
            Type = TileClickActionType.OpenApplication,
            ApplicationPath = string.Empty
        });

        Assert.False(result.IsValid);
        Assert.Contains("请选择要启动的应用路径", result.ValidationMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_missing_application_file()
    {
        var applicationPath = Path.Combine(Path.GetTempPath(), "WinTiles.Tests", Guid.NewGuid().ToString("N"), "missing.exe");
        var result = TileClickActionService.Validate(new TileClickAction
        {
            Type = TileClickActionType.OpenApplication,
            ApplicationPath = applicationPath
        });

        Assert.False(result.IsValid);
        Assert.Contains("应用路径不存在", result.ValidationMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_trims_application_fields()
    {
        var normalizedAction = TileClickActionService.Normalize(new TileClickAction
        {
            Type = TileClickActionType.OpenApplication,
            ApplicationPath = "  C:\\Apps\\Demo.exe  ",
            Arguments = "  --flag  ",
            WorkingDirectory = "  C:\\Apps  "
        });

        Assert.NotNull(normalizedAction);
        Assert.Equal("C:\\Apps\\Demo.exe", normalizedAction.ApplicationPath);
        Assert.Equal("--flag", normalizedAction.Arguments);
        Assert.Equal("C:\\Apps", normalizedAction.WorkingDirectory);
    }

    [Fact]
    public void Validate_none_action_returns_fallback_summary()
    {
        var result = TileClickActionService.Validate(null);

        Assert.True(result.IsValid);
        Assert.False(result.HasConfiguredAction);
        Assert.Contains("打开 WinTiles", result.SummaryText, StringComparison.Ordinal);
    }
}
