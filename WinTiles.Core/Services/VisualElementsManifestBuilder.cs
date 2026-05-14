using System.Text;

namespace WinTiles.Core.Services;

public sealed class VisualElementsManifestBuilder
{
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
        // 当前这条 Win32 + ExplorerPatcher 经典开始菜单链路，实际接受的是这里这种直接挂在 VisualElements 根节点上的写法。
        // 上一轮改成 DefaultTile 之后，2x2 会回退，4x2 甚至出现“提示成功但没有固定上去”的回归。
        builder.AppendLine($"    Square70x70Logo=\"{assetDirectoryName}\\Square70x70Logo.png\"");
        builder.AppendLine($"    Square150x150Logo=\"{assetDirectoryName}\\Square150x150Logo.png\"");
        builder.AppendLine($"    Wide310x150Logo=\"{assetDirectoryName}\\Wide310x150Logo.png\"");
        builder.AppendLine($"    Square310x310Logo=\"{assetDirectoryName}\\Square310x310Logo.png\" />");
        builder.AppendLine("</Application>");
        return builder.ToString();
    }
}
