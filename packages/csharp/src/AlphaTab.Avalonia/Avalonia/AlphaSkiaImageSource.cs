using AlphaTab.Platform.Skia.AlphaSkiaBridge;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AlphaTab.Avalonia;

internal static class AlphaSkiaImageSource
{
    public static WriteableBitmap Create(AlphaSkiaImage imageBridge)
    {
        var image = imageBridge.Image;
        var bitmap = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var buffer = bitmap.Lock();
        image.ReadPixels(buffer.Address, (ulong)buffer.RowBytes);

        return bitmap;
    }
}
