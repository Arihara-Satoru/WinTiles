namespace WinTiles;

/// <summary>
/// 表示裁切区顶部列控制或左侧行控制的单个按钮状态。
/// </summary>
public sealed class CropAxisControlViewModel : ViewModelBase
{
    private bool _isActive;
    private bool _isEnabled = true;
    private bool _isVisible = true;
    private double _left;
    private double _top;

    /// <summary>
    /// 初始化单个轴按钮视图模型。
    /// </summary>
    /// <param name="index">当前按钮对应的零基索引。</param>
    public CropAxisControlViewModel(int index)
    {
        Index = index;
    }

    /// <summary>
    /// 当前按钮对应的零基索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 指示当前按钮对应的行或列是否已经落在活跃矩形内。
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetField(ref _isActive, value))
            {
                OnPropertyChanged(nameof(Symbol));
            }
        }
    }

    /// <summary>
    /// 指示当前按钮是否允许点击。
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    /// <summary>
    /// 指示当前按钮是否需要显示。
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    /// <summary>
    /// 当前按钮在画布中的左侧偏移。
    /// </summary>
    public double Left
    {
        get => _left;
        set => SetField(ref _left, value);
    }

    /// <summary>
    /// 当前按钮在画布中的顶部偏移。
    /// </summary>
    public double Top
    {
        get => _top;
        set => SetField(ref _top, value);
    }

    /// <summary>
    /// 根据当前活跃状态返回按钮展示的加减符号。
    /// </summary>
    public string Symbol => IsActive ? "-" : "+";
}
