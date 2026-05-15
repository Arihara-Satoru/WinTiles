using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public static class PinHelperCommandLine
{
    public static string BuildPinArguments(string tileId, TileRequestSize size, string hostExePath)
    {
        return string.Join(
            " ",
            "pin-image",
            "--tile-id", Quote(tileId),
            "--size", Quote(size.ToCliArgument()),
            "--host-exe", Quote(hostExePath));
    }

    public static string BuildUnpinArguments(string tileId)
    {
        return string.Join(
            " ",
            "unpin-image",
            "--tile-id", Quote(tileId));
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
