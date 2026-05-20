namespace WinTiles.Core.Models;

/// <summary>
/// 表示点击动作校验后的结果，供界面和保存流程复用。
/// </summary>
public sealed class TileClickActionValidationResult
{
    /// <summary>
    /// 当前动作配置是否有效。
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// 当前动作是否真正启用了跳转能力。
    /// </summary>
    public required bool HasConfiguredAction { get; init; }

    /// <summary>
    /// 展示给用户的摘要说明。
    /// </summary>
    public required string SummaryText { get; init; }

    /// <summary>
    /// 展示给用户的校验提示。
    /// </summary>
    public required string ValidationMessage { get; init; }
}
