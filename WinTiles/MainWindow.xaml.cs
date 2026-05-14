using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTiles.Core.Models;
using WinTiles.Core.Services;

namespace WinTiles;

public partial class MainWindow : Window
{
    private readonly WinTilesApplicationContext _applicationContext;
    private readonly MainWindowViewModel _viewModel;
    private string? _selectedImagePath;

    public MainWindow(WinTilesApplicationContext applicationContext)
    {
        _applicationContext = applicationContext;
        _viewModel = new MainWindowViewModel();
        InitializeComponent();
        DataContext = _viewModel;
    }

    public async Task InitializeAsync(string? startupTileId)
    {
        RefreshEnvironmentState();
        ApplySelectedSize(TileRequestSize.Medium2x2);
        RefreshPinButtonState();

        if (!string.IsNullOrWhiteSpace(startupTileId))
        {
            await HandleActivationAsync(startupTileId).ConfigureAwait(true);
        }
    }

    public async Task HandleActivationAsync(string? tileId)
    {
        Activate();
        WindowState = WindowState.Normal;
        Topmost = true;
        Topmost = false;
        Focus();

        if (string.IsNullOrWhiteSpace(tileId))
        {
            return;
        }

        var tileRecord = await _applicationContext.TileRecordStore.LoadTileRecordAsync(tileId).ConfigureAwait(true);
        if (tileRecord is null)
        {
            SetStatus("已打开 WinTiles，但没有找到对应的磁贴记录。", Brushes.DarkGoldenrod);
            return;
        }

        _selectedImagePath = tileRecord.SourceImagePath;
        LoadPreviewImage(tileRecord.SourceImagePath);
        ApplySelectedSize(tileRecord.RequestedSize);

        var pinAttempt = await _applicationContext.TileRecordStore.LoadPinAttemptAsync(tileId).ConfigureAwait(true);
        if (pinAttempt is not null)
        {
            SetStatus(pinAttempt.Message, MapStatusBrush(pinAttempt.Status));
        }
    }

