namespace WinTiles.Core.Models;

/// <summary>
/// 表示图片磁贴点击动作的完整配置。
/// </summary>
public sealed class TileClickAction
{
    /// <summary>
    /// 动作类型。
    /// </summary>
    public TileClickActionType Type { get; init; }

    /// <summary>
    /// 网页动作对应的地址，仅支持 http/https。
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 应用动作对应的可执行文件或快捷方式路径。
    /// </summary>
    public string? ApplicationPath { get; init; }

    /// <summary>
    /// 应用启动参数。
    /// </summary>
    public string? Arguments { get; init; }

    /// <summary>
    /// 应用工作目录。
    /// </summary>
    public string? WorkingDirectory { get; init; }
}
