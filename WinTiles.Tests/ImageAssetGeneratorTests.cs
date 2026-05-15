using System.Drawing;
using WinTiles.Core.Services;

namespace WinTiles.Tests;

public sealed class ImageAssetGeneratorTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), "WinTiles.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GenerateAssets_creates_expected_png_files_with_expected_sizes()
    {
        Directory.CreateDirectory(_workingDirectory);
        var sourcePath = Path.Combine(_workingDirectory, "source.png");

        using (var bitmap = new Bitmap(640, 480))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.CornflowerBlue);
            bitmap.Save(sourcePath);
        }

        var generator = new ImageAssetGenerator();
        var generatedAssets = generator.GenerateAssets(sourcePath, Path.Combine(_workingDirectory, "Assets"));

        AssertImageSize(generatedAssets.Square70x70LogoPath, 70, 70);
        AssertImageSize(generatedAssets.Square150x150LogoPath, 150, 150);
        AssertImageSize(generatedAssets.Wide310x150LogoPath, 310, 150);
        AssertImageSize(generatedAssets.Square310x310LogoPath, 310, 310);
        Assert.True(File.Exists(generatedAssets.ShortcutIconPath));
        Assert.True(new FileInfo(generatedAssets.ShortcutIconPath).Length > 0);
    }

    [Fact]
    public void GenerateAssets_with_explicit_crop_rectangle_uses_requested_source_region()
    {
        Directory.CreateDirectory(_workingDirectory);
        var sourcePath = Path.Combine(_workingDirectory, "source-two-colors.png");

        using (var bitmap = new Bitmap(200, 100))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Red);
            graphics.FillRectangle(Brushes.Blue, 100, 0, 100, 100);
            bitmap.Save(sourcePath);
        }

        var generator = new ImageAssetGenerator();
        var generatedAssets = generator.GenerateAssets(
            sourcePath,
            Path.Combine(_workingDirectory, "CroppedAssets"),
            new RectangleF(100, 0, 100, 100));

        using var image = new Bitmap(generatedAssets.Square150x150LogoPath);
        var samplePixel = image.GetPixel(75, 75);

        Assert.True(samplePixel.B > samplePixel.R);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    private static void AssertImageSize(string path, int expectedWidth, int expectedHeight)
    {
        Assert.True(File.Exists(path));
        using var image = Image.FromFile(path);
        Assert.Equal(expectedWidth, image.Width);
        Assert.Equal(expectedHeight, image.Height);
    }
}
