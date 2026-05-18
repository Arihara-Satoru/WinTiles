using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinTiles;

public sealed class MainWindowViewModel : ViewModelBase
{
    private MainPanelMode _panelMode = MainPanelMode.Crop;
    private string _cropTitle = "尚未选择图片";
    private string _cropSubtitle = "点击网格启用裁切区域，然后在右侧用滚轮缩放、拖拽图片位置。";
    private BitmapImage? _cropImage;
    private bool _hasCropImage;
    private string _statusText = "请选择一张图片，然后点击右侧网格启用裁切区域。";
    private string _statusHintText = "提示：拖拽图片可以调整位置，滚轮可以缩放，缩到刚好铺满时会自动吸附。";
    private Brush _statusBrush = Brushes.DarkSlateBlue;
    private string _availabilityMessage = "正在检查经典开始菜单状态…";
    private Brush _availabilityBrush = Brushes.DarkSlateBlue;
    private string _recordLocationText = "本地记录目录";
    private string _historyCountText = "0 条";
    private bool _isClassicStartAvailable;
    private bool _areToolsAvailable;
    private bool _isBusy;
    private bool _hasHistoryItems;
    private bool _canPinImage;
    private bool _canOpenHistory;
    private bool _canClearSelection;
    private bool _canClearAllPinnedTiles;
    private bool _canOpenRecordFolder = true;
    private bool _canCheckForUpdates = true;
    private bool _isCheckingForUpdates;
    private string _selectionSummaryText = "尚未启用裁切区域";
    private string _zoomText = "缩放 100%";
    private string _updateStatusText = "版本检查未开始。";
    private double _cropScale = 1d;
    private double _minimumCropScale = 1d;
    private double _cropOffsetX;
    private double _cropOffsetY;
    private double _cropBoardSize = 640d;

    public ObservableCollection<CropCellViewModel> CropCells { get; } = new();

    public ObservableCollection<TileBatchHistoryItemViewModel> BatchHistoryItems { get; } = new();

    public MainPanelMode PanelMode
    {
        get => _panelMode;
        set
        {
            if (SetField(ref _panelMode, value))
            {
                OnPropertyChanged(nameof(IsCropMode));
                OnPropertyChanged(nameof(IsHistoryMode));
            }
        }
    }

    public bool IsCropMode => PanelMode == MainPanelMode.Crop;

    public bool IsHistoryMode => PanelMode == MainPanelMode.History;

    public string CropTitle
    {
        get => _cropTitle;
        set => SetField(ref _cropTitle, value);
    }

    public string CropSubtitle
    {
        get => _cropSubtitle;
        set => SetField(ref _cropSubtitle, value);
    }

    public BitmapImage? CropImage
    {
        get => _cropImage;
        set => SetField(ref _cropImage, value);
    }

    public bool HasCropImage
    {
        get => _hasCropImage;
        set => SetField(ref _hasCropImage, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string StatusHintText
    {
        get => _statusHintText;
        set => SetField(ref _statusHintText, value);
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

    public string HistoryCountText
    {
        get => _historyCountText;
        set => SetField(ref _historyCountText, value);
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

    public bool CanPinImage
    {
        get => _canPinImage;
        set => SetField(ref _canPinImage, value);
    }

    public bool CanOpenHistory
    {
        get => _canOpenHistory;
        set => SetField(ref _canOpenHistory, value);
    }

    public bool CanClearSelection
    {
        get => _canClearSelection;
        set => SetField(ref _canClearSelection, value);
    }

    public bool CanClearAllPinnedTiles
    {
        get => _canClearAllPinnedTiles;
        set => SetField(ref _canClearAllPinnedTiles, value);
    }

    public bool CanOpenRecordFolder
    {
        get => _canOpenRecordFolder;
        set => SetField(ref _canOpenRecordFolder, value);
    }

    public bool CanCheckForUpdates
    {
        get => _canCheckForUpdates;
        set => SetField(ref _canCheckForUpdates, value);
    }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        set => SetField(ref _isCheckingForUpdates, value);
    }

    public string SelectionSummaryText
    {
        get => _selectionSummaryText;
        set => SetField(ref _selectionSummaryText, value);
    }

    public string ZoomText
    {
        get => _zoomText;
        set => SetField(ref _zoomText, value);
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set => SetField(ref _updateStatusText, value);
    }

    public double CropScale
    {
        get => _cropScale;
        set => SetField(ref _cropScale, value);
    }

    public double MinimumCropScale
    {
        get => _minimumCropScale;
        set => SetField(ref _minimumCropScale, value);
    }

    public double CropOffsetX
    {
        get => _cropOffsetX;
        set => SetField(ref _cropOffsetX, value);
    }

    public double CropOffsetY
    {
        get => _cropOffsetY;
        set => SetField(ref _cropOffsetY, value);
    }

    public double CropBoardSize
    {
        get => _cropBoardSize;
        set => SetField(ref _cropBoardSize, value);
    }
}
