using System.Drawing.Imaging;
using System.IO;

namespace AlphaTab.Avalonia;

internal static class GdiImageSource
{
    public static global::Avalonia.Media.Imaging.Bitmap Create(System.Drawing.Bitmap image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return new global::Avalonia.Media.Imaging.Bitmap(stream);
    }
}
