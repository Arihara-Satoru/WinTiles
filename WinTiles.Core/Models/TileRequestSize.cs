namespace WinTiles.Core.Models;

public enum TileRequestSize
{
    Medium2x2,
    Wide4x2
}

public static class TileRequestSizeExtensions
{
    public static string ToDisplayText(this TileRequestSize size) => size switch
    {
        TileRequestSize.Wide4x2 => "4x2",
        _ => "2x2"
    };

    public static string ToCliArgument(this TileRequestSize size) => size switch
    {
        TileRequestSize.Wide4x2 => "4x2",
        _ => "2x2"
    };
}
