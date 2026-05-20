using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinTiles.Core.Models;
using WinTiles.Core.Services;
using DrawingPointF = System.Drawing.PointF;
using DrawingSizeF = System.Drawing.SizeF;

namespace WinTiles;

public partial class MainWindow : Window
{
    // 继续收紧裁切格子之间的留白，让右侧网格更贴合预览拼图的观感。
    private const float CropCellGap = 6f;
    private const float ZoomStep = 1.10f;
    private const double DragThreshold = 4d;

    private readonly WinTilesApplicationContext _applicationContext;
    private readonly MainWindowViewModel _viewModel;
    private readonly CropLayoutCalculator _cropLayoutCalculator;

    private SilentUpdatePreparationResult? _preparedUpdate;
    private string? _selectedImagePath;
    private DrawingSizeF _selectedImagePixelSize;
    private Point? _dragStartPoint;
    private DrawingPointF _dragStartOffset;
    private bool _isDraggingCropImage;

    public MainWindow(WinTilesApplicationContext applicationContext)
    {
        _applicationContext = applicationContext;
        _cropLayoutCalculator = applicationContext.CropLayoutCalculator;
        _viewModel = new MainWindowViewModel();

        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RecordLocationText = _applicationContext.TileRecordStore.RootDirectory;

        InitializeCropCells();
        UpdateClickActionState();
    }

    public async Task InitializeAsync(string? startupTileId)
    {
        RefreshEnvironmentState();
        ApplyPanelMode(MainPanelMode.Crop);
        UpdateSelectionSummary();
        RefreshActionButtonsState();

        await RefreshHistoryAsync(startupTileId).ConfigureAwait(true);
        UpdateCropBoardLayout();

        if (!string.IsNullOrWhiteSpace(startupTileId))
        {
            await HandleActivationAsync(startupTileId).ConfigureAwait(true);
        }

        // 更新检查放到主界面完成初始化之后异步触发，避免网络波动拖慢首屏可用时间。
        _ = CheckForUpdatesAsync(
            showUpToDateMessage: false,
            showFailureMessage: false,
            showUpdatePrompt: true);
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

        if (await TryExecuteTileClickActionAsync(tileRecord).ConfigureAwait(true))
        {
            return;
        }

        ApplyPanelMode(MainPanelMode.History);
        await RefreshHistoryAsync(tileId).ConfigureAwait(true);
        SetStatus($"已定位到磁贴记录：{ResolveTilePositionText(tileRecord)}", Brushes.DarkSlateBlue);
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
        ApplyPanelMode(MainPanelMode.Crop);
        LoadSelectedImage(openFileDialog.FileName);
    }

