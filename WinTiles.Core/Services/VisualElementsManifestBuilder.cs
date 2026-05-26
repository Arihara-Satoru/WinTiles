using System.Text;

namespace WinTiles.Core.Services;

public sealed class VisualElementsManifestBuilder
{
    /// <summary>
    /// 生成桌面快捷方式磁贴使用的 VisualElements 清单。
    /// </summary>
    /// <param name="assetDirectoryName">磁贴资源所在目录名称，默认使用当前磁贴目录下的 Assets。</param>
    /// <returns>供快捷方式属性引用的完整清单 XML 文本。</returns>
    public string Build(string assetDirectoryName = "Assets")
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        builder.AppendLine("<Application xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        builder.AppendLine("  <VisualElements");
        builder.AppendLine("    BackgroundColor=\"#FFFFFF\"");
        builder.AppendLine("    ForegroundText=\"light\"");
        builder.AppendLine("    ShowNameOnSquare150x150Logo=\"off\"");
        builder.AppendLine("    ShowNameOnWide310x150Logo=\"off\"");
        // 根级属性继续保住 2x2 的稳定显示，DefaultTile 只补 4x2 需要的宽图提示。
        builder.AppendLine($"    Square70x70Logo=\"{assetDirectoryName}\\Square70x70Logo.png\"");
        builder.AppendLine($"    Square150x150Logo=\"{assetDirectoryName}\\Square150x150Logo.png\"");
        builder.AppendLine($"    Wide310x150Logo=\"{assetDirectoryName}\\Wide310x150Logo.png\"");
        builder.AppendLine($"    Square310x310Logo=\"{assetDirectoryName}\\Square310x310Logo.png\">");
        // 默认尺寸继续保持 2x2，避免开始菜单在首次解析 Win32 快捷方式磁贴时
        // 把宽资源当成不兼容配置，退回成“左侧方图 + 右侧纯背景”的异常宽磁贴。
        builder.AppendLine("    <DefaultTile DefaultSize=\"square150x150Logo\"");
        builder.AppendLine($"      Square70x70Logo=\"{assetDirectoryName}\\Square70x70Logo.png\"");
        builder.AppendLine($"      Wide310x150Logo=\"{assetDirectoryName}\\Wide310x150Logo.png\"");
        builder.AppendLine($"      Square310x310Logo=\"{assetDirectoryName}\\Square310x310Logo.png\" />");
        builder.AppendLine("  </VisualElements>");
        builder.AppendLine("</Application>");
        return builder.ToString();
    }
}
