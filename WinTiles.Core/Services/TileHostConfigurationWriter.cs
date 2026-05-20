using System.Text;
using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public sealed class TileHostConfigurationWriter
{
    /// <summary>
    /// 生成 TileHost 使用的配置文件，并使用 Win32 INI API 兼容的 UTF-16 LE 编码写入。
    /// </summary>
    /// <param name="tileDirectory">当前磁贴对应的数据目录。</param>
    /// <param name="mainExecutablePath">主程序可执行文件路径。</param>
    /// <param name="tileId">当前磁贴的唯一标识。</param>
    /// <param name="clickAction">当前磁贴绑定的点击动作；为空时表示继续回退到 WinTiles 主程序。</param>
    /// <returns>生成后的配置文件完整路径。</returns>
    public string Write(string tileDirectory, string mainExecutablePath, string tileId, TileClickAction? clickAction)
    {
        Directory.CreateDirectory(tileDirectory);
        var configPath = Path.Combine(tileDirectory, "TileHost.ini");
        var normalizedAction = TileClickActionService.Normalize(clickAction);
        var content = new StringBuilder()
            .AppendLine("[TileHost]")
            .AppendLine($"MainExecutable={mainExecutablePath}")
            .AppendLine($"TileId={tileId}")
            .AppendLine($"ClickActionType={(int)(normalizedAction?.Type ?? TileClickActionType.None)}")
            .AppendLine($"ClickActionUrl={normalizedAction?.Url ?? string.Empty}")
            .AppendLine($"ClickActionApplicationPath={normalizedAction?.ApplicationPath ?? string.Empty}")
            .AppendLine($"ClickActionArguments={normalizedAction?.Arguments ?? string.Empty}")
            .AppendLine($"ClickActionWorkingDirectory={normalizedAction?.WorkingDirectory ?? string.Empty}")
            .ToString();

        // GetPrivateProfileStringW 对 UTF-8 ini 的兼容性并不稳定，
        // 这里统一改成带 BOM 的 UTF-16 LE，保证原生宿主能稳定读到配置项。
        File.WriteAllText(configPath, content, Encoding.Unicode);
        return configPath;
    }
}
