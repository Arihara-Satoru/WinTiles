using Microsoft.Win32;
using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public sealed class ClassicStartDetector
{
    private const string AdvancedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public ClassicStartAvailability Detect()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AdvancedKeyPath, writable: false);
        var classicModeValue = key?.GetValue("Start_ShowClassicMode");
        var classicModeEnabled = Convert.ToInt32(classicModeValue ?? 0) == 1;

        if (!classicModeEnabled)
        {
            return new ClassicStartAvailability
            {
                IsAvailable = false,
                Message = "未检测到已启用的 ExplorerPatcher 经典开始菜单。"
            };
        }

        var startTileDataPath = Path.Combine(Environment.SystemDirectory, "StartTileData.dll");
        if (!File.Exists(startTileDataPath))
        {
            return new ClassicStartAvailability
            {
                IsAvailable = false,
                Message = "系统缺少 StartTileData.dll，无法继续固定图片。"
            };
        }

        return new ClassicStartAvailability
        {
            IsAvailable = true,
            Message = "已检测到 ExplorerPatcher 经典开始菜单。"
        };
    }
}
