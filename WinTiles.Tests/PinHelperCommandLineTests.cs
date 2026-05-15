using WinTiles.Core.Models;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class PinHelperCommandLineTests
{
    [Fact]
    public void BuildPinArguments_contains_all_expected_switches()
    {
        var commandLine = PinHelperCommandLine.BuildPinArguments(
            "abc123",
            TileRequestSize.Wide4x2,
            @"D:\Tiles\abc123\TileHost.exe");

        Assert.Contains("pin-image", commandLine, StringComparison.Ordinal);
        Assert.Contains("--tile-id \"abc123\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("--size \"4x2\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("--host-exe \"D:\\Tiles\\abc123\\TileHost.exe\"", commandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("--image", commandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("--launch-target", commandLine, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUnpinArguments_contains_only_tile_identifier()
    {
        var commandLine = PinHelperCommandLine.BuildUnpinArguments("abc123");

        Assert.Contains("unpin-image", commandLine, StringComparison.Ordinal);
        Assert.Contains("--tile-id \"abc123\"", commandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("--size", commandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("--host-exe", commandLine, StringComparison.Ordinal);
    }
}
