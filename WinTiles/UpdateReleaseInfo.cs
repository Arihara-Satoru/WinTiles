namespace WinTiles;

public sealed class UpdateReleaseInfo
{
    public required string TagName { get; init; }

    public required Version Version { get; init; }

    public required string HtmlUrl { get; init; }

    public string? DownloadUrl { get; init; }

    public string? AssetName { get; init; }
}
