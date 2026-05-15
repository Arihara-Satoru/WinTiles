namespace WinTiles.Core.Models;

public sealed class TileBatchRecord
{
    public required string BatchId { get; init; }

    public required string Title { get; init; }

    public required string SourceImagePath { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required List<string> TileIds { get; init; }
}
