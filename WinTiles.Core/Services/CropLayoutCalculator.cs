using System.Drawing;
using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public sealed class CropLayoutCalculator
{
    public const int GridDimension = 5;

    // 吸附阈值保持得稍微宽一点，避免用户靠滚轮调到临界值时来回抖动。
    public const float MinimumScaleSnapThreshold = 0.04f;

    public RectangleF GetCellBounds(SizeF boardSize, float cellGap, int row, int column)
    {
        var cellSize = CalculateCellSize(boardSize, cellGap);
        var left = column * (cellSize + cellGap);
        var top = row * (cellSize + cellGap);
        return new RectangleF(left, top, cellSize, cellSize);
    }

    public RectangleF CalculateActiveBounds(
        SizeF boardSize,
        float cellGap,
        IReadOnlyCollection<(int Row, int Column)> activeCells)
    {
        ArgumentNullException.ThrowIfNull(activeCells);

        if (activeCells.Count == 0)
        {
            return new RectangleF(0f, 0f, boardSize.Width, boardSize.Height);
        }

        RectangleF? aggregateBounds = null;
        foreach (var (row, column) in activeCells)
        {
            var cellBounds = GetCellBounds(boardSize, cellGap, row, column);
            aggregateBounds = aggregateBounds is null
                ? cellBounds
                : RectangleF.Union(aggregateBounds.Value, cellBounds);
        }

        return aggregateBounds ?? new RectangleF(0f, 0f, boardSize.Width, boardSize.Height);
    }

    public float CalculateMinimumScale(
        SizeF sourceImageSize,
        SizeF boardSize,
        float cellGap,
        IReadOnlyCollection<(int Row, int Column)> activeCells)
    {
        var activeBounds = CalculateActiveBounds(boardSize, cellGap, activeCells);
        var widthScale = activeBounds.Width / Math.Max(1f, sourceImageSize.Width);
        var heightScale = activeBounds.Height / Math.Max(1f, sourceImageSize.Height);
        return Math.Max(widthScale, heightScale);
    }

    public PointF ClampOffset(
        SizeF sourceImageSize,
        float scale,
        SizeF boardSize,
        float cellGap,
        IReadOnlyCollection<(int Row, int Column)> activeCells,
        PointF desiredOffset)
    {
        var activeBounds = CalculateActiveBounds(boardSize, cellGap, activeCells);
        var scaledWidth = sourceImageSize.Width * scale;
        var scaledHeight = sourceImageSize.Height * scale;

        var minOffsetX = activeBounds.Right - scaledWidth;
        var maxOffsetX = activeBounds.Left;
        var minOffsetY = activeBounds.Bottom - scaledHeight;
        var maxOffsetY = activeBounds.Top;

        return new PointF(
            ClampAxis(desiredOffset.X, minOffsetX, maxOffsetX),
            ClampAxis(desiredOffset.Y, minOffsetY, maxOffsetY));
    }

    public PointF CalculateCenteredOffset(
        SizeF sourceImageSize,
        float scale,
        SizeF boardSize,
        float cellGap,
        IReadOnlyCollection<(int Row, int Column)> activeCells)
    {
        var activeBounds = CalculateActiveBounds(boardSize, cellGap, activeCells);
        var scaledWidth = sourceImageSize.Width * scale;
        var scaledHeight = sourceImageSize.Height * scale;
        var desiredOffset = new PointF(
            activeBounds.Left + (activeBounds.Width - scaledWidth) / 2f,
            activeBounds.Top + (activeBounds.Height - scaledHeight) / 2f);

        return ClampOffset(sourceImageSize, scale, boardSize, cellGap, activeCells, desiredOffset);
    }

    public IReadOnlyList<CropExportRegion> BuildExportRegions(
        SizeF sourceImageSize,
        SizeF boardSize,
        float cellGap,
        IReadOnlyCollection<(int Row, int Column)> activeCells,
        float scale,
        PointF offset)
    {
        ArgumentNullException.ThrowIfNull(activeCells);

        var sortedCells = activeCells
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .ToArray();

        var exportRegions = new List<CropExportRegion>(sortedCells.Length);
        for (var index = 0; index < sortedCells.Length; index++)
        {
            var cell = sortedCells[index];
            var cellBounds = GetCellBounds(boardSize, cellGap, cell.Row, cell.Column);
            var sourceCropBounds = new RectangleF(
                (cellBounds.Left - offset.X) / scale,
                (cellBounds.Top - offset.Y) / scale,
                cellBounds.Width / scale,
                cellBounds.Height / scale);

            exportRegions.Add(new CropExportRegion
            {
                TileIndex = index,
                GridRow = cell.Row,
                GridColumn = cell.Column,
                CellBounds = cellBounds,
                SourceCropBounds = ClampCropBounds(sourceCropBounds, sourceImageSize)
            });
        }

        return exportRegions;
    }

    public float SnapScaleToMinimum(float desiredScale, float minimumScale)
    {
        return Math.Abs(desiredScale - minimumScale) <= MinimumScaleSnapThreshold
            ? minimumScale
            : desiredScale;
    }

    public float CalculateCellSize(SizeF boardSize, float cellGap)
    {
        var totalGap = cellGap * (GridDimension - 1);
        return (boardSize.Width - totalGap) / GridDimension;
    }

    private static float ClampAxis(float value, float min, float max)
    {
        if (min > max)
        {
            return (min + max) / 2f;
        }

        return Math.Clamp(value, min, max);
    }

    private static RectangleF ClampCropBounds(RectangleF cropBounds, SizeF sourceImageSize)
    {
        var x = Math.Clamp(cropBounds.X, 0f, sourceImageSize.Width - 1f);
        var y = Math.Clamp(cropBounds.Y, 0f, sourceImageSize.Height - 1f);
        var width = Math.Clamp(cropBounds.Width, 1f, sourceImageSize.Width - x);
        var height = Math.Clamp(cropBounds.Height, 1f, sourceImageSize.Height - y);
        return new RectangleF(x, y, width, height);
    }
}
