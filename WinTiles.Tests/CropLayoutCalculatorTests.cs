using System.Drawing;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class CropLayoutCalculatorTests
{
    private readonly CropLayoutCalculator _calculator = new();

    [Fact]
    public void CalculateMinimumScale_uses_selected_bounds_instead_of_full_board()
    {
        var minimumScale = _calculator.CalculateMinimumScale(
            new SizeF(200, 100),
            new SizeF(516, 516),
            16f,
            [(0, 0)]);

        Assert.Equal(0.904f, minimumScale, 3);
    }

    [Fact]
    public void ClampOffset_keeps_selected_region_covered()
    {
        var clampedOffset = _calculator.ClampOffset(
            new SizeF(300, 300),
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

    [Fact]
    public void SnapScaleToMinimum_returns_exact_minimum_when_close_enough()
    {
        var snappedScale = _calculator.SnapScaleToMinimum(0.932f, 0.904f);

        Assert.Equal(0.904f, snappedScale, 3);
    }
}
