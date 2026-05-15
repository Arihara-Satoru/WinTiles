using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTiles.Core.Models;

namespace WinTiles;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _previewTitle = "尚未选择图片";
    private BitmapImage? _previewImage;
    private bool _hasPreviewImage;
    private string _statusText = "请选择一张图片，然后选择 2x2 或 4x2 并固定。";
    private Brush _statusBrush = Brushes.DarkSlateBlue;
    private string _availabilityMessage = "正在检查经典开始菜单状态…";
    private Brush _availabilityBrush = Brushes.DarkSlateBlue;
    private TileRequestSize _selectedSize = TileRequestSize.Medium2x2;
    private bool _isClassicStartAvailable;
    private bool _areToolsAvailable;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PreviewTitle
    {
        get => _previewTitle;
        set => SetField(ref _previewTitle, value);
    }

    public BitmapImage? PreviewImage
    {
        get => _previewImage;
        set => SetField(ref _previewImage, value);
    }

    public bool HasPreviewImage
    {
        get => _hasPreviewImage;
        set => SetField(ref _hasPreviewImage, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        set => SetField(ref _statusBrush, value);
    }

    public string AvailabilityMessage
    {
        get => _availabilityMessage;
        set => SetField(ref _availabilityMessage, value);
    }

    public Brush AvailabilityBrush
    {
        get => _availabilityBrush;
        set => SetField(ref _availabilityBrush, value);
    }

    public TileRequestSize SelectedSize
    {
        get => _selectedSize;
        set => SetField(ref _selectedSize, value);
    }

    public bool IsClassicStartAvailable
    {
        get => _isClassicStartAvailable;
        set => SetField(ref _isClassicStartAvailable, value);
    }

    public bool AreToolsAvailable
    {
        get => _areToolsAvailable;
        set => SetField(ref _areToolsAvailable, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
