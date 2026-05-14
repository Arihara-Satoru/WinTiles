using System.Text;

namespace WinTiles.Core.Services;

public sealed class TileHostConfigurationWriter
{
    public string Write(string tileDirectory, string mainExecutablePath, string tileId)
    {
        Directory.CreateDirectory(tileDirectory);
        var configPath = Path.Combine(tileDirectory, "TileHost.ini");
        var content = new StringBuilder()
            .AppendLine("[TileHost]")
            .AppendLine($"MainExecutable={mainExecutablePath}")
            .AppendLine($"TileId={tileId}")
            .ToString();
        File.WriteAllText(configPath, content, Encoding.UTF8);
        return configPath;
    }
}
