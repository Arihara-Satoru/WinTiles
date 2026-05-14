using System.IO;
using System.Windows;
using WinTiles.Core.Services;

namespace WinTiles;

public partial class App : Application
{
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
            AppBaseDirectory = appBaseDirectory,
            MainExecutablePath = Environment.ProcessPath ?? Path.Combine(appBaseDirectory, "WinTiles.exe"),
            PinHelperPath = Path.Combine(toolsRoot, "WinTiles.PinHelper.exe"),
            TileHostTemplatePath = Path.Combine(toolsRoot, "TileHost.exe"),
            TileRecordStore = new TileRecordStore(dataRoot),
            ClassicStartDetector = new ClassicStartDetector(),
            ImageAssetGenerator = new ImageAssetGenerator(),
            VisualElementsManifestBuilder = new VisualElementsManifestBuilder(),
            TileHostConfigurationWriter = new TileHostConfigurationWriter(),
            StartMenuShortcutService = new StartMenuShortcutService(),
            PinHelperInvoker = new PinHelperInvoker()
        };
    }
}
