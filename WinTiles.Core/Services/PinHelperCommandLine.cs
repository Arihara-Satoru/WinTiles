using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public static class PinHelperCommandLine
{
    public static string BuildArguments(string tileId, TileRequestSize size, string hostExePath)
    {
        return string.Join(
            " ",
            "pin-image",
            "--tile-id", Quote(tileId),
            "--size", Quote(size.ToCliArgument()),
            "--host-exe", Quote(hostExePath));
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
