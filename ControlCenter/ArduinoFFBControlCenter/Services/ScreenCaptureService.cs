using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using DrawingSize = System.Drawing.Size;

namespace ArduinoFFBControlCenter.Services;

public class ScreenCaptureService
{
    public byte[] CaptureVirtualScreenPng(int maxWidth = 1280)
    {
        var left = (int)SystemParameters.VirtualScreenLeft;
        var top = (int)SystemParameters.VirtualScreenTop;
        var width = Math.Max(1, (int)SystemParameters.VirtualScreenWidth);
        var height = Math.Max(1, (int)SystemParameters.VirtualScreenHeight);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(left, top, 0, 0, new DrawingSize(width, height), CopyPixelOperation.SourceCopy);
        }

        using var scaled = ScaleIfNeeded(bitmap, maxWidth);
        using var ms = new MemoryStream();
        scaled.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static Bitmap ScaleIfNeeded(Bitmap source, int maxWidth)
    {
        if (source.Width <= maxWidth)
        {
            return new Bitmap(source);
        }

        var ratio = (double)maxWidth / source.Width;
        var targetHeight = Math.Max(1, (int)Math.Round(source.Height * ratio));
        return new Bitmap(source, new DrawingSize(maxWidth, targetHeight));
    }
}