    private void SelectImageButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*",
            Title = "选择要固定的图片"
        };

        if (openFileDialog.ShowDialog(this) != true)
        {
            return;
        }

        _selectedImagePath = openFileDialog.FileName;
        LoadPreviewImage(openFileDialog.FileName);
        _viewModel.PreviewTitle = Path.GetFileName(openFileDialog.FileName);
        SetStatus($"已选择图片：{Path.GetFileName(openFileDialog.FileName)}", Brushes.DarkSlateBlue);
        RefreshPinButtonState();
    }

    private async void PinImageButton_Click(object sender, RoutedEventArgs e)
    {
        await PinCurrentImageAsync().ConfigureAwait(true);
    }

    private void SizeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton)
        {
            return;
        }

        // XAML 初始化阶段单选按钮会依次触发 Checked，这里只根据当前 sender 判断，避免访问尚未构造完成的控件字段。
        _viewModel.SelectedSize = radioButton.Name switch
        {
            nameof(Size1x1RadioButton) => TileRequestSize.Small1x1,
            nameof(Size4x2RadioButton) => TileRequestSize.Wide4x2,
            nameof(Size4x4RadioButton) => TileRequestSize.Large4x4,
            _ => TileRequestSize.Medium2x2
        };

        RefreshPinButtonState();
    }

    private async Task PinCurrentImageAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedImagePath))
        {
            MessageBox.Show(this, "请先选择一张图片。", "WinTiles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        RefreshEnvironmentState();
        if (!_viewModel.IsClassicStartAvailable || !_viewModel.AreToolsAvailable)
        {
            MessageBox.Show(this, _viewModel.AvailabilityMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _viewModel.IsBusy = true;
            RefreshPinButtonState();

            var tileId = Guid.NewGuid().ToString("N");
            var tileDirectory = _applicationContext.TileRecordStore.CreateTileDirectory(tileId);
            var sourceExtension = Path.GetExtension(_selectedImagePath);
            var copiedSourcePath = Path.Combine(tileDirectory, $"Source{sourceExtension}");
            File.Copy(_selectedImagePath, copiedSourcePath, overwrite: true);

            var assetsDirectory = Path.Combine(tileDirectory, "Assets");
            var generatedAssetSet = _applicationContext.ImageAssetGenerator.GenerateAssets(copiedSourcePath, assetsDirectory);

            var hostExePath = PrepareTileHost(tileDirectory, tileId);
            var manifestPath = Path.Combine(tileDirectory, "TileHost.VisualElementsManifest.xml");
            File.WriteAllText(
                manifestPath,
                _applicationContext.VisualElementsManifestBuilder.Build());

            var appUserModelId = TileIdentityBuilder.BuildAppUserModelId(tileId);
            var shortcutPath = CreateStartMenuShortcut(tileId, hostExePath, appUserModelId, generatedAssetSet.ShortcutIconPath, manifestPath);

            var tileRecord = new TileRecord
            {
                TileId = tileId,
                SourceImagePath = copiedSourcePath,
                RequestedSize = _viewModel.SelectedSize,
                ResolvedIdentity = TileIdentityBuilder.BuildResolvedIdentity(tileId, hostExePath),
                HostExePath = hostExePath,
                ShortcutPath = shortcutPath,
                AssetsVersion = generatedAssetSet.AssetsVersion
            };

            await _applicationContext.TileRecordStore.SaveTileRecordAsync(tileRecord).ConfigureAwait(true);

            var pinResult = await _applicationContext.PinHelperInvoker.PinImageAsync(
                _applicationContext.PinHelperPath,
                tileId,
                copiedSourcePath,
                _viewModel.SelectedSize,
                _applicationContext.MainExecutablePath,
                hostExePath).ConfigureAwait(true);

            var normalizedAttempt = NormalizePinAttempt(pinResult);
            await _applicationContext.TileRecordStore.SavePinAttemptAsync(tileId, normalizedAttempt).ConfigureAwait(true);

            SetStatus(normalizedAttempt.Message, MapStatusBrush(normalizedAttempt.Status));
            if (normalizedAttempt.Status is PinHelperResultStatus.Warning or PinHelperResultStatus.Failure)
            {
                MessageBox.Show(
                    this,
                    normalizedAttempt.Warning is { Length: > 0 }
                        ? $"{normalizedAttempt.Message}\n\n{normalizedAttempt.Warning}"
                        : normalizedAttempt.Message,
                    "WinTiles",
                    MessageBoxButton.OK,
                    normalizedAttempt.Status == PinHelperResultStatus.Failure
                        ? MessageBoxImage.Error
                        : MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            SetStatus("固定图片失败，请查看弹窗提示。", Brushes.Firebrick);
            MessageBox.Show(this, exception.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
            RefreshPinButtonState();
        }
    }

    private string PrepareTileHost(string tileDirectory, string tileId)
    {
        if (!File.Exists(_applicationContext.TileHostTemplatePath))
        {
            throw new FileNotFoundException("未找到 TileHost.exe，请先构建原生工具。", _applicationContext.TileHostTemplatePath);
        }

        var hostExePath = Path.Combine(tileDirectory, "TileHost.exe");
        File.Copy(_applicationContext.TileHostTemplatePath, hostExePath, overwrite: true);

        // 每个磁贴目录都会落一份独立 ini，保证点击磁贴时能准确回流到主程序。
        _applicationContext.TileHostConfigurationWriter.Write(tileDirectory, _applicationContext.MainExecutablePath, tileId);
        return hostExePath;
    }

    private string CreateStartMenuShortcut(string tileId, string hostExePath, string appUserModelId, string shortcutIconPath, string manifestPath)
    {
        var startMenuPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "WinTiles");
        var shortcutFileName = TileIdentityBuilder.BuildShortcutFileName(_selectedImagePath!, tileId);
        var shortcutPath = Path.Combine(startMenuPrograms, shortcutFileName);

        return _applicationContext.StartMenuShortcutService.CreateShortcut(
            shortcutPath,
            hostExePath,
            string.Empty,
            Path.GetDirectoryName(hostExePath)!,
            "WinTiles 图片磁贴",
            appUserModelId,
            shortcutIconPath,
            manifestPath);
    }

    private PinAttemptRecord NormalizePinAttempt(PinHelperResult pinResult)
    {
        var status = pinResult.Status;
        var message = pinResult.Message;
        var warning = pinResult.Warning;

        if (_viewModel.SelectedSize.UsesExperimentalPinPath() && pinResult.Status == PinHelperResultStatus.Success)
        {
            status = PinHelperResultStatus.Warning;
            message = "已固定，但系统可能未按请求尺寸显示";
            warning ??= $"{_viewModel.SelectedSize.ToDisplayText()} 仍走实验性固定路径，请手动确认开始菜单中的实际尺寸。";
        }

        return new PinAttemptRecord
        {
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            RequestedSize = _viewModel.SelectedSize,
            Status = status,
            Message = message,
            PinMethod = pinResult.PinMethod,
            Warning = warning,
            IdentityKind = pinResult.IdentityKind,
            IdentityValue = pinResult.IdentityValue,
            ContainsBefore = pinResult.ContainsBefore,
            ContainsAfterCommit = pinResult.ContainsAfterCommit,
            ContainsAfterReopen = pinResult.ContainsAfterReopen
        };
    }

    private void RefreshEnvironmentState()
    {
        var classicStartAvailability = _applicationContext.ClassicStartDetector.Detect();
        var toolsAvailable = File.Exists(_applicationContext.PinHelperPath) && File.Exists(_applicationContext.TileHostTemplatePath);

        _viewModel.IsClassicStartAvailable = classicStartAvailability.IsAvailable;
        _viewModel.AreToolsAvailable = toolsAvailable;

        if (!classicStartAvailability.IsAvailable)
        {
            _viewModel.AvailabilityMessage = classicStartAvailability.Message;
            _viewModel.AvailabilityBrush = Brushes.Firebrick;
        }
        else if (!toolsAvailable)
        {
            _viewModel.AvailabilityMessage = "未找到 Tools\\WinTiles.PinHelper.exe 或 Tools\\TileHost.exe，请先构建原生工具。";
            _viewModel.AvailabilityBrush = Brushes.DarkGoldenrod;
        }
        else
        {
            _viewModel.AvailabilityMessage = classicStartAvailability.Message;
            _viewModel.AvailabilityBrush = Brushes.SeaGreen;
        }
    }

    private void RefreshPinButtonState()
    {
        if (PinImageButton is null)
        {
            return;
        }

        PinImageButton.IsEnabled =
            !_viewModel.IsBusy &&
            !string.IsNullOrWhiteSpace(_selectedImagePath) &&
            _viewModel.IsClassicStartAvailable &&
            _viewModel.AreToolsAvailable;
    }

    private void ApplySelectedSize(TileRequestSize size)
    {
        _viewModel.SelectedSize = size;
        Size1x1RadioButton.IsChecked = size == TileRequestSize.Small1x1;
        Size2x2RadioButton.IsChecked = size == TileRequestSize.Medium2x2;
        Size4x2RadioButton.IsChecked = size == TileRequestSize.Wide4x2;
        Size4x4RadioButton.IsChecked = size == TileRequestSize.Large4x4;
    }

    private void LoadPreviewImage(string imagePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        _viewModel.HasPreviewImage = true;
        _viewModel.PreviewImage = bitmap;
        _viewModel.PreviewTitle = Path.GetFileName(imagePath);
        RefreshPinButtonState();
    }

    private void SetStatus(string message, Brush brush)
    {
        _viewModel.StatusText = message;
        _viewModel.StatusBrush = brush;
    }

    private static Brush MapStatusBrush(PinHelperResultStatus status) => status switch
    {
        PinHelperResultStatus.Success => Brushes.SeaGreen,
        PinHelperResultStatus.Warning => Brushes.DarkGoldenrod,
        _ => Brushes.Firebrick
    };
}
