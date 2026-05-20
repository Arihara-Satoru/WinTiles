using WinTiles.Core.Models;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class TileRecordStoreTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), "WinTiles.TileStore", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveTileBatchRecordAsync_round_trips_batch_record()
    {
        var store = new TileRecordStore(_workingDirectory);
        var batchRecord = new TileBatchRecord
        {
            BatchId = "batch-a",
            Title = "测试批次",
            SourceImagePath = @"D:\Images\sample.png",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TileIds = ["tile-1", "tile-2"]
        };

        await store.SaveTileBatchRecordAsync(batchRecord);
        var loadedRecord = await store.LoadTileBatchRecordAsync(batchRecord.BatchId);

        Assert.NotNull(loadedRecord);
        Assert.Equal(batchRecord.BatchId, loadedRecord.BatchId);
        Assert.Equal(batchRecord.Title, loadedRecord.Title);
        Assert.Equal(batchRecord.SourceImagePath, loadedRecord.SourceImagePath);
        Assert.Equal(batchRecord.TileIds, loadedRecord.TileIds);
    }

    [Fact]
    public async Task LoadAllTileRecordsAsync_ignores_batches_directory()
    {
        var store = new TileRecordStore(_workingDirectory);
        var tileRecord = new TileRecord
        {
            TileId = "tile-a",
            SourceImagePath = @"D:\Images\sample.png",
            BatchId = "batch-a",
            TileIndex = 0,
            GridRow = 0,
            GridColumn = 0,
            PreviewImagePath = @"D:\Images\preview.png",
            RequestedSize = TileRequestSize.Medium2x2,
            HostExePath = @"D:\Tiles\tile-a\TileHost.exe",
            ShortcutPath = @"D:\Tiles\tile-a\sample.lnk",
            AssetsVersion = GeneratedAssetSet.CurrentAssetsVersion
        };

        await store.SaveTileRecordAsync(tileRecord);
        await store.SaveTileBatchRecordAsync(new TileBatchRecord
        {
            BatchId = "batch-a",
            Title = "测试批次",
            SourceImagePath = tileRecord.SourceImagePath,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TileIds = [tileRecord.TileId]
        });

        var loadedRecords = await store.LoadAllTileRecordsAsync();

        Assert.Single(loadedRecords);
        Assert.Equal(tileRecord.TileId, loadedRecords[0].TileId);
    }

    [Fact]
    public async Task LoadTileRecordAsync_old_record_without_click_action_is_backward_compatible()
    {
        var store = new TileRecordStore(_workingDirectory);
        var tileDirectory = store.CreateTileDirectory("legacy-tile");
        var recordPath = Path.Combine(tileDirectory, "tile-record.json");

        await File.WriteAllTextAsync(
            recordPath,
            """
            {
              "TileId": "legacy-tile",
              "SourceImagePath": "D:\\Images\\legacy.png",
              "BatchId": "legacy-batch",
              "TileIndex": 0,
              "GridRow": 0,
              "GridColumn": 0,
              "PreviewImagePath": "D:\\Images\\preview.png",
              "RequestedSize": 1,
              "HostExePath": "D:\\Tiles\\legacy\\TileHost.exe",
              "ShortcutPath": "D:\\Tiles\\legacy\\tile.lnk",
              "AssetsVersion": "1"
            }
            """);

        var loadedRecord = await store.LoadTileRecordAsync("legacy-tile");

        Assert.NotNull(loadedRecord);
        Assert.Null(loadedRecord.ClickAction);
    }

    [Fact]
    public async Task SaveTileBatchRecordAsync_round_trips_default_click_action()
    {
        var store = new TileRecordStore(_workingDirectory);
        var batchRecord = new TileBatchRecord
        {
            BatchId = "batch-click-action",
            Title = "测试批次",
            SourceImagePath = @"D:\Images\sample.png",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TileIds = ["tile-1", "tile-2"],
            DefaultClickAction = new TileClickAction
            {
                Type = TileClickActionType.OpenUrl,
                Url = "https://example.com"
            }
        };

        await store.SaveTileBatchRecordAsync(batchRecord);
        var loadedRecord = await store.LoadTileBatchRecordAsync(batchRecord.BatchId);

        Assert.NotNull(loadedRecord);
        Assert.NotNull(loadedRecord.DefaultClickAction);
        Assert.Equal(TileClickActionType.OpenUrl, loadedRecord.DefaultClickAction.Type);
        Assert.Equal("https://example.com", loadedRecord.DefaultClickAction.Url);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
