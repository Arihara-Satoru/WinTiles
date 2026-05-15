using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WinTiles;

public sealed class TileBatchHistoryItemViewModel : ViewModelBase
{
    private bool _isExpanded;

    public required string BatchId { get; init; }

    public required string DisplayTitle { get; init; }

    public required ImageSource? ThumbnailImage { get; init; }

    public required bool HasThumbnailImage { get; init; }

    public required string AttemptedAtText { get; init; }

    public required string SummaryText { get; init; }

    public required Brush SummaryBrush { get; init; }

    public required string TileCountText { get; init; }

    public required int SuccessCount { get; init; }

    public required int FailureCount { get; init; }

    public required string SourceImagePath { get; init; }

    public ObservableCollection<TileHistoryItemViewModel> Tiles { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }
}
