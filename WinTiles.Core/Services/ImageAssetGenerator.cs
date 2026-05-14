using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using WinTiles.Core.Models;

namespace WinTiles.Core.Services;

public sealed class ImageAssetGenerator
{
    public GeneratedAssetSet GenerateAssets(string sourceImagePath, string assetsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        if (!File.Exists(sourceImagePath))
        {
            throw new FileNotFoundException("源图片不存在。", sourceImagePath);
        }

        Directory.CreateDirectory(assetsDirectory);

        using var sourceImage = Image.FromFile(sourceImagePath);
        var square70Path = Path.Combine(assetsDirectory, "Square70x70Logo.png");
        var square150Path = Path.Combine(assetsDirectory, "Square150x150Logo.png");
        var wide310x150Path = Path.Combine(assetsDirectory, "Wide310x150Logo.png");
        var square310Path = Path.Combine(assetsDirectory, "Square310x310Logo.png");
        var shortcutIconPath = Path.Combine(assetsDirectory, "ShortcutIcon.ico");

        SaveResizedImage(sourceImage, square70Path, 70, 70);
        SaveResizedImage(sourceImage, square150Path, 150, 150);
        SaveResizedImage(sourceImage, wide310x150Path, 310, 150);
        SaveResizedImage(sourceImage, square310Path, 310, 310);
        SavePngBackedIcon(sourceImage, shortcutIconPath, 256);

        return new GeneratedAssetSet
        {
            Square70x70LogoPath = square70Path,
            Square150x150LogoPath = square150Path,
            Wide310x150LogoPath = wide310x150Path,
            Square310x310LogoPath = square310Path,
            ShortcutIconPath = shortcutIconPath
        };
    }

    // 这里统一采用“居中裁剪 + 高质量缩放”，让任意图片都能平整铺满磁贴。
    private static void SaveResizedImage(Image sourceImage, string destinationPath, int targetWidth, int targetHeight)
    {
        using var bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        var sourceRectangle = CalculateCoverCrop(sourceImage.Width, sourceImage.Height, targetWidth, targetHeight);
        graphics.DrawImage(
            sourceImage,
            new Rectangle(0, 0, targetWidth, targetHeight),
            sourceRectangle,
            GraphicsUnit.Pixel);
        bitmap.Save(destinationPath, ImageFormat.Png);
    }

    // 这里输出一个 PNG 承载的 .ico，让壳层即使回退到快捷方式图标，也能直接显示用户图片。
    private static void SavePngBackedIcon(Image sourceImage, string destinationPath, int iconSize)
    {
        var pngBytes = RenderPngBytes(sourceImage, iconSize, iconSize);
        using var stream = File.Create(destinationPath);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0); // ICONDIR reserved
        writer.Write((ushort)1); // ICONDIR type = icon
        writer.Write((ushort)1); // count
        writer.Write((byte)0);   // width 0 == 256
        writer.Write((byte)0);   // height 0 == 256
        writer.Write((byte)0);   // color count
        writer.Write((byte)0);   // reserved
        writer.Write((ushort)1); // planes
        writer.Write((ushort)32); // bpp
        writer.Write((uint)pngBytes.Length);
        writer.Write((uint)22); // 6-byte ICONDIR + 16-byte ICONDIRENTRY
        writer.Write(pngBytes);
    }

    private static byte[] RenderPngBytes(Image sourceImage, int targetWidth, int targetHeight)
    {
        using var bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        var sourceRectangle = CalculateCoverCrop(sourceImage.Width, sourceImage.Height, targetWidth, targetHeight);
        graphics.DrawImage(
            sourceImage,
            new Rectangle(0, 0, targetWidth, targetHeight),
            sourceRectangle,
            GraphicsUnit.Pixel);

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        return memoryStream.ToArray();
    }

    private static RectangleF CalculateCoverCrop(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        var sourceAspect = (float)sourceWidth / sourceHeight;
        var targetAspect = (float)targetWidth / targetHeight;

        if (sourceAspect > targetAspect)
        {
            var croppedWidth = sourceHeight * targetAspect;
            var offsetX = (sourceWidth - croppedWidth) / 2f;
            return new RectangleF(offsetX, 0f, croppedWidth, sourceHeight);
        }

        var croppedHeight = sourceWidth / targetAspect;
        var offsetY = (sourceHeight - croppedHeight) / 2f;
        return new RectangleF(0f, offsetY, sourceWidth, croppedHeight);
    }
}
