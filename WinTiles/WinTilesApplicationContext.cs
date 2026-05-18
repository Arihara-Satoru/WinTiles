using WinTiles.Core.Services;

namespace WinTiles;

public sealed class WinTilesApplicationContext
{
    public required GitHubReleaseUpdateService ReleaseUpdateService { get; init; }

    public required SilentUpdateService SilentUpdateService { get; init; }

    public required string MainExecutablePath { get; init; }

    public required string PinHelperPath { get; init; }

    public required string TileHostTemplatePath { get; init; }

    public required TileRecordStore TileRecordStore { get; init; }

    public required ClassicStartDetector ClassicStartDetector { get; init; }

    public required CropLayoutCalculator CropLayoutCalculator { get; init; }

    public required ImageAssetGenerator ImageAssetGenerator { get; init; }

    public required VisualElementsManifestBuilder VisualElementsManifestBuilder { get; init; }

    public required TileHostConfigurationWriter TileHostConfigurationWriter { get; init; }

    public required StartMenuShortcutService StartMenuShortcutService { get; init; }

    public required StartMenuPinVerbInvoker StartMenuPinVerbInvoker { get; init; }

    public required PinHelperInvoker PinHelperInvoker { get; init; }
}
