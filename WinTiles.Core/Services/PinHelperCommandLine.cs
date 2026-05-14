using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public static class PinHelperCommandLine
{
    public static string BuildArguments(string tileId, string imagePath, TileRequestSize size, string launchTarget, string hostExePath)
    {
        return string.Join(
            " ",
            "pin-image",
            "--tile-id", Quote(tileId),
            "--image", Quote(imagePath),
            "--size", Quote(size.ToCliArgument()),
            "--launch-target", Quote(launchTarget),
            "--host-exe", Quote(hostExePath));
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
