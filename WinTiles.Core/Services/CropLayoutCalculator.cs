using System.Drawing;
using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public sealed class CropLayoutCalculator
{
    public const int GridDimension = 5;

    // 吸附阈值保持得稍微宽一点，避免用户靠滚轮调到临界值时来回抖动。
    public const float MinimumScaleSnapThreshold = 0.04f;

    /// <summary>
    /// 根据当前启用的行列数量，计算单个裁切格子在画板中的位置。
    /// </summary>
    /// <param name="boardSize">当前实际裁切画板尺寸。</param>
    /// <param name="cellGap">格子之间的间距。</param>
    /// <param name="row">目标格子的零基行号。</param>
    /// <param name="column">目标格子的零基列号。</param>
    /// <param name="rowCount">当前启用的总行数。</param>
    /// <param name="columnCount">当前启用的总列数。</param>
    /// <returns>目标格子的矩形边界。</returns>
    public RectangleF GetCellBounds(SizeF boardSize, float cellGap, int row, int column, int rowCount, int columnCount)
    {
        var cellSize = CalculateCellSize(boardSize, cellGap, rowCount, columnCount);
        var left = column * (cellSize + cellGap);
        var top = row * (cellSize + cellGap);
        return new RectangleF(left, top, cellSize, cellSize);
    }

    /// <summary>
    /// 根据可用区域和当前行列数量，计算真实裁切画板应占用的矩形尺寸。
    /// </summary>
    /// <param name="availableSize">可用于放置裁切画板的最大区域。</param>
    /// <param name="cellGap">格子之间的间距。</param>
    /// <param name="rowCount">当前启用的总行数。</param>
    /// <param name="columnCount">当前启用的总列数。</param>
    /// <returns>能够完整容纳当前矩形裁切区域的实际画板尺寸。</returns>
    public SizeF CalculateBoardSize(SizeF availableSize, float cellGap, int rowCount, int columnCount)
    {
        var normalizedRowCount = Math.Clamp(rowCount, 1, GridDimension);
        var normalizedColumnCount = Math.Clamp(columnCount, 1, GridDimension);
        var cellSize = CalculateCellSize(availableSize, cellGap, normalizedRowCount, normalizedColumnCount);
        var width = cellSize * normalizedColumnCount + cellGap * Math.Max(0, normalizedColumnCount - 1);
        var height = cellSize * normalizedRowCount + cellGap * Math.Max(0, normalizedRowCount - 1);
        return new SizeF(width, height);
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

        var (rowCount, columnCount) = ResolveLayoutDimensions(activeCells);
        RectangleF? aggregateBounds = null;
        foreach (var (row, column) in activeCells)
        {
            var cellBounds = GetCellBounds(boardSize, cellGap, row, column, rowCount, columnCount);
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
        var (rowCount, columnCount) = ResolveLayoutDimensions(sortedCells);

        var exportRegions = new List<CropExportRegion>(sortedCells.Length);
        for (var index = 0; index < sortedCells.Length; index++)
        {
            var cell = sortedCells[index];
            var cellBounds = GetCellBounds(boardSize, cellGap, cell.Row, cell.Column, rowCount, columnCount);
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

    /// <summary>
    /// 计算当前行列布局下单个裁切格子的边长。
    /// </summary>
    /// <param name="boardSize">当前实际裁切画板尺寸或可用区域尺寸。</param>
    /// <param name="cellGap">格子之间的间距。</param>
    /// <param name="rowCount">当前启用的总行数。</param>
    /// <param name="columnCount">当前启用的总列数。</param>
    /// <returns>单个正方形格子的边长。</returns>
    public float CalculateCellSize(SizeF boardSize, float cellGap, int rowCount, int columnCount)
    {
        var normalizedRowCount = Math.Clamp(rowCount, 1, GridDimension);
        var normalizedColumnCount = Math.Clamp(columnCount, 1, GridDimension);
        var totalHorizontalGap = cellGap * Math.Max(0, normalizedColumnCount - 1);
        var totalVerticalGap = cellGap * Math.Max(0, normalizedRowCount - 1);
        var widthDrivenCellSize = (boardSize.Width - totalHorizontalGap) / normalizedColumnCount;
        var heightDrivenCellSize = (boardSize.Height - totalVerticalGap) / normalizedRowCount;
        return Math.Max(1f, Math.Min(widthDrivenCellSize, heightDrivenCellSize));
    }

    /// <summary>
    /// 从当前活跃格子集合里推导实际使用的行列数量。
    /// </summary>
    private static (int RowCount, int ColumnCount) ResolveLayoutDimensions(IReadOnlyCollection<(int Row, int Column)> activeCells)
    {
        if (activeCells.Count == 0)
        {
            return (1, 1);
        }

        var rowCount = activeCells.Max(cell => cell.Row) + 1;
        var columnCount = activeCells.Max(cell => cell.Column) + 1;
        return (rowCount, columnCount);
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
