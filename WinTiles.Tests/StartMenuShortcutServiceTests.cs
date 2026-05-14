using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class StartMenuShortcutServiceTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), "WinTiles.Shortcuts", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateShortcut_writes_app_user_model_id()
    {
        Directory.CreateDirectory(_workingDirectory);
        var shortcutPath = Path.Combine(_workingDirectory, "sample.lnk");
        var service = new StartMenuShortcutService();

        service.CreateShortcut(
            shortcutPath,
            Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            string.Empty,
            Environment.SystemDirectory,
            "WinTiles 测试快捷方式",
            "WinTiles.Image.test");

        Assert.True(File.Exists(shortcutPath));
        Assert.Equal("WinTiles.Image.test", service.TryReadAppUserModelId(shortcutPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
