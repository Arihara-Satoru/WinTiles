namespace WinTiles.Core.Models;

public sealed class TileRecord
{
    public required string TileId { get; init; }

    public required string SourceImagePath { get; init; }

    public required TileRequestSize RequestedSize { get; init; }

    public required string HostExePath { get; init; }

    public required string ShortcutPath { get; init; }

    public required string AssetsVersion { get; init; }
}
