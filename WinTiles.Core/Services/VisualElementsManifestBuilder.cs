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
        builder.AppendLine($"    Square71x71Logo=\"{assetDirectoryName}\\Square71x71Logo.png\"");
        builder.AppendLine($"    Square150x150Logo=\"{assetDirectoryName}\\Square150x150Logo.png\"");
        builder.AppendLine($"    Wide310x150Logo=\"{assetDirectoryName}\\Wide310x150Logo.png\"");
        builder.AppendLine($"    Square310x310Logo=\"{assetDirectoryName}\\Square310x310Logo.png\" />");
        builder.AppendLine("</Application>");
        return builder.ToString();
    }
}
