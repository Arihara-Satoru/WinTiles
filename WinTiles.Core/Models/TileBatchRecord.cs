namespace WinTiles.Core.Models;

public sealed class TileBatchRecord
{
    public required string BatchId { get; init; }

    public required string Title { get; init; }

    public required string SourceImagePath { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required List<string> TileIds { get; init; }

    /// <summary>
    /// 当前批次默认使用的点击动作配置，供历史列表展示和后续扩展复用。
    /// </summary>
    public TileClickAction? DefaultClickAction { get; init; }
}
