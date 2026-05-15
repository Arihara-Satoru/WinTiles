using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTiles.Core.Models;

namespace WinTiles;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _previewTitle = "尚未选择图片或历史固定";
    private BitmapImage? _previewImage;
    private bool _hasPreviewImage;
    private string _statusText = "请选择一张图片，或从固定历史中点开一条记录。";
    private Brush _statusBrush = Brushes.DarkSlateBlue;
    private string _availabilityMessage = "正在检查经典开始菜单状态…";
    private Brush _availabilityBrush = Brushes.DarkSlateBlue;
    private string _recordLocationText = "本地记录目录";
    private string _selectedHistoryBadgeText = "历史预览";
    private string _historyCountText = "0 条";
    private TileRequestSize _selectedSize = TileRequestSize.Medium2x2;
    private bool _isClassicStartAvailable;
    private bool _areToolsAvailable;
    private bool _isBusy;
    private bool _hasHistoryItems;
    private bool _canDeleteSelectedHistory;

    public ObservableCollection<TileHistoryItemViewModel> HistoryItems { get; } = new();

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

    public string RecordLocationText
    {
        get => _recordLocationText;
        set => SetField(ref _recordLocationText, value);
    }

    public string SelectedHistoryBadgeText
    {
        get => _selectedHistoryBadgeText;
        set => SetField(ref _selectedHistoryBadgeText, value);
    }

    public string HistoryCountText
    {
        get => _historyCountText;
        set => SetField(ref _historyCountText, value);
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

    public bool HasHistoryItems
    {
        get => _hasHistoryItems;
        set => SetField(ref _hasHistoryItems, value);
    }

    public bool CanDeleteSelectedHistory
    {
        get => _canDeleteSelectedHistory;
        set => SetField(ref _canDeleteSelectedHistory, value);
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
