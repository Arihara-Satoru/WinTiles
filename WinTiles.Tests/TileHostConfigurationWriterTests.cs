using System.Text;
using WinTiles.Core.Models;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class TileHostConfigurationWriterTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), "WinTiles.TileHostConfig", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_generates_utf16_ini_for_win32_profile_api()
    {
        var writer = new TileHostConfigurationWriter();
        var tileDirectory = Path.Combine(_workingDirectory, "tile-a");
        var clickAction = new TileClickAction
        {
            Type = TileClickActionType.OpenApplication,
            ApplicationPath = @"D:\Apps\Notepad.exe",
            Arguments = "--example",
            WorkingDirectory = @"D:\Apps"
        };

        var configPath = writer.Write(tileDirectory, @"D:\Apps\WinTiles.exe", "tile-a", clickAction);

        Assert.True(File.Exists(configPath));

        var fileBytes = File.ReadAllBytes(configPath);
        Assert.True(fileBytes.Length >= 2);
        Assert.Equal(0xFF, fileBytes[0]);
        Assert.Equal(0xFE, fileBytes[1]);

        var fileContent = Encoding.Unicode.GetString(fileBytes);
        Assert.Contains("[TileHost]", fileContent, StringComparison.Ordinal);
        Assert.Contains("MainExecutable=D:\\Apps\\WinTiles.exe", fileContent, StringComparison.Ordinal);
        Assert.Contains("TileId=tile-a", fileContent, StringComparison.Ordinal);
        Assert.Contains("ClickActionType=2", fileContent, StringComparison.Ordinal);
        Assert.Contains("ClickActionApplicationPath=D:\\Apps\\Notepad.exe", fileContent, StringComparison.Ordinal);
        Assert.Contains("ClickActionArguments=--example", fileContent, StringComparison.Ordinal);
        Assert.Contains("ClickActionWorkingDirectory=D:\\Apps", fileContent, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
