namespace WinTiles;

public sealed class UpdateCheckResult
{
    public required Version CurrentVersion { get; init; }

    public required string CurrentVersionText { get; init; }

    public bool IsUpdateAvailable { get; init; }

    public UpdateReleaseInfo? Release { get; init; }

    public string SummaryText { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }
}
