namespace WinTiles.Core.Services;

/// <summary>
/// 统一维护裁切区的矩形行列选择规则，避免界面层散落重复判断。
/// </summary>
public sealed class RectangularCropSelectionController
{
    /// <summary>
    /// 默认启用的行列数量。
    /// </summary>
    public const int DefaultAxisCount = 2;

    /// <summary>
    /// 单个轴允许的最小数量。
    /// </summary>
    public const int MinimumAxisCount = 1;

    /// <summary>
    /// 单个轴允许的最大数量，沿用裁切板的 5x5 上限。
    /// </summary>
    public const int MaximumAxisCount = CropLayoutCalculator.GridDimension;

    /// <summary>
    /// 把传入的行数或列数夹紧到合法范围内。
    /// </summary>
    /// <param name="axisCount">待规范化的行数或列数。</param>
    /// <returns>落在 1 到 5 之间的合法数量。</returns>
    public int NormalizeAxisCount(int axisCount)
    {
        return Math.Clamp(axisCount, MinimumAxisCount, MaximumAxisCount);
    }

    /// <summary>
    /// 判断指定轴按钮在当前状态下是否允许点击。
    /// </summary>
    /// <param name="currentAxisCount">当前轴已启用的数量。</param>
    /// <param name="axisIndex">当前按钮对应的零基索引。</param>
    /// <returns>若允许扩展或收缩则返回 true；否则返回 false。</returns>
    public bool IsAxisButtonEnabled(int currentAxisCount, int axisIndex)
    {
        var normalizedCurrentAxisCount = NormalizeAxisCount(currentAxisCount);
        var normalizedAxisIndex = Math.Clamp(axisIndex, 0, MaximumAxisCount - 1);
        return normalizedCurrentAxisCount > MinimumAxisCount || normalizedAxisIndex > 0;
    }

    /// <summary>
    /// 根据被点击的行按钮或列按钮，计算点击后的新数量。
    /// </summary>
    /// <param name="currentAxisCount">当前轴已启用的数量。</param>
    /// <param name="axisIndex">被点击按钮对应的零基索引。</param>
    /// <returns>点击后的新行数或列数。</returns>
    public int ResolveAxisCountFromClick(int currentAxisCount, int axisIndex)
    {
        var normalizedCurrentAxisCount = NormalizeAxisCount(currentAxisCount);
        var normalizedAxisIndex = Math.Clamp(axisIndex, 0, MaximumAxisCount - 1);
        var requestedAxisCount = normalizedAxisIndex < normalizedCurrentAxisCount
            ? normalizedAxisIndex
            : normalizedAxisIndex + 1;

        return NormalizeAxisCount(requestedAxisCount);
    }

    /// <summary>
    /// 根据当前矩形行列数，生成左上角连续矩形范围内的所有活跃格子。
    /// </summary>
    /// <param name="rowCount">当前启用的行数。</param>
    /// <param name="columnCount">当前启用的列数。</param>
    /// <returns>按从上到下、从左到右顺序排列的活跃格子集合。</returns>
    public IReadOnlyList<(int Row, int Column)> BuildActiveCells(int rowCount, int columnCount)
    {
        var normalizedRowCount = NormalizeAxisCount(rowCount);
        var normalizedColumnCount = NormalizeAxisCount(columnCount);
        var activeCells = new List<(int Row, int Column)>(normalizedRowCount * normalizedColumnCount);

        for (var row = 0; row < normalizedRowCount; row++)
        {
            for (var column = 0; column < normalizedColumnCount; column++)
            {
                activeCells.Add((row, column));
            }
        }

        return activeCells;
    }
}