    private async void ShowHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowHistoryAsync().ConfigureAwait(true);
    }

    private async void PinImageButton_Click(object sender, RoutedEventArgs e)
    {
        await PinCurrentImageAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// 切换为“不设置点击动作”选项，并刷新界面状态。
    /// </summary>
    private void NoClickActionRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedClickActionType = TileClickActionType.None;
        UpdateClickActionState();
    }

    /// <summary>
    /// 切换为“打开网页”动作，并刷新界面状态。
    /// </summary>
    private void OpenUrlActionRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedClickActionType = TileClickActionType.OpenUrl;
        UpdateClickActionState();
    }

    /// <summary>
    /// 切换为“打开应用”动作，并刷新界面状态。
    /// </summary>
    private void OpenApplicationActionRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedClickActionType = TileClickActionType.OpenApplication;
        UpdateClickActionState();
    }

    /// <summary>
    /// 当点击动作表单发生变化时，同步刷新校验结果和按钮状态。
    /// </summary>
    private void ClickActionInputChanged(object sender, TextChangedEventArgs e)
    {
        UpdateClickActionState();
    }

    /// <summary>
    /// 选择要由磁贴点击后启动的应用文件。
    /// </summary>
    private void BrowseApplicationPathButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "应用或快捷方式|*.exe;*.lnk|可执行文件|*.exe|快捷方式|*.lnk|所有文件|*.*",
            Title = "选择磁贴点击后要启动的应用"
        };

        if (openFileDialog.ShowDialog(this) != true)
        {
            return;
        }

        _viewModel.ClickActionApplicationPath = openFileDialog.FileName;
        if (string.IsNullOrWhiteSpace(_viewModel.ClickActionWorkingDirectory))
        {
            _viewModel.ClickActionWorkingDirectory = Path.GetDirectoryName(openFileDialog.FileName) ?? string.Empty;
        }

        UpdateClickActionState();
    }

    /// <summary>
    /// 一键把工作目录填充为应用所在目录，减少用户重复输入。
    /// </summary>
    private void UseApplicationDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.ClickActionApplicationPath))
        {
            return;
        }

        _viewModel.ClickActionWorkingDirectory = Path.GetDirectoryName(_viewModel.ClickActionApplicationPath) ?? string.Empty;
        UpdateClickActionState();
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(
            showUpToDateMessage: true,
            showFailureMessage: true,
            showUpdatePrompt: true).ConfigureAwait(true);
    }

    private void BackToCropButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyPanelMode(MainPanelMode.Crop);
    }

    private void ClearCropSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cell in _viewModel.CropCells)
        {
            cell.IsSelected = false;
        }

        UpdateSelectionSummary();
        ResetCropTransform();
        SetStatus("已清空所有裁切区域。", Brushes.DarkSlateBlue);
        RefreshActionButtonsState();
    }

    private async void ClearAllPinnedTilesButton_Click(object sender, RoutedEventArgs e)
    {
        await ClearAllPinnedTilesAsync().ConfigureAwait(true);
    }

    private async void DeleteTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TileHistoryItemViewModel historyItem })
        {
            return;
        }

        await DeleteSingleTileAsync(historyItem).ConfigureAwait(true);
    }

    private async void DeleteBatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TileBatchHistoryItemViewModel historyItem })
        {
            return;
        }

        await DeleteBatchAsync(historyItem).ConfigureAwait(true);
    }

    private void OpenRecordFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 让用户直接跳到本地记录目录，方便检查固定后的磁贴记录和批次目录。
            Process.Start(new ProcessStartInfo
            {
                FileName = _applicationContext.TileRecordStore.RootDirectory,
                UseShellExecute = true
            });
            SetStatus("已打开本地记录目录。", Brushes.DarkSlateBlue);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CropBoardHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCropBoardLayout();
    }

    private void CropBoardBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_viewModel.HasCropImage || _selectedImagePixelSize.Width <= 0 || _selectedImagePixelSize.Height <= 0)
        {
            return;
        }

        e.Handled = true;

        var activeCells = GetActiveCells();
        var boardSize = GetCropBoardSize();
        var minimumScale = _cropLayoutCalculator.CalculateMinimumScale(
            _selectedImagePixelSize,
            boardSize,
            CropCellGap,
            activeCells);
        var currentScale = (float)_viewModel.CropScale;
        var wheelFactor = e.Delta > 0 ? ZoomStep : 1f / ZoomStep;
        var desiredScale = currentScale * wheelFactor;
        desiredScale = Math.Max(desiredScale, minimumScale);
        desiredScale = _cropLayoutCalculator.SnapScaleToMinimum(desiredScale, minimumScale);

        var pointer = e.GetPosition(CropBoardBorder);
        var currentOffset = new DrawingPointF((float)_viewModel.CropOffsetX, (float)_viewModel.CropOffsetY);
        var nextOffset = new DrawingPointF(
            (float)(pointer.X - (pointer.X - currentOffset.X) * (desiredScale / currentScale)),
            (float)(pointer.Y - (pointer.Y - currentOffset.Y) * (desiredScale / currentScale)));

        nextOffset = _cropLayoutCalculator.ClampOffset(
            _selectedImagePixelSize,
            desiredScale,
            boardSize,
            CropCellGap,
            activeCells,
            nextOffset);

        ApplyCropState(desiredScale, nextOffset, minimumScale);
    }

    private void CropBoardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (CropBoardBorder is null)
        {
            return;
        }

        _dragStartPoint = e.GetPosition(CropBoardBorder);
        _dragStartOffset = new DrawingPointF((float)_viewModel.CropOffsetX, (float)_viewModel.CropOffsetY);
        _isDraggingCropImage = false;
        CropBoardBorder.CaptureMouse();
        e.Handled = true;
    }

    private void CropBoardBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartPoint is null || CropBoardBorder is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!_viewModel.HasCropImage || _selectedImagePixelSize.Width <= 0 || _selectedImagePixelSize.Height <= 0)
        {
            return;
        }

        var currentPoint = e.GetPosition(CropBoardBorder);
        var delta = currentPoint - _dragStartPoint.Value;
        if (!_isDraggingCropImage && delta.Length < DragThreshold)
        {
            return;
        }

        _isDraggingCropImage = true;

        var boardSize = GetCropBoardSize();
        var activeCells = GetActiveCells();
        var desiredOffset = new DrawingPointF(
            _dragStartOffset.X + (float)delta.X,
            _dragStartOffset.Y + (float)delta.Y);
        var clampedOffset = _cropLayoutCalculator.ClampOffset(
            _selectedImagePixelSize,
            (float)_viewModel.CropScale,
            boardSize,
            CropCellGap,
            activeCells,
            desiredOffset);

        ApplyCropState((float)_viewModel.CropScale, clampedOffset, (float)_viewModel.MinimumCropScale);
    }

    private void CropBoardBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStartPoint is null || CropBoardBorder is null)
        {
            return;
        }

        var releasePoint = e.GetPosition(CropBoardBorder);
        CropBoardBorder.ReleaseMouseCapture();

        if (!_isDraggingCropImage)
        {
            ToggleCellAtPoint(releasePoint);
        }

        _dragStartPoint = null;
        _isDraggingCropImage = false;
        e.Handled = true;
    }

    private void CropBoardBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        if (CropBoardBorder is not null && CropBoardBorder.IsMouseCaptured && e.LeftButton == MouseButtonState.Released)
        {
            CropBoardBorder.ReleaseMouseCapture();
            _dragStartPoint = null;
            _isDraggingCropImage = false;
        }
    }

    private async Task ShowHistoryAsync(string? preferredTileId = null)
    {
        ApplyPanelMode(MainPanelMode.History);
        await RefreshHistoryAsync(preferredTileId).ConfigureAwait(true);
    }

    private async Task PinCurrentImageAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedImagePath))
        {
            MessageBox.Show(this, "请先选择一张图片。", "WinTiles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var activeCells = GetActiveCells();
        if (activeCells.Count == 0)
        {
            MessageBox.Show(this, "请先点击右侧网格，至少启用一个裁切区域。", "WinTiles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var clickAction = BuildCurrentClickAction();
        var clickActionValidation = TileClickActionService.Validate(clickAction);
        if (!clickActionValidation.IsValid)
        {
            MessageBox.Show(this, clickActionValidation.ValidationMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            RefreshActionButtonsState();

            var batchId = Guid.NewGuid().ToString("N");
            var batchDirectory = _applicationContext.TileRecordStore.CreateBatchDirectory(batchId);
            var copiedSourcePath = CopySourceImageToBatchDirectory(batchDirectory, _selectedImagePath);
            var exportRegions = _cropLayoutCalculator.BuildExportRegions(
                _selectedImagePixelSize,
                GetCropBoardSize(),
                CropCellGap,
                activeCells,
                (float)_viewModel.CropScale,
                new DrawingPointF((float)_viewModel.CropOffsetX, (float)_viewModel.CropOffsetY));

            var batchTileIds = new List<string>(exportRegions.Count);
            var successCount = 0;
            var warningCount = 0;
            var failureCount = 0;
            var detailMessages = new List<string>();

            foreach (var exportRegion in exportRegions)
            {
                var tileId = Guid.NewGuid().ToString("N");
                var tileDirectory = _applicationContext.TileRecordStore.CreateTileDirectory(tileId);
                var assetsDirectory = Path.Combine(tileDirectory, "Assets");
                var hostExePath = Path.Combine(tileDirectory, "TileHost.exe");
                var manifestPath = Path.Combine(tileDirectory, "TileHost.VisualElementsManifest.xml");
                var appUserModelId = TileIdentityBuilder.BuildAppUserModelId(tileId);
                var shortcutDisplayTitle = TileIdentityBuilder.BuildShortcutDisplayTitle(
                    copiedSourcePath,
                    exportRegion.GridRow,
                    exportRegion.GridColumn);
                var predictedShortcutPath = BuildStartMenuShortcutPath(
                    copiedSourcePath,
                    tileId,
                    exportRegion.GridRow,
                    exportRegion.GridColumn);

                GeneratedAssetSet? generatedAssetSet = null;
                TileRecord? tileRecord = null;
                PinAttemptRecord attemptRecord;

                try
                {
                    generatedAssetSet = _applicationContext.ImageAssetGenerator.GenerateAssets(
                        copiedSourcePath,
                        assetsDirectory,
                        exportRegion.SourceCropBounds);

                    var preparedHostExePath = PrepareTileHost(tileDirectory, tileId);
                    File.WriteAllText(
                        manifestPath,
                        _applicationContext.VisualElementsManifestBuilder.Build());

                    var shortcutPath = CreateStartMenuShortcut(
                        copiedSourcePath,
                        tileId,
                        preparedHostExePath,
                        appUserModelId,
                        generatedAssetSet.ShortcutIconPath,
                        manifestPath,
                        shortcutDisplayTitle,
                        exportRegion.GridRow,
                        exportRegion.GridColumn);

                    tileRecord = new TileRecord
                    {
                        TileId = tileId,
                        SourceImagePath = copiedSourcePath,
                        BatchId = batchId,
                        TileIndex = exportRegion.TileIndex,
                        GridRow = exportRegion.GridRow,
                        GridColumn = exportRegion.GridColumn,
                        PreviewImagePath = generatedAssetSet.Square310x310LogoPath,
                        RequestedSize = TileRequestSize.Medium2x2,
                        HostExePath = preparedHostExePath,
                        ShortcutPath = shortcutPath,
                        AssetsVersion = generatedAssetSet.AssetsVersion,
                        ClickAction = clickAction
                    };

                    await _applicationContext.TileRecordStore.SaveTileRecordAsync(tileRecord).ConfigureAwait(true);

                    // 先保留系统入口作为兼容探测，真正的自动固定仍统一交给 helper。
                    var shellPinResult = await _applicationContext.StartMenuPinVerbInvoker.TryPinAsync(
                        appUserModelId,
                        shortcutPath).ConfigureAwait(true);

                    var pinResult = await _applicationContext.PinHelperInvoker.PinImageAsync(
                        _applicationContext.PinHelperPath,
                        tileId,
                        TileRequestSize.Medium2x2,
                        preparedHostExePath).ConfigureAwait(true);

                    attemptRecord = NormalizePinAttempt(shellPinResult, pinResult, appUserModelId);
                }
                catch (Exception exception)
                {
                    tileRecord ??= new TileRecord
                    {
                        TileId = tileId,
                        SourceImagePath = copiedSourcePath,
                        BatchId = batchId,
                        TileIndex = exportRegion.TileIndex,
                        GridRow = exportRegion.GridRow,
                        GridColumn = exportRegion.GridColumn,
                        PreviewImagePath = generatedAssetSet?.Square310x310LogoPath ?? Path.Combine(assetsDirectory, "Square310x310Logo.png"),
                        RequestedSize = TileRequestSize.Medium2x2,
                        HostExePath = hostExePath,
                        ShortcutPath = predictedShortcutPath,
                        AssetsVersion = generatedAssetSet?.AssetsVersion ?? GeneratedAssetSet.CurrentAssetsVersion,
                        ClickAction = clickAction
                    };

                    await _applicationContext.TileRecordStore.SaveTileRecordAsync(tileRecord).ConfigureAwait(true);
                    attemptRecord = CreateFailureAttemptRecord(exception);
                }

                await _applicationContext.TileRecordStore.SavePinAttemptAsync(tileId, attemptRecord).ConfigureAwait(true);
                batchTileIds.Add(tileId);

                switch (attemptRecord.Status)
                {
                    case PinHelperResultStatus.Success:
                        successCount++;
                        break;
                    case PinHelperResultStatus.Warning:
                        successCount++;
                        warningCount++;
                        detailMessages.Add($"{ResolveTilePositionText(tileRecord)}：{attemptRecord.Message}");
                        if (!string.IsNullOrWhiteSpace(attemptRecord.Warning))
                        {
                            detailMessages.Add(attemptRecord.Warning);
                        }
                        break;
                    default:
                        failureCount++;
                        detailMessages.Add($"{ResolveTilePositionText(tileRecord)}：{attemptRecord.Message}");
                        if (!string.IsNullOrWhiteSpace(attemptRecord.Warning))
                        {
                            detailMessages.Add(attemptRecord.Warning);
                        }
                        break;
                }
            }

            var batchRecord = new TileBatchRecord
            {
                BatchId = batchId,
                Title = TileIdentityBuilder.BuildBatchDisplayTitle(copiedSourcePath),
                SourceImagePath = copiedSourcePath,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                TileIds = batchTileIds,
                DefaultClickAction = clickAction
            };

            await _applicationContext.TileRecordStore.SaveTileBatchRecordAsync(batchRecord).ConfigureAwait(true);
            await ReconcileBatchRecordsAsync().ConfigureAwait(true);
            await RefreshHistoryAsync().ConfigureAwait(true);

            var statusBrush = failureCount > 0
                ? Brushes.DarkGoldenrod
                : warningCount > 0
                    ? Brushes.DarkGoldenrod
                    : Brushes.SeaGreen;
            var statusMessage = failureCount > 0
                ? $"本次固定完成：成功 {successCount}，失败 {failureCount}。"
                : warningCount > 0
                    ? $"本次固定完成：成功 {successCount}，其中 {warningCount} 块需要手动确认。"
                    : $"已按顺序固定 {successCount} 个图片磁贴。";

            if (detailMessages.Count > 0)
            {
                MessageBox.Show(
                    this,
                    $"{statusMessage}\n\n{string.Join(Environment.NewLine, detailMessages.Distinct(StringComparer.Ordinal))}",
                    "WinTiles",
                    MessageBoxButton.OK,
                    failureCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }

            ClearSelectedImageAfterBatchPin();
            SetStatus($"{statusMessage} 已自动清空裁剪区图片。", statusBrush);
        }
        catch (Exception exception)
        {
            SetStatus("固定图片失败，请查看弹窗提示。", Brushes.Firebrick);
            MessageBox.Show(this, exception.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
            RefreshActionButtonsState();
        }
    }

    private async Task ClearAllPinnedTilesAsync()
    {
        RefreshEnvironmentState();
        if (!_viewModel.IsClassicStartAvailable || !_viewModel.AreToolsAvailable)
        {
            MessageBox.Show(this, _viewModel.AvailabilityMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _viewModel.IsBusy = true;
            RefreshActionButtonsState();

            var tileRecords = await _applicationContext.TileRecordStore.LoadAllTileRecordsAsync().ConfigureAwait(true);
            if (tileRecords.Count == 0)
            {
                SetStatus("当前没有通过 WinTiles 固定的磁贴。", Brushes.DarkGoldenrod);
                await RefreshHistoryAsync().ConfigureAwait(true);
                return;
            }

            var deletedCount = 0;
            var warningMessages = new List<string>();

            foreach (var tileRecord in tileRecords)
            {
                var deleteResult = await TryDeletePinnedTileAsync(tileRecord).ConfigureAwait(true);
                if (deleteResult.Deleted)
                {
                    deletedCount++;
                }

                if (!string.IsNullOrWhiteSpace(deleteResult.Message))
                {
                    warningMessages.Add(deleteResult.Message);
                }
            }

            await ReconcileBatchRecordsAsync().ConfigureAwait(true);
            await RefreshHistoryAsync().ConfigureAwait(true);

            var statusMessage = deletedCount == 0
                ? "没有成功清除任何磁贴。"
                : deletedCount == 1
                    ? "已清除 1 个通过 WinTiles 固定的磁贴。"
                    : $"已清除 {deletedCount} 个通过 WinTiles 固定的磁贴。";

            SetStatus(statusMessage, deletedCount == 0 ? Brushes.Firebrick : Brushes.SeaGreen);

            if (warningMessages.Count > 0)
            {
                MessageBox.Show(
                    this,
                    $"{statusMessage}\n\n{string.Join(Environment.NewLine, warningMessages.Distinct(StringComparer.Ordinal))}",
                    "WinTiles",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            SetStatus("清除固定失败，请查看弹窗提示。", Brushes.Firebrick);
            MessageBox.Show(this, exception.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
            RefreshActionButtonsState();
        }
    }

    private async Task DeleteSingleTileAsync(TileHistoryItemViewModel historyItem)
    {
        RefreshEnvironmentState();
        if (!_viewModel.IsClassicStartAvailable || !_viewModel.AreToolsAvailable)
        {
            MessageBox.Show(this, _viewModel.AvailabilityMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _viewModel.IsBusy = true;
            RefreshActionButtonsState();

            var deleteResult = await TryDeletePinnedTileAsync(historyItem.TileRecord).ConfigureAwait(true);
            await ReconcileBatchRecordsAsync().ConfigureAwait(true);
            await RefreshHistoryAsync().ConfigureAwait(true);

            var statusMessage = deleteResult.Deleted
                ? $"已删除 {ResolveTilePositionText(historyItem.TileRecord)}。"
                : $"删除 {ResolveTilePositionText(historyItem.TileRecord)} 时遇到问题。";
            SetStatus(statusMessage, deleteResult.Deleted ? Brushes.SeaGreen : Brushes.DarkGoldenrod);

            if (!string.IsNullOrWhiteSpace(deleteResult.Message))
            {
                MessageBox.Show(this, deleteResult.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            SetStatus("删除单块失败，请查看弹窗提示。", Brushes.Firebrick);
            MessageBox.Show(this, exception.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
            RefreshActionButtonsState();
        }
    }

    private async Task DeleteBatchAsync(TileBatchHistoryItemViewModel historyItem)
    {
        RefreshEnvironmentState();
        if (!_viewModel.IsClassicStartAvailable || !_viewModel.AreToolsAvailable)
        {
            MessageBox.Show(this, _viewModel.AvailabilityMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _viewModel.IsBusy = true;
            RefreshActionButtonsState();

            var deletedCount = 0;
            var warningMessages = new List<string>();
            foreach (var tileItem in historyItem.Tiles)
            {
                var deleteResult = await TryDeletePinnedTileAsync(tileItem.TileRecord).ConfigureAwait(true);
                if (deleteResult.Deleted)
                {
                    deletedCount++;
                }

                if (!string.IsNullOrWhiteSpace(deleteResult.Message))
                {
                    warningMessages.Add(deleteResult.Message);
                }
            }

            await ReconcileBatchRecordsAsync().ConfigureAwait(true);
            await RefreshHistoryAsync().ConfigureAwait(true);

            var statusMessage = deletedCount == 0
                ? $"未能完整删除批次：{historyItem.DisplayTitle}"
                : $"已处理批次：{historyItem.DisplayTitle}，成功删除 {deletedCount} 块。";
            SetStatus(statusMessage, deletedCount == historyItem.Tiles.Count ? Brushes.SeaGreen : Brushes.DarkGoldenrod);

            if (warningMessages.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, warningMessages.Distinct(StringComparer.Ordinal)),
                    "WinTiles",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            SetStatus("删除批次失败，请查看弹窗提示。", Brushes.Firebrick);
            MessageBox.Show(this, exception.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
            RefreshActionButtonsState();
        }
    }

    private async Task RefreshHistoryAsync(string? preferredTileId = null)
    {
        var tileRecords = await _applicationContext.TileRecordStore.LoadAllTileRecordsAsync().ConfigureAwait(true);
        var batchRecords = await _applicationContext.TileRecordStore.LoadAllTileBatchRecordsAsync().ConfigureAwait(true);

        var attemptTasks = tileRecords.ToDictionary(
            tileRecord => tileRecord.TileId,
            tileRecord => _applicationContext.TileRecordStore.LoadPinAttemptAsync(tileRecord.TileId));
        await Task.WhenAll(attemptTasks.Values).ConfigureAwait(true);

        var pinAttempts = attemptTasks.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Result);

        var historyItems = BuildBatchHistoryItems(tileRecords, batchRecords, pinAttempts, preferredTileId);

        _viewModel.BatchHistoryItems.Clear();
        foreach (var historyItem in historyItems)
        {
            _viewModel.BatchHistoryItems.Add(historyItem);
        }

        _viewModel.HasHistoryItems = historyItems.Count > 0;
        _viewModel.HistoryCountText = $"{historyItems.Count} 批";
        RefreshActionButtonsState();
    }

    private List<TileBatchHistoryItemViewModel> BuildBatchHistoryItems(
        IReadOnlyList<TileRecord> tileRecords,
        IReadOnlyList<TileBatchRecord> batchRecords,
        IReadOnlyDictionary<string, PinAttemptRecord?> pinAttempts,
        string? preferredTileId)
    {
        var tileRecordsById = tileRecords.ToDictionary(tileRecord => tileRecord.TileId, StringComparer.Ordinal);
        var consumedTileIds = new HashSet<string>(StringComparer.Ordinal);
        var historyItems = new List<TileBatchHistoryItemViewModel>();

        foreach (var batchRecord in batchRecords)
        {
            var groupedTileRecords = batchRecord.TileIds
                .Select(tileId => tileRecordsById.TryGetValue(tileId, out var tileRecord) ? tileRecord : null)
                .Where(tileRecord => tileRecord is not null)
                .Cast<TileRecord>()
                .Concat(tileRecords.Where(tileRecord => string.Equals(tileRecord.BatchId, batchRecord.BatchId, StringComparison.Ordinal)))
                .DistinctBy(tileRecord => tileRecord.TileId, StringComparer.Ordinal)
                .ToArray();

            if (groupedTileRecords.Length == 0)
            {
                continue;
            }

            foreach (var tileRecord in groupedTileRecords)
            {
                consumedTileIds.Add(tileRecord.TileId);
            }

            historyItems.Add(CreateBatchHistoryItem(batchRecord, groupedTileRecords, pinAttempts, preferredTileId));
        }

        foreach (var legacyGroup in tileRecords
                     .Where(tileRecord => !consumedTileIds.Contains(tileRecord.TileId))
                     .GroupBy(tileRecord => string.IsNullOrWhiteSpace(tileRecord.BatchId) ? tileRecord.TileId : tileRecord.BatchId!, StringComparer.Ordinal))
        {
            var groupedTileRecords = legacyGroup.ToArray();
            var exemplar = groupedTileRecords[0];
            var syntheticBatchRecord = new TileBatchRecord
            {
                BatchId = string.IsNullOrWhiteSpace(exemplar.BatchId) ? exemplar.TileId : exemplar.BatchId!,
                Title = TileIdentityBuilder.BuildBatchDisplayTitle(exemplar.SourceImagePath),
                SourceImagePath = exemplar.SourceImagePath,
                CreatedAtUtc = GetRecordTimestampUtc(exemplar),
                TileIds = groupedTileRecords.Select(tileRecord => tileRecord.TileId).ToList(),
                DefaultClickAction = exemplar.ClickAction
            };

            historyItems.Add(CreateBatchHistoryItem(syntheticBatchRecord, groupedTileRecords, pinAttempts, preferredTileId));
        }

        return historyItems
            .OrderByDescending(item => item.Tiles.Max(tile => tile.SortTimestampUtc))
            .ThenBy(item => item.DisplayTitle, StringComparer.Ordinal)
            .ToList();
    }

    private TileBatchHistoryItemViewModel CreateBatchHistoryItem(
        TileBatchRecord batchRecord,
        IReadOnlyList<TileRecord> tileRecords,
        IReadOnlyDictionary<string, PinAttemptRecord?> pinAttempts,
        string? preferredTileId)
    {
        var tileItems = tileRecords
            .Select(tileRecord => CreateTileHistoryItem(tileRecord, pinAttempts.TryGetValue(tileRecord.TileId, out var pinAttempt) ? pinAttempt : null))
            .OrderBy(tileItem => tileItem.TileRecord.TileIndex ?? int.MaxValue)
            .ThenBy(tileItem => tileItem.TileRecord.GridRow ?? int.MaxValue)
            .ThenBy(tileItem => tileItem.TileRecord.GridColumn ?? int.MaxValue)
            .ThenBy(tileItem => tileItem.TileId, StringComparer.Ordinal)
            .ToArray();

        var newestTimestamp = tileItems.Max(tileItem => tileItem.SortTimestampUtc);
        var successCount = tileItems.Count(tileItem => tileItem.PinAttempt?.Status != PinHelperResultStatus.Failure);
        var failureCount = tileItems.Length - successCount;
        var hasWarning = tileItems.Any(tileItem => tileItem.PinAttempt?.Status == PinHelperResultStatus.Warning);
        var summaryBrush = failureCount > 0
            ? Brushes.DarkGoldenrod
            : hasWarning
                ? Brushes.DarkGoldenrod
                : Brushes.SeaGreen;
        var summaryText = failureCount > 0
            ? $"成功 {successCount} 块，失败 {failureCount} 块"
            : hasWarning
                ? $"共 {tileItems.Length} 块，其中部分需要手动确认"
                : $"共 {tileItems.Length} 块，已完成固定";
        var actionSummary = TileClickActionService.Validate(batchRecord.DefaultClickAction).SummaryText;

        var batchHistoryItem = new TileBatchHistoryItemViewModel
        {
            BatchId = batchRecord.BatchId,
            DisplayTitle = batchRecord.Title,
            ThumbnailImage = TryCreateBitmapImage(batchRecord.SourceImagePath, 180),
            HasThumbnailImage = File.Exists(batchRecord.SourceImagePath),
            AttemptedAtText = newestTimestamp == DateTimeOffset.MinValue
                ? "未记录时间"
                : newestTimestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            SummaryText = summaryText,
            SummaryBrush = summaryBrush,
            ActionSummaryText = actionSummary,
            TileCountText = $"{tileItems.Length} 块",
            SuccessCount = successCount,
            FailureCount = failureCount,
            SourceImagePath = batchRecord.SourceImagePath,
            IsExpanded = tileItems.Any(tileItem => string.Equals(tileItem.TileId, preferredTileId, StringComparison.Ordinal))
        };

        foreach (var tileItem in tileItems)
        {
            batchHistoryItem.Tiles.Add(tileItem);
        }

        return batchHistoryItem;
    }

    private TileHistoryItemViewModel CreateTileHistoryItem(TileRecord tileRecord, PinAttemptRecord? pinAttempt)
    {
        var sortTimestampUtc = pinAttempt?.AttemptedAtUtc ?? GetRecordTimestampUtc(tileRecord);
        var attemptedAtText = sortTimestampUtc == DateTimeOffset.MinValue
            ? "未记录时间"
            : sortTimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var detailText = pinAttempt is null
            ? "已保存固定记录"
            : CombineWarnings(pinAttempt.Message, pinAttempt.Warning) ?? pinAttempt.Message;
        var detailBrush = pinAttempt is null
            ? Brushes.DarkSlateBlue
            : MapStatusBrush(pinAttempt.Status);
        var actionDetailText = BuildTileActionDetailText(tileRecord.ClickAction);
        var previewPath = tileRecord.PreviewImagePath;
        var thumbnailImage = !string.IsNullOrWhiteSpace(previewPath)
            ? TryCreateBitmapImage(previewPath, 160)
            : TryCreateBitmapImage(tileRecord.SourceImagePath, 160);

        return new TileHistoryItemViewModel
        {
            TileId = tileRecord.TileId,
            DisplayTitle = ResolveHistoryDisplayTitle(tileRecord),
            ThumbnailImage = thumbnailImage,
            HasThumbnailImage = thumbnailImage is not null,
            GridPositionText = ResolveTilePositionText(tileRecord),
            AttemptedAtText = attemptedAtText,
            DetailText = detailText,
            DetailBrush = detailBrush,
            ActionDetailText = actionDetailText,
            SortTimestampUtc = sortTimestampUtc,
            TileRecord = tileRecord,
            PinAttempt = pinAttempt
        };
    }

    private async Task ReconcileBatchRecordsAsync()
    {
        var tileRecords = await _applicationContext.TileRecordStore.LoadAllTileRecordsAsync().ConfigureAwait(true);
        var batchRecords = await _applicationContext.TileRecordStore.LoadAllTileBatchRecordsAsync().ConfigureAwait(true);
        var batchRecordsById = batchRecords.ToDictionary(batchRecord => batchRecord.BatchId, StringComparer.Ordinal);

        var groupedTileRecords = tileRecords
            .Where(tileRecord => !string.IsNullOrWhiteSpace(tileRecord.BatchId))
            .GroupBy(tileRecord => tileRecord.BatchId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group
                .OrderBy(tileRecord => tileRecord.TileIndex ?? int.MaxValue)
                .ThenBy(tileRecord => tileRecord.GridRow ?? int.MaxValue)
                .ThenBy(tileRecord => tileRecord.GridColumn ?? int.MaxValue)
                .ThenBy(tileRecord => tileRecord.TileId, StringComparer.Ordinal)
                .ToArray(), StringComparer.Ordinal);

        foreach (var groupedRecord in groupedTileRecords)
        {
            var batchId = groupedRecord.Key;
            var tileIds = groupedRecord.Value.Select(tileRecord => tileRecord.TileId).ToList();
            var exemplar = groupedRecord.Value[0];

            if (batchRecordsById.TryGetValue(batchId, out var existingBatchRecord))
            {
                var sourceImagePath = File.Exists(existingBatchRecord.SourceImagePath)
                    ? existingBatchRecord.SourceImagePath
                    : exemplar.SourceImagePath;
                var updatedBatchRecord = new TileBatchRecord
                {
                    BatchId = batchId,
                    Title = string.IsNullOrWhiteSpace(existingBatchRecord.Title)
                        ? TileIdentityBuilder.BuildBatchDisplayTitle(sourceImagePath)
                        : existingBatchRecord.Title,
                    SourceImagePath = sourceImagePath,
                    CreatedAtUtc = existingBatchRecord.CreatedAtUtc == DateTimeOffset.MinValue
                        ? GetRecordTimestampUtc(exemplar)
                        : existingBatchRecord.CreatedAtUtc,
                    TileIds = tileIds,
                    DefaultClickAction = existingBatchRecord.DefaultClickAction ?? exemplar.ClickAction
                };

                if (!existingBatchRecord.TileIds.SequenceEqual(tileIds, StringComparer.Ordinal) ||
                    !string.Equals(existingBatchRecord.SourceImagePath, updatedBatchRecord.SourceImagePath, StringComparison.Ordinal) ||
                    !AreTileClickActionsEqual(existingBatchRecord.DefaultClickAction, updatedBatchRecord.DefaultClickAction))
                {
                    await _applicationContext.TileRecordStore.SaveTileBatchRecordAsync(updatedBatchRecord).ConfigureAwait(true);
                }
            }
            else
            {
                var createdBatchRecord = new TileBatchRecord
                {
                    BatchId = batchId,
                    Title = TileIdentityBuilder.BuildBatchDisplayTitle(exemplar.SourceImagePath),
                    SourceImagePath = exemplar.SourceImagePath,
                    CreatedAtUtc = GetRecordTimestampUtc(exemplar),
                    TileIds = tileIds,
                    DefaultClickAction = exemplar.ClickAction
                };
                await _applicationContext.TileRecordStore.SaveTileBatchRecordAsync(createdBatchRecord).ConfigureAwait(true);
            }
        }

        foreach (var batchRecord in batchRecords)
        {
            if (!groupedTileRecords.ContainsKey(batchRecord.BatchId))
            {
                _applicationContext.TileRecordStore.DeleteBatchDirectory(batchRecord.BatchId);
            }
        }
    }

    private async Task<(bool Deleted, string? Message)> TryDeletePinnedTileAsync(TileRecord tileRecord)
    {
        // 无论 pin helper 是否报告完全成功，都继续清理本地快捷方式和记录，避免失败记录无法从历史里删除。
        var unpinResult = await _applicationContext.PinHelperInvoker.UnpinImageAsync(
            _applicationContext.PinHelperPath,
            tileRecord.TileId).ConfigureAwait(true);

        var messages = new List<string>();
        if (unpinResult.Status == PinHelperResultStatus.Warning || unpinResult.Status == PinHelperResultStatus.Failure)
        {
            var helperMessage = CombineWarnings(unpinResult.Message, unpinResult.Warning);
            if (!string.IsNullOrWhiteSpace(helperMessage))
            {
                messages.Add($"{ResolveTilePositionText(tileRecord)}：{helperMessage}");
            }
        }

        try
        {
            DeleteFileIfExists(tileRecord.ShortcutPath);
        }
        catch (Exception cleanupException)
        {
            messages.Add($"{ResolveTilePositionText(tileRecord)}：删除快捷方式失败：{cleanupException.Message}");
        }

        try
        {
            _applicationContext.TileRecordStore.DeleteTileDirectory(tileRecord.TileId);
        }
        catch (Exception cleanupException)
        {
            messages.Add($"{ResolveTilePositionText(tileRecord)}：删除本地记录失败：{cleanupException.Message}");
        }

        return (true, CombineWarnings(messages.ToArray()));
    }

    private void InitializeCropCells()
    {
        _viewModel.CropCells.Clear();
        for (var row = 0; row < CropLayoutCalculator.GridDimension; row++)
        {
            for (var column = 0; column < CropLayoutCalculator.GridDimension; column++)
            {
                _viewModel.CropCells.Add(new CropCellViewModel(row, column));
            }
        }
    }

    private void UpdateCropBoardLayout()
    {
        if (CropBoardHost is null)
        {
            return;
        }

        var boardSize = Math.Min(CropBoardHost.ActualWidth, CropBoardHost.ActualHeight);
        if (boardSize <= 0)
        {
            return;
        }

        _viewModel.CropBoardSize = boardSize;

        var boardSizeF = GetCropBoardSize();
        var cellSize = _cropLayoutCalculator.CalculateCellSize(boardSizeF, CropCellGap);
        foreach (var cell in _viewModel.CropCells)
        {
            var cellBounds = _cropLayoutCalculator.GetCellBounds(boardSizeF, CropCellGap, cell.Row, cell.Column);
            cell.Left = cellBounds.Left;
            cell.Top = cellBounds.Top;
            cell.Size = cellSize;
        }

        EnsureCropTransformWithinBounds(recenterWhenNeeded: false);
    }

    private void ToggleCellAtPoint(Point point)
    {
        var cell = _viewModel.CropCells.FirstOrDefault(cropCell =>
            point.X >= cropCell.Left &&
            point.X <= cropCell.Left + cropCell.Size &&
            point.Y >= cropCell.Top &&
            point.Y <= cropCell.Top + cropCell.Size);

        if (cell is null || !cell.IsAvailable)
        {
            return;
        }

        var hadNoSelection = GetActiveCells().Count == 0;
        cell.IsSelected = !cell.IsSelected;
        UpdateSelectionSummary();

        if (_viewModel.HasCropImage)
        {
            EnsureCropTransformWithinBounds(recenterWhenNeeded: hadNoSelection || GetActiveCells().Count == 1);
        }

        RefreshActionButtonsState();
    }

    private void LoadSelectedImage(string imagePath)
    {
        try
        {
            var bitmap = CreateBitmapImage(imagePath);
            _viewModel.CropImage = bitmap;
            _viewModel.HasCropImage = true;
        _selectedImagePixelSize = new DrawingSizeF(bitmap.PixelWidth, bitmap.PixelHeight);

            if (CropImageElement is not null)
            {
                CropImageElement.Width = bitmap.PixelWidth;
                CropImageElement.Height = bitmap.PixelHeight;
            }

            _viewModel.CropTitle = Path.GetFileName(imagePath);
            _viewModel.CropSubtitle = "已选择图片。点击格子启用区域，然后用滚轮缩放、拖拽位置。";
            SetStatus($"已选择图片：{Path.GetFileName(imagePath)}", Brushes.DarkSlateBlue);
            ResetCropTransform();
        }
        catch (Exception exception)
        {
            _selectedImagePath = null;
            _selectedImagePixelSize = DrawingSizeF.Empty;
            _viewModel.CropImage = null;
            _viewModel.HasCropImage = false;
            SetStatus($"加载图片失败：{exception.Message}", Brushes.Firebrick);
        }

        RefreshActionButtonsState();
    }

    // 固定完一批后只清空当前图片，保留已启用区域，方便用户继续按同一布局处理下一张图。
    private void ClearSelectedImageAfterBatchPin()
    {
        _selectedImagePath = null;
        _selectedImagePixelSize = DrawingSizeF.Empty;
        _dragStartPoint = null;
        _dragStartOffset = DrawingPointF.Empty;
        _isDraggingCropImage = false;

        _viewModel.CropImage = null;
        _viewModel.HasCropImage = false;
        _viewModel.CropTitle = "尚未选择图片";
        _viewModel.CropSubtitle = "本批固定完成。已保留启用区域，请重新选择图片继续固定。";

        if (CropImageElement is not null)
        {
            CropImageElement.Width = 0;
            CropImageElement.Height = 0;
        }

        ApplyCropState(1f, DrawingPointF.Empty, 1f);
        RefreshActionButtonsState();
    }

    private void ResetCropTransform()
    {
        if (!_viewModel.HasCropImage || _selectedImagePixelSize.Width <= 0 || _selectedImagePixelSize.Height <= 0)
        {
            ApplyCropState(1f, DrawingPointF.Empty, 1f);
            return;
        }

        var activeCells = GetActiveCells();
        var boardSize = GetCropBoardSize();
        var minimumScale = _cropLayoutCalculator.CalculateMinimumScale(
            _selectedImagePixelSize,
            boardSize,
            CropCellGap,
            activeCells);
        var centeredOffset = _cropLayoutCalculator.CalculateCenteredOffset(
            _selectedImagePixelSize,
            minimumScale,
            boardSize,
            CropCellGap,
            activeCells);

        ApplyCropState(minimumScale, centeredOffset, minimumScale);
    }

    private void EnsureCropTransformWithinBounds(bool recenterWhenNeeded)
    {
        if (!_viewModel.HasCropImage || _selectedImagePixelSize.Width <= 0 || _selectedImagePixelSize.Height <= 0)
        {
            return;
        }

        var activeCells = GetActiveCells();
        var boardSize = GetCropBoardSize();
        var minimumScale = _cropLayoutCalculator.CalculateMinimumScale(
            _selectedImagePixelSize,
            boardSize,
            CropCellGap,
            activeCells);

        var scale = Math.Max((float)_viewModel.CropScale, minimumScale);
        var offset = recenterWhenNeeded
            ? _cropLayoutCalculator.CalculateCenteredOffset(
                _selectedImagePixelSize,
                scale,
                boardSize,
                CropCellGap,
                activeCells)
            : _cropLayoutCalculator.ClampOffset(
                _selectedImagePixelSize,
                scale,
                boardSize,
                CropCellGap,
                activeCells,
                new DrawingPointF((float)_viewModel.CropOffsetX, (float)_viewModel.CropOffsetY));

        ApplyCropState(scale, offset, minimumScale);
    }

    private void ApplyCropState(float scale, DrawingPointF offset, float minimumScale)
    {
        _viewModel.CropScale = scale;
        _viewModel.MinimumCropScale = minimumScale;
        _viewModel.CropOffsetX = offset.X;
        _viewModel.CropOffsetY = offset.Y;

        if (CropImageScaleTransform is not null)
        {
            CropImageScaleTransform.ScaleX = scale;
            CropImageScaleTransform.ScaleY = scale;
        }

        if (CropImageTranslateTransform is not null)
        {
            CropImageTranslateTransform.X = offset.X;
            CropImageTranslateTransform.Y = offset.Y;
        }

        var zoomPercent = minimumScale <= 0f
            ? 100d
            : Math.Round(scale / minimumScale * 100d);
        _viewModel.ZoomText = $"缩放 {zoomPercent:0}%";
    }

    private void UpdateSelectionSummary()
    {
        var activeCells = _viewModel.CropCells
            .Where(cell => cell.IsAvailable && cell.IsSelected)
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .ToArray();

        // 统一维护选中数量，供右侧设置卡片直接绑定显示。
        _viewModel.ActiveCropCellCount = activeCells.Length;
        _viewModel.SelectionSummaryText = activeCells.Length == 0
            ? "尚未启用裁切区域"
            : $"已启用 {activeCells.Length} / {_viewModel.CropCellTotalCount} 个区域";
    }

    private void ApplyPanelMode(MainPanelMode panelMode)
    {
        _viewModel.PanelMode = panelMode;
        _viewModel.StatusHintText = panelMode == MainPanelMode.Crop
            ? "提示：拖拽图片可以调整位置，滚轮可以缩放，缩到刚好铺满启用区域时会自动吸附。"
            : "提示：删除会同步移除开始菜单磁贴和本地记录；失败记录也可以直接清理。";
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

    /// <summary>
    /// 根据当前界面输入重算点击动作的摘要、提示和固定按钮状态。
    /// </summary>
    private void UpdateClickActionState()
    {
        var validationResult = TileClickActionService.Validate(BuildCurrentClickAction());
        _viewModel.ClickActionSummaryText = validationResult.SummaryText;
        _viewModel.ClickActionValidationText = validationResult.ValidationMessage;
        _viewModel.ClickActionValidationBrush = validationResult.IsValid
            ? Brushes.DarkSlateBlue
            : Brushes.Firebrick;
        RefreshActionButtonsState();
    }

    private void RefreshActionButtonsState()
    {
        var activeCellsCount = GetActiveCells().Count;
        var environmentReady = _viewModel.IsClassicStartAvailable && _viewModel.AreToolsAvailable;
        var clickActionValidation = TileClickActionService.Validate(BuildCurrentClickAction());

        _viewModel.CanPinImage =
            !_viewModel.IsBusy &&
            !string.IsNullOrWhiteSpace(_selectedImagePath) &&
            activeCellsCount > 0 &&
            environmentReady &&
            clickActionValidation.IsValid;
        _viewModel.CanOpenHistory = !_viewModel.IsBusy;
        _viewModel.CanClearSelection = !_viewModel.IsBusy && activeCellsCount > 0;
        _viewModel.CanClearAllPinnedTiles = !_viewModel.IsBusy && environmentReady && _viewModel.HasHistoryItems;
        _viewModel.CanOpenRecordFolder = !_viewModel.IsBusy;
        _viewModel.CanCheckForUpdates = !_viewModel.IsBusy && !_viewModel.IsCheckingForUpdates;
    }

    private async Task CheckForUpdatesAsync(
        bool showUpToDateMessage,
        bool showFailureMessage,
        bool showUpdatePrompt)
    {
        if (_viewModel.IsCheckingForUpdates)
        {
            return;
        }

        try
        {
            _viewModel.IsCheckingForUpdates = true;
            _viewModel.UpdateStatusText = "正在检查 GitHub Releases…";
            RefreshActionButtonsState();

            var result = await _applicationContext.ReleaseUpdateService.CheckForUpdatesAsync().ConfigureAwait(true);
            _viewModel.UpdateStatusText = result.ErrorMessage ?? result.SummaryText;

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                if (showFailureMessage)
                {
                    MessageBox.Show(this, result.ErrorMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return;
            }

            if (result.IsUpdateAvailable && result.Release is not null)
            {
                SetStatus(result.SummaryText, Brushes.DarkSlateBlue);
                // 先征求用户确认，再决定是否下载更新包，避免启动后自动占用带宽和磁盘。
                _viewModel.UpdateStatusText = $"发现新版本 {result.Release.Version.ToString(3)}，等待用户确认是否下载。";
                if (showUpdatePrompt)
                {
                    await PromptDownloadUpdateAsync(result.Release, result.CurrentVersionText).ConfigureAwait(true);
                }

                return;
            }

            if (showUpToDateMessage)
            {
                MessageBox.Show(this, result.SummaryText, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        finally
        {
            _viewModel.IsCheckingForUpdates = false;
            RefreshActionButtonsState();
        }
    }

    private async Task PromptDownloadUpdateAsync(UpdateReleaseInfo release, string currentVersionText)
    {
        var message = $"发现新版本 {release.Version.ToString(3)}，当前版本 {currentVersionText}。\n\n是否现在下载更新包？";
        var result = MessageBox.Show(this, message, "WinTiles 更新", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes)
        {
            _viewModel.UpdateStatusText = $"发现新版本 {release.Version.ToString(3)}，你可以稍后手动点击“检查更新”再下载。";
            return;
        }

        // 用户明确确认后才开始下载，下载结束前不触发安装流程。
        _viewModel.UpdateStatusText = $"正在下载新版本 {release.Version.ToString(3)}…";
        var preparedUpdate = await _applicationContext.SilentUpdateService
            .PrepareUpdateAsync(release)
            .ConfigureAwait(true);
        _preparedUpdate = preparedUpdate;
        _viewModel.UpdateStatusText = preparedUpdate.Message;

        if (preparedUpdate.IsReadyToInstall)
        {
            PromptInstallPreparedUpdate(preparedUpdate);
            return;
        }

        if (!string.IsNullOrWhiteSpace(release.HtmlUrl))
        {
            var openReleasePageResult = MessageBox.Show(
                this,
                BuildManualDownloadPromptMessage(preparedUpdate.Message),
                "WinTiles 更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (openReleasePageResult == MessageBoxResult.Yes)
            {
                OpenUrl(release.HtmlUrl);
            }
        }
    }

    /// <summary>
    /// 构造手动下载更新时展示给用户的提示文案，并补充 Data 目录迁移说明。
    /// </summary>
    /// <param name="preparedUpdateMessage">当前更新准备阶段返回的提示文本。</param>
    /// <returns>供弹窗直接展示的完整提示内容。</returns>
    private string BuildManualDownloadPromptMessage(string preparedUpdateMessage)
    {
        var applicationDirectory = Path.GetDirectoryName(_applicationContext.MainExecutablePath) ?? AppContext.BaseDirectory;
        var dataDirectory = Path.Combine(applicationDirectory, "Data");
        return $"{preparedUpdateMessage}\n\n如果你改为手动下载新版本，请把当前目录下的 Data 文件夹一起迁移到新版本目录中，否则现有数据不会自动带过去。\n\n当前 Data 路径：{dataDirectory}\n\n是否改为打开 GitHub Release 页面手动下载？";
    }

    private void PromptInstallPreparedUpdate(SilentUpdatePreparationResult preparedUpdate)
    {
        var message = $"{preparedUpdate.Message}\n\n是否现在重启并自动完成升级？";
        var result = MessageBox.Show(this, message, "WinTiles 更新", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (!_applicationContext.SilentUpdateService.TryStartInstaller(
                preparedUpdate,
                Environment.ProcessId,
                out var errorMessage))
        {
            MessageBox.Show(this, errorMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 启动外部升级器后尽快退出当前进程，让文件替换可以顺利完成。
        Application.Current.Shutdown();
    }

    private void OpenUrl(string url)
    {
        try
        {
            // 使用系统默认浏览器打开发布页，避免把下载逻辑塞回应用内部。
            LaunchUrl(url);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"打开发布页失败：{exception.Message}", "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// 根据当前界面输入生成即将保存的点击动作配置。
    /// </summary>
    /// <returns>规范化后的点击动作；如果用户未设置，则返回 null。</returns>
    private TileClickAction? BuildCurrentClickAction()
    {
        var action = _viewModel.SelectedClickActionType switch
        {
            TileClickActionType.OpenUrl => new TileClickAction
            {
                Type = TileClickActionType.OpenUrl,
                Url = _viewModel.ClickActionUrl
            },
            TileClickActionType.OpenApplication => new TileClickAction
            {
                Type = TileClickActionType.OpenApplication,
                ApplicationPath = _viewModel.ClickActionApplicationPath,
                Arguments = _viewModel.ClickActionArguments,
                WorkingDirectory = _viewModel.ClickActionWorkingDirectory
            },
            _ => new TileClickAction
            {
                Type = TileClickActionType.None
            }
        };

        return TileClickActionService.Normalize(action);
    }

    /// <summary>
    /// 尝试执行磁贴绑定的点击动作；执行成功时直接返回 true。
    /// </summary>
    /// <param name="tileRecord">当前被点击的磁贴记录。</param>
    /// <returns>若动作已成功执行则返回 true，否则返回 false 以便继续回退到历史定位。</returns>
    private async Task<bool> TryExecuteTileClickActionAsync(TileRecord tileRecord)
    {
        var clickAction = TileClickActionService.Normalize(tileRecord.ClickAction);
        if (clickAction is null && !string.IsNullOrWhiteSpace(tileRecord.BatchId))
        {
            var batchRecord = await _applicationContext.TileRecordStore
                .LoadTileBatchRecordAsync(tileRecord.BatchId)
                .ConfigureAwait(true);
            clickAction = TileClickActionService.Normalize(batchRecord?.DefaultClickAction);
        }

        if (clickAction is null)
        {
            return false;
        }

        var validationResult = TileClickActionService.Validate(clickAction);
        if (!validationResult.IsValid)
        {
            ApplyPanelMode(MainPanelMode.History);
            await RefreshHistoryAsync(tileRecord.TileId).ConfigureAwait(true);
            SetStatus("已打开 WinTiles，但该磁贴的点击动作配置无效。", Brushes.DarkGoldenrod);
            MessageBox.Show(this, validationResult.ValidationMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }

        try
        {
            switch (clickAction.Type)
            {
                case TileClickActionType.OpenUrl:
                    LaunchUrl(clickAction.Url!);
                    Application.Current.Shutdown();
                    return true;
                case TileClickActionType.OpenApplication:
                    LaunchApplication(clickAction);
                    Application.Current.Shutdown();
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception exception)
        {
            ApplyPanelMode(MainPanelMode.History);
            await RefreshHistoryAsync(tileRecord.TileId).ConfigureAwait(true);
            SetStatus("已打开 WinTiles，但执行点击动作失败。", Brushes.DarkGoldenrod);
            MessageBox.Show(this, $"执行点击动作失败：{exception.Message}", "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }
    }

    /// <summary>
    /// 启动磁贴点击后绑定的应用目标。
    /// </summary>
    /// <param name="action">已经通过基础校验的应用动作配置。</param>
    private static void LaunchApplication(TileClickAction action)
    {
        var applicationPath = action.ApplicationPath?.Trim();
        if (string.IsNullOrWhiteSpace(applicationPath) || !File.Exists(applicationPath))
        {
            throw new FileNotFoundException("目标应用不存在，请重新配置后再尝试。", applicationPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = applicationPath,
            Arguments = action.Arguments ?? string.Empty,
            UseShellExecute = true
        };

        var workingDirectory = action.WorkingDirectory?.Trim();
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        Process.Start(startInfo);
    }

    /// <summary>
    /// 使用系统默认浏览器打开网页地址。
    /// </summary>
    /// <param name="url">待打开的网址。</param>
    private static void LaunchUrl(string url)
    {
        if (!TileClickActionService.IsSupportedUrl(url))
        {
            throw new InvalidOperationException("网页地址无效，首版仅支持 http:// 或 https:// 链接。");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private string CopySourceImageToBatchDirectory(string batchDirectory, string sourceImagePath)
    {
        var sourceExtension = Path.GetExtension(sourceImagePath);
        var copiedSourcePath = Path.Combine(batchDirectory, $"Source{sourceExtension}");
        File.Copy(sourceImagePath, copiedSourcePath, overwrite: true);
        return copiedSourcePath;
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

    private string CreateStartMenuShortcut(
        string sourceImagePath,
        string tileId,
        string hostExePath,
        string appUserModelId,
        string shortcutIconPath,
        string manifestPath,
        string displayTitle,
        int? gridRow,
        int? gridColumn)
    {
        var shortcutPath = BuildStartMenuShortcutPath(sourceImagePath, tileId, gridRow, gridColumn);
        return _applicationContext.StartMenuShortcutService.CreateShortcut(
            shortcutPath,
            hostExePath,
            string.Empty,
            Path.GetDirectoryName(hostExePath)!,
            displayTitle,
            appUserModelId,
            shortcutIconPath,
            manifestPath);
    }

    /// <summary>
    /// 判断两个点击动作配置是否等价，避免批次修复时反复无意义改写。
    /// </summary>
    private static bool AreTileClickActionsEqual(TileClickAction? left, TileClickAction? right)
    {
        var normalizedLeft = TileClickActionService.Normalize(left);
        var normalizedRight = TileClickActionService.Normalize(right);

        if (normalizedLeft is null || normalizedRight is null)
        {
            return normalizedLeft is null && normalizedRight is null;
        }

        return normalizedLeft.Type == normalizedRight.Type
               && string.Equals(normalizedLeft.Url, normalizedRight.Url, StringComparison.Ordinal)
               && string.Equals(normalizedLeft.ApplicationPath, normalizedRight.ApplicationPath, StringComparison.Ordinal)
               && string.Equals(normalizedLeft.Arguments, normalizedRight.Arguments, StringComparison.Ordinal)
               && string.Equals(normalizedLeft.WorkingDirectory, normalizedRight.WorkingDirectory, StringComparison.Ordinal);
    }

    /// <summary>
    /// 生成历史列表里显示的点击动作说明。
    /// </summary>
    /// <param name="clickAction">磁贴保存的点击动作。</param>
    /// <returns>适合直接展示的中文说明。</returns>
    private static string BuildTileActionDetailText(TileClickAction? clickAction)
    {
        var summaryText = TileClickActionService.Validate(clickAction).SummaryText;
        const string prefix = "点击后：";
        var readableSummary = summaryText.StartsWith(prefix, StringComparison.Ordinal)
            ? summaryText[prefix.Length..]
            : summaryText;
        return $"点击动作：{readableSummary}";
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private PinAttemptRecord NormalizePinAttempt(
        StartMenuPinVerbResult shellPinResult,
        PinHelperResult? pinResult,
        string appUserModelId)
    {
        const TileRequestSize requestedSize = TileRequestSize.Medium2x2;

        if (shellPinResult.Invoked && pinResult is null)
        {
            return new PinAttemptRecord
            {
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                RequestedSize = requestedSize,
                Status = PinHelperResultStatus.Warning,
                Message = "已触发系统固定命令，但没有确认开始菜单最终结果，请检查实际显示。",
                PinMethod = $"{shellPinResult.TargetName}.{shellPinResult.VerbName}",
                IdentityKind = "AppUserModelID",
                IdentityValue = appUserModelId
            };
        }

        if (shellPinResult.Invoked && pinResult is not null)
        {
            var warning = CombineWarnings(shellPinResult.ErrorMessage, pinResult.Warning);
            var status = pinResult.Status;
            var message = pinResult.Message;

            if (pinResult.Status == PinHelperResultStatus.Failure)
            {
                status = PinHelperResultStatus.Warning;
                message = "已触发系统固定命令，但内部刷新失败，请检查开始菜单中的实际结果。";
                warning = CombineWarnings(shellPinResult.ErrorMessage, pinResult.Message, pinResult.Warning);
            }

            return new PinAttemptRecord
            {
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                RequestedSize = requestedSize,
                Status = status,
                Message = message,
                PinMethod = $"{shellPinResult.TargetName}.{shellPinResult.VerbName}+{pinResult.PinMethod}",
                Warning = warning,
                IdentityKind = pinResult.IdentityKind,
                IdentityValue = pinResult.IdentityValue,
                ContainsBefore = pinResult.ContainsBefore,
                ContainsAfterCommit = pinResult.ContainsAfterCommit,
                ContainsAfterReopen = pinResult.ContainsAfterReopen
            };
        }

        if (pinResult is not null)
        {
            return new PinAttemptRecord
            {
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                RequestedSize = requestedSize,
                Status = pinResult.Status,
                Message = pinResult.Message,
                PinMethod = pinResult.PinMethod,
                Warning = CombineWarnings(shellPinResult.ErrorMessage, pinResult.Warning),
                IdentityKind = pinResult.IdentityKind,
                IdentityValue = pinResult.IdentityValue,
                ContainsBefore = pinResult.ContainsBefore,
                ContainsAfterCommit = pinResult.ContainsAfterCommit,
                ContainsAfterReopen = pinResult.ContainsAfterReopen
            };
        }

        return new PinAttemptRecord
        {
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            RequestedSize = requestedSize,
            Status = PinHelperResultStatus.Failure,
            Message = "未能触发系统固定命令。",
            PinMethod = "ShellVerbUnavailable",
            Warning = shellPinResult.ErrorMessage
        };
    }

    private static PinAttemptRecord CreateFailureAttemptRecord(Exception exception)
    {
        return new PinAttemptRecord
        {
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            RequestedSize = TileRequestSize.Medium2x2,
            Status = PinHelperResultStatus.Failure,
            Message = $"生成或固定磁贴失败：{exception.Message}",
            PinMethod = "BatchCropPipeline"
        };
    }

    private static string? CombineWarnings(params string?[] messages)
    {
        var filteredMessages = messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return filteredMessages.Length == 0
            ? null
            : string.Join(Environment.NewLine, filteredMessages);
    }

    private IReadOnlyCollection<(int Row, int Column)> GetActiveCells()
    {
        return _viewModel.CropCells
            .Where(cell => cell.IsAvailable && cell.IsSelected)
            .Select(cell => (cell.Row, cell.Column))
            .ToArray();
    }

    /// <summary>
    /// 在右侧输入框失焦后，同步已启用数量和总数量到真实裁切网格。
    /// </summary>
    private void CropCountTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var requestedTotalCount = ParseCropCountText(CropCellTotalCountTextBox?.Text, _viewModel.CropCellTotalCount);
        var normalizedTotalCount = Math.Clamp(requestedTotalCount, 0, _viewModel.CropCells.Count);
        var requestedActiveCount = ParseCropCountText(ActiveCropCellCountTextBox?.Text, _viewModel.ActiveCropCellCount);
        var normalizedActiveCount = Math.Clamp(requestedActiveCount, 0, normalizedTotalCount);

        ApplyEditableCropCounts(normalizedActiveCount, normalizedTotalCount);
    }

    /// <summary>
    /// 解析输入框中的数量文本，解析失败时回退到给定默认值。
    /// </summary>
    private static int ParseCropCountText(string? text, int fallbackValue)
    {
        return int.TryParse(text, out var parsedValue)
            ? parsedValue
            : fallbackValue;
    }

    /// <summary>
    /// 按固定顺序重建可用区域和启用区域，确保输入值与真实裁切状态一致。
    /// </summary>
    private void ApplyEditableCropCounts(int activeCount, int totalCount)
    {
        var orderedCells = _viewModel.CropCells
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .ToArray();

        for (var index = 0; index < orderedCells.Length; index++)
        {
            var isAvailable = index < totalCount;
            orderedCells[index].IsAvailable = isAvailable;
            orderedCells[index].IsSelected = isAvailable && index < activeCount;
        }

        _viewModel.CropCellTotalCount = totalCount;
        UpdateSelectionSummary();

        if (_viewModel.HasCropImage)
        {
            EnsureCropTransformWithinBounds(recenterWhenNeeded: true);
        }

        RefreshActionButtonsState();
    }

    private DrawingSizeF GetCropBoardSize()
    {
        return new DrawingSizeF((float)_viewModel.CropBoardSize, (float)_viewModel.CropBoardSize);
    }

    private string BuildStartMenuShortcutPath(
        string sourceImagePath,
        string tileId,
        int? gridRow,
        int? gridColumn)
    {
        var startMenuPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "WinTiles");
        var shortcutFileName = TileIdentityBuilder.BuildShortcutFileName(sourceImagePath, tileId, gridRow, gridColumn);
        return Path.Combine(startMenuPrograms, shortcutFileName);
    }

    private string ResolveHistoryDisplayTitle(TileRecord tileRecord)
    {
        var shortcutTitle = _applicationContext.StartMenuShortcutService.TryReadTitle(tileRecord.ShortcutPath);
        if (!string.IsNullOrWhiteSpace(shortcutTitle))
        {
            return shortcutTitle;
        }

        return ResolveTilePositionText(tileRecord);
    }

    private static string ResolveTilePositionText(TileRecord tileRecord)
    {
        if (tileRecord.GridRow.HasValue && tileRecord.GridColumn.HasValue)
        {
            return $"第 {tileRecord.GridRow.Value + 1} 行 · 第 {tileRecord.GridColumn.Value + 1} 列";
        }

        if (tileRecord.TileIndex.HasValue)
        {
            return $"第 {tileRecord.TileIndex.Value + 1} 块";
        }

        return "未标注位置";
    }

    private static DateTimeOffset GetRecordTimestampUtc(TileRecord tileRecord)
    {
        if (File.Exists(tileRecord.ShortcutPath))
        {
            return new DateTimeOffset(File.GetCreationTimeUtc(tileRecord.ShortcutPath), TimeSpan.Zero);
        }

        if (File.Exists(tileRecord.SourceImagePath))
        {
            return new DateTimeOffset(File.GetCreationTimeUtc(tileRecord.SourceImagePath), TimeSpan.Zero);
        }

        return DateTimeOffset.MinValue;
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

    private static BitmapImage CreateBitmapImage(string imagePath, int? decodePixelWidth = null)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth.HasValue)
        {
            bitmap.DecodePixelWidth = decodePixelWidth.Value;
        }

        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapImage? TryCreateBitmapImage(string imagePath, int? decodePixelWidth = null)
    {
        try
        {
            if (!File.Exists(imagePath))
            {
                return null;
            }

            return CreateBitmapImage(imagePath, decodePixelWidth);
        }
        catch
        {
            return null;
        }
    }
}
