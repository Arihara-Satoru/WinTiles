namespace WinTiles.Core.Models;

public sealed class GeneratedAssetSet
{
    // 这里只记录当前资源批次版本，方便后续判断旧磁贴是否需要重新固定。
    public const string CurrentAssetsVersion = "2";

    public required string Square70x70LogoPath { get; init; }

    public required string Square150x150LogoPath { get; init; }

    public required string Wide310x150LogoPath { get; init; }

    public required string Square310x310LogoPath { get; init; }

    public required string ShortcutIconPath { get; init; }

    public string AssetsVersion => CurrentAssetsVersion;
}
