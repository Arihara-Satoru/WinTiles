using System.Drawing;

namespace WinTiles.Core.Models;

public sealed class CropExportRegion
{
    public required int TileIndex { get; init; }

    public required int GridRow { get; init; }

    public required int GridColumn { get; init; }

    public required RectangleF CellBounds { get; init; }

    public required RectangleF SourceCropBounds { get; init; }
}
