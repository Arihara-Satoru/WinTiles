namespace WinTiles.Core.Services;

public static class TileIdentityBuilder
{
    public static string BuildAppUserModelId(string tileId) => $"WinTiles.Image.{tileId}";

    public static string BuildResolvedIdentity(string tileId, string hostExePath) =>
        $"AppUserModelID={BuildAppUserModelId(tileId)};HostExe={hostExePath}";

    public static string BuildShortcutDisplayTitle(string sourceImagePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourceImagePath);
        var safeName = string.Join(
            string.Empty,
            baseName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(safeName)
            ? "图片磁贴"
            : safeName;
    }

    public static string BuildShortcutFileName(string sourceImagePath, string tileId)
    {
        var safeName = BuildShortcutDisplayTitle(sourceImagePath);

        var suffix = tileId.Length >= 8 ? tileId[..8] : tileId;
        return $"{safeName}-{suffix}.lnk";
    }
}
