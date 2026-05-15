namespace WinTiles.Core.Services;

public static class TileIdentityBuilder
{
    public static string BuildAppUserModelId(string tileId) => $"WinTiles.Image.{tileId}";

    public static string BuildBatchDisplayTitle(string sourceImagePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourceImagePath);
        var safeName = string.Join(
            string.Empty,
            baseName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(safeName)
            ? "图片磁贴"
            : safeName;
    }

    public static string BuildShortcutDisplayTitle(string sourceImagePath, int? gridRow = null, int? gridColumn = null)
    {
        var baseTitle = BuildBatchDisplayTitle(sourceImagePath);
        if (!gridRow.HasValue || !gridColumn.HasValue)
        {
            return baseTitle;
        }

        return $"{baseTitle} {gridRow.Value + 1}-{gridColumn.Value + 1}";
    }

    public static string BuildShortcutFileName(
        string sourceImagePath,
        string tileId,
        int? gridRow = null,
        int? gridColumn = null)
    {
        var safeName = BuildShortcutDisplayTitle(sourceImagePath, gridRow, gridColumn);

        var suffix = tileId.Length >= 8 ? tileId[..8] : tileId;
        return $"{safeName}-{suffix}.lnk";
    }
}
