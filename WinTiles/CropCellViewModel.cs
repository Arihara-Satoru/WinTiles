namespace WinTiles;

public sealed class CropCellViewModel : ViewModelBase
{
    private bool _isSelected;
    private bool _isAvailable = true;
    private double _left;
    private double _top;
    private double _size;

    public CropCellViewModel(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public int Row { get; }

    public int Column { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetField(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(ShowPlus));
            }
        }
    }

    /// <summary>
    /// 指示当前裁切格子是否处于可用状态。
    /// </summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetField(ref _isAvailable, value);
    }

    public double Left
    {
        get => _left;
        set => SetField(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => SetField(ref _top, value);
    }

    public double Size
    {
        get => _size;
        set => SetField(ref _size, value);
    }

    public bool ShowPlus => !IsSelected;
}
