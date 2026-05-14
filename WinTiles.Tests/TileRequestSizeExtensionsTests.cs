using WinTiles.Core.Models;

namespace WinTiles.Tests;

public sealed class TileRequestSizeExtensionsTests
{
    [Theory]
    [InlineData(TileRequestSize.Small1x1, "1x1", "1x1", true)]
    [InlineData(TileRequestSize.Medium2x2, "2x2", "2x2", false)]
    [InlineData(TileRequestSize.Wide4x2, "4x2", "4x2", false)]
    [InlineData(TileRequestSize.Large4x4, "4x4", "4x4", true)]
    public void Extensions_return_expected_values(
        TileRequestSize size,
        string expectedDisplayText,
        string expectedCliArgument,
        bool expectedExperimentalValue)
    {
        Assert.Equal(expectedDisplayText, size.ToDisplayText());
        Assert.Equal(expectedCliArgument, size.ToCliArgument());
        Assert.Equal(expectedExperimentalValue, size.UsesExperimentalPinPath());
    }
}
