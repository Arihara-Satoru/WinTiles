using System.IO;
using System.Windows;
using WinTiles.Core.Services;

namespace WinTiles;

public partial class App : Application
{
    private const string MainAppDisplayName = "WinTiles";
    private const string MainAppUserModelId = "WinTiles.MainApp";

    private Mutex? _singleInstanceMutex;
    private SingleInstanceCoordinator? _singleInstanceCoordinator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var activationTileId = ParseTileId(e.Args);
        _singleInstanceMutex = new Mutex(true, "WinTiles.SingleInstance", out var isPrimaryInstance);
        if (!isPrimaryInstance)
        {
            // 这里必须留在 UI 线程继续执行 Shutdown，否则 WPF 会因为跨线程关闭而直接崩溃。
            await SingleInstanceCoordinator.SendActivationAsync(activationTileId);
            Shutdown();
            return;
        }

        var applicationContext = CreateApplicationContext();
        EnsureMainAppStartMenuShortcut(applicationContext);
        var mainWindow = new MainWindow(applicationContext);
        MainWindow = mainWindow;

        _singleInstanceCoordinator = new SingleInstanceCoordinator(
            "WinTiles.ActivationPipe",
            tileId => Dispatcher.InvokeAsync(() => mainWindow.HandleActivationAsync(tileId)).Task.Unwrap());

        _singleInstanceCoordinator.Start();
        mainWindow.Show();
        await mainWindow.InitializeAsync(activationTileId).ConfigureAwait(true);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceCoordinator?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static string? ParseTileId(IEnumerable<string> args)
    {
        var argumentList = args.ToList();
        for (var index = 0; index < argumentList.Count - 1; index++)
        {
            if (string.Equals(argumentList[index], "--tile-id", StringComparison.OrdinalIgnoreCase))
            {
                return argumentList[index + 1];
            }
        }

        return null;
    }

    private static WinTilesApplicationContext CreateApplicationContext()
    {
        var appBaseDirectory = AppContext.BaseDirectory;
        var dataRoot = Path.Combine(appBaseDirectory, "Data", "PinnedTiles");
        var toolsRoot = Path.Combine(appBaseDirectory, "Tools");

        return new WinTilesApplicationContext
        {
            ReleaseUpdateService = new GitHubReleaseUpdateService("Arihara-Satoru", "WinTiles"),
            MainExecutablePath = Environment.ProcessPath ?? Path.Combine(appBaseDirectory, "WinTiles.exe"),
            SilentUpdateService = new SilentUpdateService(
                appBaseDirectory,
                Environment.ProcessPath ?? Path.Combine(appBaseDirectory, "WinTiles.exe")),
            PinHelperPath = Path.Combine(toolsRoot, "WinTiles.PinHelper.exe"),
            TileHostTemplatePath = Path.Combine(toolsRoot, "TileHost.exe"),
            TileRecordStore = new TileRecordStore(dataRoot),
            ClassicStartDetector = new ClassicStartDetector(),
            CropLayoutCalculator = new CropLayoutCalculator(),
            ImageAssetGenerator = new ImageAssetGenerator(),
            VisualElementsManifestBuilder = new VisualElementsManifestBuilder(),
            TileHostConfigurationWriter = new TileHostConfigurationWriter(),
            StartMenuShortcutService = new StartMenuShortcutService(),
            StartMenuPinVerbInvoker = new StartMenuPinVerbInvoker(),
            PinHelperInvoker = new PinHelperInvoker()
        };
    }

    private static void EnsureMainAppStartMenuShortcut(WinTilesApplicationContext applicationContext)
    {
        try
        {
            var appBaseDirectory = AppContext.BaseDirectory;
            var startMenuShortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Start Menu",
                "Programs",
                $"{MainAppDisplayName}.lnk");
            var visualElementsManifestPath = Path.Combine(appBaseDirectory, "WinTiles.VisualElementsManifest.xml");

            // 主程序入口统一刷新为带磁贴提示的开始菜单快捷方式，
            // 这样用户固定 WinTiles 本体时，壳层更容易走桌面磁贴资源而不是回退成小图标。
            applicationContext.StartMenuShortcutService.CreateShortcut(
                startMenuShortcutPath,
                applicationContext.MainExecutablePath,
                arguments: string.Empty,
                workingDirectory: appBaseDirectory,
                description: MainAppDisplayName,
                appUserModelId: MainAppUserModelId,
                iconPath: applicationContext.MainExecutablePath,
                visualElementsManifestHintPath: File.Exists(visualElementsManifestPath) ? visualElementsManifestPath : null);
        }
        catch
        {
            // 入口修复失败时不要影响主程序启动，后续用户仍可先进入应用继续操作。
        }
    }
}
