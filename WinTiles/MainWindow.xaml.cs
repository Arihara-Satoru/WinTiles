using Microsoft.Win32;
using System.Diagnostics;
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
    private TileHistoryItemViewModel? _selectedHistoryItem;
    private bool _isRefreshingHistory;

    public MainWindow(WinTilesApplicationContext applicationContext)
    {
        _applicationContext = applicationContext;
        _viewModel = new MainWindowViewModel();
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RecordLocationText = _applicationContext.TileRecordStore.RootDirectory;
    }

    public async Task InitializeAsync(string? startupTileId)
    {
        RefreshEnvironmentState();
        ResetDefaultTileSize();
        RefreshActionButtonsState();

        if (!string.IsNullOrWhiteSpace(startupTileId))
        {
            await HandleActivationAsync(startupTileId).ConfigureAwait(true);
            return;
        }

        await RefreshHistoryAsync().ConfigureAwait(true);
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
            await RefreshHistoryAsync().ConfigureAwait(true);
            RefreshActionButtonsState();
            return;
        }

        _selectedImagePath = null;
        await RefreshHistoryAsync(tileId).ConfigureAwait(true);

        RefreshActionButtonsState();
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
        _selectedHistoryItem = null;
        if (HistoryListBox is not null)
        {
            HistoryListBox.SelectedItem = null;
        }

        ShowCurrentImagePreview(openFileDialog.FileName);
        RefreshActionButtonsState();
    }

    private async void PinImageButton_Click(object sender, RoutedEventArgs e)
    {
        await PinCurrentImageAsync().ConfigureAwait(true);
    }

    private async void ClearPinButton_Click(object sender, RoutedEventArgs e)
    {
        await ClearAllPinnedTilesAsync().ConfigureAwait(true);
    }

    private async void DeleteSelectedHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await DeleteSelectedHistoryAsync().ConfigureAwait(true);
    }

    private void OpenRecordFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 让用户直接跳到本地记录目录，方便检查固定后的磁贴记录和清理结果。
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
            RefreshActionButtonsState();

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
            var shortcutDisplayTitle = TileIdentityBuilder.BuildShortcutDisplayTitle(_selectedImagePath!);
            var shortcutPath = CreateStartMenuShortcut(
                tileId,
                hostExePath,
                appUserModelId,
                generatedAssetSet.ShortcutIconPath,
                manifestPath,
                shortcutDisplayTitle);

            var tileRecord = new TileRecord
            {
                TileId = tileId,
                SourceImagePath = copiedSourcePath,
                RequestedSize = _viewModel.SelectedSize,
                HostExePath = hostExePath,
                ShortcutPath = shortcutPath,
                AssetsVersion = generatedAssetSet.AssetsVersion
            };

            await _applicationContext.TileRecordStore.SaveTileRecordAsync(tileRecord).ConfigureAwait(true);

            // 先保留系统入口作为兼容探测，真正的自动固定统一交给 helper，避免 2x2 只弹侧栏不落地。
            var shellPinResult = await _applicationContext.StartMenuPinVerbInvoker.TryPinAsync(
                appUserModelId,
                shortcutPath).ConfigureAwait(true);

            // helper 才是负责真正写入 Start.TileGrid 的路径，前端现在统一只走默认请求。
            var pinResult = await _applicationContext.PinHelperInvoker.PinImageAsync(
                _applicationContext.PinHelperPath,
                tileId,
                _viewModel.SelectedSize,
                hostExePath).ConfigureAwait(true);

            var normalizedAttempt = NormalizePinAttempt(shellPinResult, pinResult, appUserModelId);
            await _applicationContext.TileRecordStore.SavePinAttemptAsync(tileId, normalizedAttempt).ConfigureAwait(true);
            await RefreshHistoryAsync().ConfigureAwait(true);

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
                await RefreshHistoryAsync().ConfigureAwait(true);
                const string emptyMessage = "当前没有通过 WinTiles 固定的磁贴。";
                SetStatus(emptyMessage, Brushes.DarkGoldenrod);
                MessageBox.Show(this, emptyMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var clearedCount = 0;
            var warningMessages = new List<string>();
            var failureMessages = new List<string>();

            foreach (var tileRecord in tileRecords)
            {
                var deleteResult = await TryDeletePinnedTileAsync(tileRecord).ConfigureAwait(true);
                if (!deleteResult.Deleted)
                {
                    failureMessages.Add(deleteResult.FailureMessage ?? FormatTileMessage(tileRecord, "删除失败。"));
                    continue;
                }

                clearedCount++;
                if (!string.IsNullOrWhiteSpace(deleteResult.WarningMessage))
                {
                    warningMessages.Add(deleteResult.WarningMessage);
                }
            }

            await RefreshHistoryAsync().ConfigureAwait(true);

            if (clearedCount == 0)
            {
                var failureMessage = CombineWarnings(failureMessages.ToArray()) ?? "未能清除任何磁贴。";
                SetStatus("清除固定失败，请查看弹窗提示。", Brushes.Firebrick);
                MessageBox.Show(this, failureMessage, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var successMessage = clearedCount == 1
                ? "已清除 1 个通过 WinTiles 固定的磁贴。"
                : $"已清除 {clearedCount} 个通过 WinTiles 固定的磁贴。";

            var combinedWarnings = CombineWarnings(warningMessages.Concat(failureMessages).ToArray());
            if (!string.IsNullOrWhiteSpace(combinedWarnings))
            {
                SetStatus(successMessage, Brushes.DarkGoldenrod);
                MessageBox.Show(this, $"{successMessage}\n\n{combinedWarnings}", "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                SetStatus(successMessage, Brushes.SeaGreen);
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

    private async Task DeleteSelectedHistoryAsync()
    {
        if (_selectedHistoryItem is null)
        {
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

            var deleteResult = await TryDeletePinnedTileAsync(_selectedHistoryItem.TileRecord).ConfigureAwait(true);
            await RefreshHistoryAsync().ConfigureAwait(true);

            if (!deleteResult.Deleted)
            {
                SetStatus("删除历史失败，请查看弹窗提示。", Brushes.Firebrick);
                MessageBox.Show(
                    this,
                    deleteResult.FailureMessage ?? "删除历史失败，请查看弹窗提示。",
                    "WinTiles",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var successMessage = "已删除 1 条固定历史。";
            if (!string.IsNullOrWhiteSpace(deleteResult.WarningMessage))
            {
                SetStatus(successMessage, Brushes.DarkGoldenrod);
                MessageBox.Show(this, $"{successMessage}\n\n{deleteResult.WarningMessage}", "WinTiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                SetStatus(successMessage, Brushes.SeaGreen);
            }
        }
        catch (Exception exception)
        {
            SetStatus("删除历史失败，请查看弹窗提示。", Brushes.Firebrick);
            MessageBox.Show(this, exception.Message, "WinTiles", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
            RefreshActionButtonsState();
        }
    }

    private async Task<(bool Deleted, string? WarningMessage, string? FailureMessage)> TryDeletePinnedTileAsync(TileRecord tileRecord)
    {
        // 先尝试从 Start.TileGrid 解除固定；如果只是本地记录残留，也允许继续清理目录和快捷方式。
        var unpinResult = await _applicationContext.PinHelperInvoker.UnpinImageAsync(
            _applicationContext.PinHelperPath,
            tileRecord.TileId).ConfigureAwait(true);

        if (unpinResult.Status == PinHelperResultStatus.Failure)
        {
            return (false, null, FormatTileMessage(tileRecord, unpinResult.Message));
        }

        var tileMessages = new List<string>();
        try
        {
            DeleteFileIfExists(tileRecord.ShortcutPath);
        }
        catch (Exception cleanupException)
        {
            tileMessages.Add($"删除快捷方式失败：{cleanupException.Message}");
        }

        try
        {
            _applicationContext.TileRecordStore.DeleteTileDirectory(tileRecord.TileId);
        }
        catch (Exception cleanupException)
        {
            tileMessages.Add($"删除本地记录失败：{cleanupException.Message}");
        }

        var warningMessages = new List<string>();
        var helperMessage = CombineWarnings(unpinResult.Message, unpinResult.Warning);
        if (!string.IsNullOrWhiteSpace(helperMessage) && unpinResult.Status == PinHelperResultStatus.Warning)
        {
            warningMessages.Add(FormatTileMessage(tileRecord, helperMessage));
        }

        var cleanupMessage = CombineWarnings(tileMessages.ToArray());
        if (!string.IsNullOrWhiteSpace(cleanupMessage))
        {
            warningMessages.Add(FormatTileMessage(tileRecord, cleanupMessage));
        }

        return (true, CombineWarnings(warningMessages.ToArray()), null);
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
        string tileId,
        string hostExePath,
        string appUserModelId,
        string shortcutIconPath,
        string manifestPath,
        string displayTitle)
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
            displayTitle,
            appUserModelId,
            shortcutIconPath,
            manifestPath);
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
        if (shellPinResult.Invoked && pinResult is null)
        {
            return new PinAttemptRecord
            {
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                RequestedSize = _viewModel.SelectedSize,
                Status = PinHelperResultStatus.Warning,
                Message = $"已触发系统固定命令，但没有自动固定为 {_viewModel.SelectedSize.ToDisplayText()}，请检查开始菜单中的实际结果。",
                PinMethod = $"{shellPinResult.TargetName}.{shellPinResult.VerbName}",
                Warning = null,
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
                message = "已触发系统固定命令，但磁贴尺寸刷新失败，请检查开始菜单中的实际结果。";
                warning = CombineWarnings(shellPinResult.ErrorMessage, pinResult.Message, pinResult.Warning);
            }

            return new PinAttemptRecord
            {
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                RequestedSize = _viewModel.SelectedSize,
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
            var status = pinResult.Status;
            var message = pinResult.Message;
            var warning = CombineWarnings(shellPinResult.ErrorMessage, pinResult.Warning);

            if (shellPinResult.Invoked && pinResult.Status == PinHelperResultStatus.Failure)
            {
                status = PinHelperResultStatus.Warning;
                message = "已触发系统固定命令，但磁贴尺寸刷新失败，请检查开始菜单中的实际结果。";
                warning = CombineWarnings(shellPinResult.ErrorMessage, pinResult.Message, pinResult.Warning);
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

        return new PinAttemptRecord
        {
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            RequestedSize = _viewModel.SelectedSize,
            Status = PinHelperResultStatus.Failure,
            Message = "未能触发系统固定命令。",
            PinMethod = "ShellVerbUnavailable",
            Warning = shellPinResult.ErrorMessage
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

        RefreshActionButtonsState();
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

    private void RefreshClearButtonState()
    {
        if (ClearPinButton is null)
        {
            return;
        }

        // 清除按钮是“当前软件固定的全部磁贴”入口，不依赖当前是否已选图片。
        ClearPinButton.IsEnabled =
            !_viewModel.IsBusy &&
            _viewModel.IsClassicStartAvailable &&
            _viewModel.AreToolsAvailable;
    }

    private void RefreshActionButtonsState()
    {
        RefreshPinButtonState();
        RefreshClearButtonState();
        _viewModel.CanDeleteSelectedHistory =
            !_viewModel.IsBusy &&
            _selectedHistoryItem is not null &&
            _viewModel.IsClassicStartAvailable &&
            _viewModel.AreToolsAvailable;
    }

    private void ResetDefaultTileSize()
    {
        // 现在前端不再给用户提供尺寸切换，内部请求统一回到默认 2x2。
        _viewModel.SelectedSize = TileRequestSize.Medium2x2;
    }

    private void HistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingHistory)
        {
            return;
        }

        // 历史列表选中后，右侧直接切到对应图片；如果只是取消选择，就保留当前图片预览。
        var selectedItem = HistoryListBox?.SelectedItem as TileHistoryItemViewModel;
        if (selectedItem is null)
        {
            _selectedHistoryItem = null;
            if (string.IsNullOrWhiteSpace(_selectedImagePath))
            {
                ShowPreviewPlaceholder();
            }
            else
            {
                RefreshActionButtonsState();
            }

            return;
        }

        ShowHistoryPreview(selectedItem);
    }

    private async Task RefreshHistoryAsync(string? preferredTileId = null)
    {
        // 把本地固定记录重新聚合成历史列表，刷新时尽量保留用户刚才正在看的那一条。
        var historyItems = await BuildHistoryItemsAsync().ConfigureAwait(true);
        var previousSelectedTileId = _selectedHistoryItem?.TileId;
        var hadHistorySelection = _selectedHistoryItem is not null;

        _isRefreshingHistory = true;
        try
        {
            _viewModel.HistoryItems.Clear();
            foreach (var historyItem in historyItems)
            {
                _viewModel.HistoryItems.Add(historyItem);
            }

            _viewModel.HasHistoryItems = historyItems.Count > 0;
            _viewModel.HistoryCountText = $"{historyItems.Count} 条";
        }
        finally
        {
            _isRefreshingHistory = false;
        }

        var selectedHistoryItem = !string.IsNullOrWhiteSpace(preferredTileId)
            ? historyItems.FirstOrDefault(item => string.Equals(item.TileId, preferredTileId, StringComparison.Ordinal))
            : !string.IsNullOrWhiteSpace(previousSelectedTileId)
                ? historyItems.FirstOrDefault(item => string.Equals(item.TileId, previousSelectedTileId, StringComparison.Ordinal))
                : null;

        if (HistoryListBox is not null)
        {
            if (selectedHistoryItem is not null)
            {
                HistoryListBox.SelectedItem = selectedHistoryItem;
            }
            else if (hadHistorySelection)
            {
                HistoryListBox.SelectedItem = null;
            }
        }

        if (selectedHistoryItem is null && hadHistorySelection)
        {
            ShowPreviewPlaceholder();
        }

        RefreshActionButtonsState();
    }

    private async Task<IReadOnlyList<TileHistoryItemViewModel>> BuildHistoryItemsAsync()
    {
        var tileRecords = await _applicationContext.TileRecordStore.LoadAllTileRecordsAsync().ConfigureAwait(true);
        var historyItems = await Task.WhenAll(tileRecords.Select(CreateHistoryItemAsync)).ConfigureAwait(true);

        return historyItems
            .OrderByDescending(item => item.SortTimestampUtc)
            .ThenByDescending(item => item.TileId, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<TileHistoryItemViewModel> CreateHistoryItemAsync(TileRecord tileRecord)
    {
        var pinAttempt = await _applicationContext.TileRecordStore.LoadPinAttemptAsync(tileRecord.TileId).ConfigureAwait(true);
        var displayTitle = ResolveHistoryDisplayTitle(tileRecord);
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
        var thumbnailImage = TryCreateBitmapImage(tileRecord.SourceImagePath, 180);

        return new TileHistoryItemViewModel
        {
            TileId = tileRecord.TileId,
            DisplayTitle = displayTitle,
            ThumbnailImage = thumbnailImage,
            HasThumbnailImage = thumbnailImage is not null,
            RequestedSizeText = tileRecord.RequestedSize.ToDisplayText(),
            AttemptedAtText = attemptedAtText,
            DetailText = detailText,
            DetailBrush = detailBrush,
            SortTimestampUtc = sortTimestampUtc,
            TileRecord = tileRecord,
            PinAttempt = pinAttempt
        };
    }

    private void ShowCurrentImagePreview(string imagePath)
    {
        // 这里是“当前待固定图片”的预览态，和历史预览互斥。
        _selectedHistoryItem = null;
        _viewModel.SelectedHistoryBadgeText = $"待固定 · {_viewModel.SelectedSize.ToDisplayText()}";
        _viewModel.PreviewTitle = Path.GetFileName(imagePath);
        _viewModel.StatusText = $"已选择图片：{Path.GetFileName(imagePath)}";
        _viewModel.StatusBrush = Brushes.DarkSlateBlue;

        try
        {
            _viewModel.PreviewImage = CreateBitmapImage(imagePath);
            _viewModel.HasPreviewImage = true;
        }
        catch (Exception exception)
        {
            _selectedImagePath = null;
            _viewModel.PreviewImage = null;
            _viewModel.HasPreviewImage = false;
            _viewModel.StatusText = $"加载图片失败：{exception.Message}";
            _viewModel.StatusBrush = Brushes.Firebrick;
        }

        RefreshActionButtonsState();
    }

    private void ShowHistoryPreview(TileHistoryItemViewModel historyItem)
    {
        // 右侧切到历史记录时，优先展示这条固定对应的原图和固定结果。
        _selectedHistoryItem = historyItem;
        _selectedImagePath = null;
        _viewModel.SelectedHistoryBadgeText = historyItem.RequestedSizeText;
        _viewModel.PreviewTitle = historyItem.DisplayTitle;

        try
        {
            _viewModel.PreviewImage = CreateBitmapImage(historyItem.TileRecord.SourceImagePath);
            _viewModel.HasPreviewImage = true;
        }
        catch (Exception exception)
        {
            _viewModel.PreviewImage = null;
            _viewModel.HasPreviewImage = false;
            _viewModel.StatusText = $"历史图片无法读取：{exception.Message}";
            _viewModel.StatusBrush = Brushes.DarkGoldenrod;
            RefreshActionButtonsState();
            return;
        }

        var statusText = $"{historyItem.AttemptedAtText} · {historyItem.DetailText}";
        _viewModel.StatusText = statusText;
        _viewModel.StatusBrush = historyItem.DetailBrush;
        RefreshActionButtonsState();
    }

    private void ShowPreviewPlaceholder()
    {
        // 没有当前图片也没有历史选中时，就回到提示态。
        _selectedHistoryItem = null;
        _viewModel.SelectedHistoryBadgeText = "历史预览";
        _viewModel.PreviewTitle = "尚未选择图片或历史固定";
        _viewModel.PreviewImage = null;
        _viewModel.HasPreviewImage = false;
        _viewModel.StatusText = "点击左侧历史条目，在右侧查看并删除。";
        _viewModel.StatusBrush = Brushes.DarkSlateBlue;
        RefreshActionButtonsState();
    }

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

    private string ResolveHistoryDisplayTitle(TileRecord tileRecord)
    {
        var shortcutTitle = _applicationContext.StartMenuShortcutService.TryReadTitle(tileRecord.ShortcutPath);
        if (!string.IsNullOrWhiteSpace(shortcutTitle))
        {
            return shortcutTitle;
        }

        return Path.GetFileNameWithoutExtension(tileRecord.ShortcutPath);
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

    private static string FormatTileMessage(TileRecord tileRecord, string message)
    {
        return $"{Path.GetFileNameWithoutExtension(tileRecord.ShortcutPath)}：{message}";
    }

    private static Brush MapStatusBrush(PinHelperResultStatus status) => status switch
    {
        PinHelperResultStatus.Success => Brushes.SeaGreen,
        PinHelperResultStatus.Warning => Brushes.DarkGoldenrod,
        _ => Brushes.Firebrick
    };
}
