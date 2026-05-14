namespace WinTiles.Core.Models;

public sealed class GeneratedAssetSet
{
    public const string CurrentAssetsVersion = "1";

    public required string Square71x71LogoPath { get; init; }

    public required string Square150x150LogoPath { get; init; }

    public required string Wide310x150LogoPath { get; init; }

    public required string Square310x310LogoPath { get; init; }

    public required string ShortcutIconPath { get; init; }

    public string AssetsVersion => CurrentAssetsVersion;
}
