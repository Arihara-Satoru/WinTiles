using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

/// <summary>
/// 负责磁贴点击动作的校验、摘要生成和基础规范化。
/// </summary>
public static class TileClickActionService
{
    /// <summary>
    /// 校验点击动作，并返回界面展示所需的摘要和提示文本。
    /// </summary>
    /// <param name="action">待校验的动作配置。</param>
    /// <returns>包含是否有效、摘要和提示的结果。</returns>
    public static TileClickActionValidationResult Validate(TileClickAction? action)
    {
        if (action is null || action.Type == TileClickActionType.None)
        {
            return new TileClickActionValidationResult
            {
                IsValid = true,
                HasConfiguredAction = false,
                SummaryText = "点击后：打开 WinTiles 并定位到对应磁贴记录",
                ValidationMessage = "当前未设置点击动作，点击磁贴时会回到 WinTiles 历史记录。"
            };
        }

        if (action.Type == TileClickActionType.OpenUrl)
        {
            var urlText = action.Url?.Trim();
            if (string.IsNullOrWhiteSpace(urlText))
            {
                return new TileClickActionValidationResult
                {
                    IsValid = false,
                    HasConfiguredAction = true,
                    SummaryText = "点击后：打开网页",
                    ValidationMessage = "请输入网页地址，且必须以 http:// 或 https:// 开头。"
                };
            }

            if (!IsSupportedUrl(urlText))
            {
                return new TileClickActionValidationResult
                {
                    IsValid = false,
                    HasConfiguredAction = true,
                    SummaryText = $"点击后：打开网页 {urlText}",
                    ValidationMessage = "网页地址无效，首版仅支持 http:// 或 https:// 链接。"
                };
            }

            return new TileClickActionValidationResult
            {
                IsValid = true,
                HasConfiguredAction = true,
                SummaryText = $"点击后：打开网页 {urlText}",
                ValidationMessage = "网页地址有效，点击磁贴时会使用系统默认浏览器打开。"
            };
        }

        if (action.Type == TileClickActionType.OpenApplication)
        {
            var applicationPath = action.ApplicationPath?.Trim();
            if (string.IsNullOrWhiteSpace(applicationPath))
            {
                return new TileClickActionValidationResult
                {
                    IsValid = false,
                    HasConfiguredAction = true,
                    SummaryText = "点击后：打开应用",
                    ValidationMessage = "请选择要启动的应用路径。"
                };
            }

            if (!File.Exists(applicationPath))
            {
                return new TileClickActionValidationResult
                {
                    IsValid = false,
                    HasConfiguredAction = true,
                    SummaryText = $"点击后：启动 {Path.GetFileNameWithoutExtension(applicationPath)}",
                    ValidationMessage = "应用路径不存在，请重新选择有效的 .exe 或 .lnk 文件。"
                };
            }

            if (!IsSupportedApplicationPath(applicationPath))
            {
                return new TileClickActionValidationResult
                {
                    IsValid = false,
                    HasConfiguredAction = true,
                    SummaryText = $"点击后：启动 {Path.GetFileNameWithoutExtension(applicationPath)}",
                    ValidationMessage = "当前仅支持 .exe 或 .lnk 文件作为应用入口。"
                };
            }

            var workingDirectory = action.WorkingDirectory?.Trim();
            if (!string.IsNullOrWhiteSpace(workingDirectory) && !Directory.Exists(workingDirectory))
            {
                return new TileClickActionValidationResult
                {
                    IsValid = false,
                    HasConfiguredAction = true,
                    SummaryText = $"点击后：启动 {Path.GetFileNameWithoutExtension(applicationPath)}",
                    ValidationMessage = "工作目录不存在，请修改后再固定。"
                };
            }

            return new TileClickActionValidationResult
            {
                IsValid = true,
                HasConfiguredAction = true,
                SummaryText = $"点击后：启动 {Path.GetFileNameWithoutExtension(applicationPath)}",
                ValidationMessage = "应用配置有效，点击磁贴时会直接启动该应用。"
            };
        }

        return new TileClickActionValidationResult
        {
            IsValid = false,
            HasConfiguredAction = true,
            SummaryText = "点击后：执行未知动作",
            ValidationMessage = "当前动作类型无法识别，请重新选择。"
        };
    }

    /// <summary>
    /// 对动作配置做轻量规范化，避免把纯空白内容写入记录。
    /// </summary>
    /// <param name="action">待规范化的动作配置。</param>
    /// <returns>规范化后的动作；若等价于未配置，则返回 null。</returns>
    public static TileClickAction? Normalize(TileClickAction? action)
    {
        if (action is null || action.Type == TileClickActionType.None)
        {
            return null;
        }

        return action.Type switch
        {
            TileClickActionType.OpenUrl => new TileClickAction
            {
                Type = TileClickActionType.OpenUrl,
                Url = NormalizeNullableText(action.Url)
            },
            TileClickActionType.OpenApplication => new TileClickAction
            {
                Type = TileClickActionType.OpenApplication,
                ApplicationPath = NormalizeNullableText(action.ApplicationPath),
                Arguments = NormalizeNullableText(action.Arguments),
                WorkingDirectory = NormalizeNullableText(action.WorkingDirectory)
            },
            _ => null
        };
    }

    /// <summary>
    /// 判断链接是否为首版允许的 http/https 地址。
    /// </summary>
    /// <param name="urlText">待校验的链接文本。</param>
    /// <returns>若是支持的链接则返回 true。</returns>
    public static bool IsSupportedUrl(string urlText)
    {
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// 判断应用路径是否为当前首版允许的启动目标。
    /// </summary>
    /// <param name="applicationPath">待校验的应用路径。</param>
    /// <returns>若是允许的扩展名则返回 true。</returns>
    public static bool IsSupportedApplicationPath(string applicationPath)
    {
        var extension = Path.GetExtension(applicationPath);
        return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeNullableText(string? text)
    {
        var trimmedText = text?.Trim();
        return string.IsNullOrWhiteSpace(trimmedText) ? null : trimmedText;
    }
}
