using System.Drawing;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class CropLayoutCalculatorTests
{
    private readonly CropLayoutCalculator _calculator = new();

    [Fact]
    public void CalculateBoardSize_expands_two_by_two_to_fill_available_area()
    {
        var boardSize = _calculator.CalculateBoardSize(
            new SizeF(516, 516),
            16f,
            2,
            2);

        Assert.Equal(516f, boardSize.Width, 3);
        Assert.Equal(516f, boardSize.Height, 3);
    }

    [Fact]
    public void CalculateMinimumScale_uses_current_rectangle_board()
    {
        var minimumScale = _calculator.CalculateMinimumScale(
            new SizeF(200, 100),
            new SizeF(516, 516),
            16f,
            [(0, 0)]);

        Assert.Equal(5.16f, minimumScale, 2);
    }

    [Fact]
    public void ClampOffset_keeps_selected_region_covered()
    {
        var clampedOffset = _calculator.ClampOffset(
            new SizeF(600, 600),
            1f,
            new SizeF(516, 516),
            16f,
            [(0, 0)],
            new PointF(24f, 36f));

        Assert.Equal(0f, clampedOffset.X, 3);
        Assert.Equal(0f, clampedOffset.Y, 3);
    }

    [Fact]
    public void BuildExportRegions_returns_regions_in_row_major_order()
    {
        var exportRegions = _calculator.BuildExportRegions(
            new SizeF(300, 300),
            new SizeF(516, 516),
            16f,
            [(1, 3), (0, 4), (1, 1)],
            1f,
            new PointF(0f, 0f));

        Assert.Collection(
            exportRegions,
            region =>
            {
                Assert.Equal(0, region.TileIndex);
                Assert.Equal(0, region.GridRow);
                Assert.Equal(4, region.GridColumn);
            },
            region =>
            {
                Assert.Equal(1, region.TileIndex);
                Assert.Equal(1, region.GridRow);
                Assert.Equal(1, region.GridColumn);
            },
            region =>
            {
                Assert.Equal(2, region.TileIndex);
                Assert.Equal(1, region.GridRow);
                Assert.Equal(3, region.GridColumn);
            });
    }

    /// <summary>
    /// 连续矩形选区的导出顺序也应保持从上到下、从左到右。
    /// </summary>
    [Fact]
    public void BuildExportRegions_returns_rectangle_regions_in_row_major_order()
    {
        var exportRegions = _calculator.BuildExportRegions(
            new SizeF(320, 320),
            new SizeF(516, 516),
            16f,
            [(0, 0), (0, 1), (1, 0), (1, 1), (2, 0), (2, 1)],
            1f,
            new PointF(0f, 0f));

        Assert.Collection(
            exportRegions,
            region => Assert.Equal((0, 0, 0), (region.TileIndex, region.GridRow, region.GridColumn)),
            region => Assert.Equal((1, 0, 1), (region.TileIndex, region.GridRow, region.GridColumn)),
            region => Assert.Equal((2, 1, 0), (region.TileIndex, region.GridRow, region.GridColumn)),
            region => Assert.Equal((3, 1, 1), (region.TileIndex, region.GridRow, region.GridColumn)),
            region => Assert.Equal((4, 2, 0), (region.TileIndex, region.GridRow, region.GridColumn)),
            region => Assert.Equal((5, 2, 1), (region.TileIndex, region.GridRow, region.GridColumn)));
    }

    [Fact]
    public void SnapScaleToMinimum_returns_exact_minimum_when_close_enough()
    {
        var snappedScale = _calculator.SnapScaleToMinimum(0.932f, 0.904f);

        Assert.Equal(0.904f, snappedScale, 3);
    }
}
