using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class VisualElementsManifestBuilderTests
{
    [Fact]
    public void Build_contains_all_expected_asset_paths()
    {
        var builder = new VisualElementsManifestBuilder();

        var manifest = builder.Build();

        Assert.Contains("Square70x70Logo=\"Assets\\Square70x70Logo.png\"", manifest, StringComparison.Ordinal);
        Assert.Contains("Square150x150Logo=\"Assets\\Square150x150Logo.png\"", manifest, StringComparison.Ordinal);
        Assert.Contains("Wide310x150Logo=\"Assets\\Wide310x150Logo.png\"", manifest, StringComparison.Ordinal);
        Assert.Contains("Square310x310Logo=\"Assets\\Square310x310Logo.png\"", manifest, StringComparison.Ordinal);
        Assert.Contains("ShowNameOnSquare150x150Logo=\"off\"", manifest, StringComparison.Ordinal);
        Assert.Contains("ShowNameOnWide310x150Logo=\"off\"", manifest, StringComparison.Ordinal);
    }
}
