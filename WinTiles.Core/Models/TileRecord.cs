namespace WinTiles.Core.Models;

public sealed class TileRecord
{
    public required string TileId { get; init; }

    public required string SourceImagePath { get; init; }

    public string? BatchId { get; init; }

    public int? TileIndex { get; init; }

    public int? GridRow { get; init; }

    public int? GridColumn { get; init; }

    public string? PreviewImagePath { get; init; }

    public required TileRequestSize RequestedSize { get; init; }

    public required string HostExePath { get; init; }

    public required string ShortcutPath { get; init; }

    public required string AssetsVersion { get; init; }

    /// <summary>
    /// 当前磁贴自身保存的点击动作配置；首版整批共用时，会把同一动作写入每一块记录。
    /// </summary>
    public TileClickAction? ClickAction { get; init; }
}
