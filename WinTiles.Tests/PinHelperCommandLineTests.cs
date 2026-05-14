using WinTiles.Core.Models;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class PinHelperCommandLineTests
{
    [Fact]
    public void BuildArguments_contains_all_expected_switches()
    {
        var commandLine = PinHelperCommandLine.BuildArguments(
            "abc123",
            @"D:\Images\sample image.png",
            TileRequestSize.Wide4x2,
            @"D:\WinTiles\WinTiles\bin\Debug\net8.0-windows\WinTiles.exe",
            @"D:\Tiles\abc123\TileHost.exe");

        Assert.Contains("pin-image", commandLine, StringComparison.Ordinal);
        Assert.Contains("--tile-id \"abc123\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("--image \"D:\\Images\\sample image.png\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("--size \"4x2\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("--launch-target", commandLine, StringComparison.Ordinal);
        Assert.Contains("--host-exe \"D:\\Tiles\\abc123\\TileHost.exe\"", commandLine, StringComparison.Ordinal);
    }
}
