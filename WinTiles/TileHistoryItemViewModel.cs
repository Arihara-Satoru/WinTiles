using System.Windows.Media;
using WinTiles.Core.Models;

namespace WinTiles;

public sealed class TileHistoryItemViewModel
{
    public required string TileId { get; init; }

    public required string DisplayTitle { get; init; }

    public required ImageSource? ThumbnailImage { get; init; }

    public required bool HasThumbnailImage { get; init; }

    public required string RequestedSizeText { get; init; }

    public required string AttemptedAtText { get; init; }

    public required string DetailText { get; init; }

    public required Brush DetailBrush { get; init; }

    public required DateTimeOffset SortTimestampUtc { get; init; }

    public required TileRecord TileRecord { get; init; }

    public PinAttemptRecord? PinAttempt { get; init; }
}
