namespace WinTiles.Core.Models;

public enum TileRequestSize
{
    Small1x1,
    Medium2x2,
    Wide4x2,
    Large4x4
}

public static class TileRequestSizeExtensions
{
    public static string ToDisplayText(this TileRequestSize size) => size switch
    {
        TileRequestSize.Small1x1 => "1x1",
        TileRequestSize.Medium2x2 => "2x2",
        TileRequestSize.Wide4x2 => "4x2",
        TileRequestSize.Large4x4 => "4x4",
        _ => "2x2"
    };

    public static string ToCliArgument(this TileRequestSize size) => size switch
    {
        TileRequestSize.Small1x1 => "1x1",
        TileRequestSize.Medium2x2 => "2x2",
        TileRequestSize.Wide4x2 => "4x2",
        TileRequestSize.Large4x4 => "4x4",
        _ => "2x2"
    };

    public static bool UsesExperimentalPinPath(this TileRequestSize size) =>
        size is TileRequestSize.Small1x1 or TileRequestSize.Large4x4;

    public static (double Width, double Height) ToGridSize(this TileRequestSize size) => size switch
    {
        TileRequestSize.Small1x1 => (1d, 1d),
        TileRequestSize.Medium2x2 => (2d, 2d),
        TileRequestSize.Wide4x2 => (4d, 2d),
        TileRequestSize.Large4x4 => (4d, 4d),
        _ => (2d, 2d)
    };
}
