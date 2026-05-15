using WinTiles.Core.Models;

namespace WinTiles.Tests;

public sealed class TileRequestSizeExtensionsTests
{
    [Theory]
    [InlineData(TileRequestSize.Medium2x2, "2x2", "2x2")]
    [InlineData(TileRequestSize.Wide4x2, "4x2", "4x2")]
    public void Extensions_return_expected_values(
        TileRequestSize size,
        string expectedDisplayText,
        string expectedCliArgument)
    {
        Assert.Equal(expectedDisplayText, size.ToDisplayText());
        Assert.Equal(expectedCliArgument, size.ToCliArgument());
    }
}
