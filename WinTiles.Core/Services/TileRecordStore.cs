using System.Text.Json;
using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public sealed class TileRecordStore
{
    private const string TileRecordFileName = "tile-record.json";
    private const string PinAttemptFileName = "pin-attempt.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public TileRecordStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    public string CreateTileDirectory(string tileId)
    {
        var tileDirectory = GetTileDirectory(tileId);
        Directory.CreateDirectory(tileDirectory);
        return tileDirectory;
    }

    public string GetTileDirectory(string tileId) => Path.Combine(RootDirectory, tileId);

    public async Task SaveTileRecordAsync(TileRecord record, CancellationToken cancellationToken = default)
    {
        var tileDirectory = CreateTileDirectory(record.TileId);
        var recordPath = Path.Combine(tileDirectory, TileRecordFileName);
        await using var stream = File.Create(recordPath);
        await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TileRecord?> LoadTileRecordAsync(string tileId, CancellationToken cancellationToken = default)
    {
        var recordPath = Path.Combine(GetTileDirectory(tileId), TileRecordFileName);
        if (!File.Exists(recordPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(recordPath);
        return await JsonSerializer.DeserializeAsync<TileRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePinAttemptAsync(string tileId, PinAttemptRecord record, CancellationToken cancellationToken = default)
    {
        var tileDirectory = CreateTileDirectory(tileId);
        var attemptPath = Path.Combine(tileDirectory, PinAttemptFileName);
        await using var stream = File.Create(attemptPath);
        await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PinAttemptRecord?> LoadPinAttemptAsync(string tileId, CancellationToken cancellationToken = default)
    {
        var attemptPath = Path.Combine(GetTileDirectory(tileId), PinAttemptFileName);
        if (!File.Exists(attemptPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(attemptPath);
        return await JsonSerializer.DeserializeAsync<PinAttemptRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
