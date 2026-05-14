namespace WinTiles.Core.Models;

public enum PinHelperResultStatus
{
    Success,
    Warning,
    Failure
}

public sealed class PinHelperResult
{
    public required PinHelperResultStatus Status { get; init; }

    public required string Message { get; init; }

    public required string PinMethod { get; init; }

    public string? Warning { get; init; }

    // 记录 helper 最终采用的身份来源，方便排查是 HostExe 生效还是 AppUserModelID 生效。
    public string? IdentityKind { get; init; }

    public string? IdentityValue { get; init; }

    public bool? ContainsBefore { get; init; }

    public bool? ContainsAfterCommit { get; init; }

    public bool? ContainsAfterReopen { get; init; }

    public string? RawOutput { get; init; }

    public string? RawError { get; init; }
}
