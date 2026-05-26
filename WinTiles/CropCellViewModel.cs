namespace WinTiles;

/// <summary>
/// 表示裁切板中单个格子的布局与选中状态。
/// </summary>
public sealed class CropCellViewModel : ViewModelBase
{
    private bool _isSelected;
    private bool _isVisible = true;
    private double _left;
    private double _top;
    private double _size;

    /// <summary>
    /// 初始化单个裁切格子视图模型。
    /// </summary>
    /// <param name="row">当前格子的零基行号。</param>
    /// <param name="column">当前格子的零基列号。</param>
    public CropCellViewModel(int row, int column)
    {
        Row = row;
        Column = column;
    }

    /// <summary>
    /// 当前格子的零基行号。
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// 当前格子的零基列号。
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// 指示当前格子是否位于活跃矩形选区内。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>
    /// 指示当前格子是否需要在预览层中显示。
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    /// <summary>
    /// 当前格子在画布中的左侧偏移。
    /// </summary>
    public double Left
    {
        get => _left;
        set => SetField(ref _left, value);
    }

    /// <summary>
    /// 当前格子在画布中的顶部偏移。
    /// </summary>
    public double Top
    {
        get => _top;
        set => SetField(ref _top, value);
    }

    /// <summary>
    /// 当前格子的边长。
    /// </summary>
    public double Size
    {
        get => _size;
        set => SetField(ref _size, value);
    }
}
