using WinTiles.Core.Services;

namespace WinTiles.Tests;

/// <summary>
/// 验证矩形裁切选择控制器的行列扩缩规则。
/// </summary>
public sealed class RectangularCropSelectionControllerTests
{
    private readonly RectangularCropSelectionController _controller = new();

    /// <summary>
    /// 默认 2x2 选区应当生成左上角连续的 4 个活跃格子。
    /// </summary>
    [Fact]
    public void BuildActiveCells_returns_default_two_by_two_rectangle()
    {
        var activeCells = _controller.BuildActiveCells(
            RectangularCropSelectionController.DefaultAxisCount,
            RectangularCropSelectionController.DefaultAxisCount);

        Assert.Collection(
            activeCells,
            cell => Assert.Equal((0, 0), cell),
            cell => Assert.Equal((0, 1), cell),
            cell => Assert.Equal((1, 0), cell),
            cell => Assert.Equal((1, 1), cell));
    }

    /// <summary>
    /// 点击未激活的第 4 行时，应直接扩展到 4 行。
    /// </summary>
    [Fact]
    public void ResolveAxisCountFromClick_expands_to_clicked_inactive_axis()
    {
        var nextRowCount = _controller.ResolveAxisCountFromClick(2, 3);

        Assert.Equal(4, nextRowCount);
    }

    /// <summary>
    /// 点击已激活的第 3 列时，应回退到前一列，也就是 2 列。
    /// </summary>
    [Fact]
    public void ResolveAxisCountFromClick_reduces_to_previous_active_axis()
    {
        var nextColumnCount = _controller.ResolveAxisCountFromClick(3, 2);

        Assert.Equal(2, nextColumnCount);
    }

    /// <summary>
    /// 行列数量应始终被限制在 1 到 5 之间。
    /// </summary>
    [Fact]
    public void NormalizeAxisCount_clamps_to_supported_range()
    {
        Assert.Equal(RectangularCropSelectionController.MinimumAxisCount, _controller.NormalizeAxisCount(0));
        Assert.Equal(RectangularCropSelectionController.MaximumAxisCount, _controller.NormalizeAxisCount(8));
    }

    /// <summary>
    /// 当只有 1 行或 1 列时，第一个减号按钮应当禁用，其他按钮仍可继续扩展。
    /// </summary>
    [Fact]
    public void IsAxisButtonEnabled_disables_only_first_button_at_minimum()
    {
        Assert.False(_controller.IsAxisButtonEnabled(1, 0));
        Assert.True(_controller.IsAxisButtonEnabled(1, 1));
        Assert.True(_controller.IsAxisButtonEnabled(5, 4));
    }
}
