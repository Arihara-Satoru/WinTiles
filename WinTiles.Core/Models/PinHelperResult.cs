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

    // 记录 helper 最终采用的固定身份，方便排查开始菜单究竟按哪条路径识别了这个磁贴。
    public string? IdentityKind { get; init; }

    public string? IdentityValue { get; init; }

    public bool? ContainsBefore { get; init; }

    public bool? ContainsAfterCommit { get; init; }

    public bool? ContainsAfterReopen { get; init; }

    public string? RawOutput { get; init; }

    public string? RawError { get; init; }
}
