namespace WinTiles.Core.Models;

public sealed class PinAttemptRecord
{
    public required DateTimeOffset AttemptedAtUtc { get; init; }

    public required TileRequestSize RequestedSize { get; init; }

    public required PinHelperResultStatus Status { get; init; }

    public required string Message { get; init; }

    public required string PinMethod { get; init; }

    public string? Warning { get; init; }

    public string? IdentityKind { get; init; }

    public string? IdentityValue { get; init; }

    public bool? ContainsBefore { get; init; }

    public bool? ContainsAfterCommit { get; init; }

    public bool? ContainsAfterReopen { get; init; }
}
