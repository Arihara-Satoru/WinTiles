namespace WinTiles;

public sealed class SilentUpdatePreparationResult
{
    public bool IsReadyToInstall { get; init; }

    public required UpdateReleaseInfo Release { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? PackagePath { get; init; }
}
