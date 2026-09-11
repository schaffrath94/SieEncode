namespace RFiDGear.Builder.RfPrj;

public static class CardReportImageOrientation
{
    // EXIF tag 274: https://learn.microsoft.com/en-us/windows/win32/properties/props-system-photo-orientation
    public static CardReportImage Apply(CardReportImage image, int orientation)
    {
        if (image.Width <= 0 || image.Height <= 0 || (long)image.Width * image.Height * 3 != image.Rgb.Length)
            throw new ArgumentException("Invalid RGB image dimensions.", nameof(image));
        if (orientation is < 2 or > 8) return image;
        var width = orientation >= 5 ? image.Height : image.Width;
        var height = orientation >= 5 ? image.Width : image.Height;
        var pixels = new byte[image.Rgb.Length];
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var (dx, dy) = orientation switch
                {
                    2 => (image.Width - 1 - x, y),
                    3 => (image.Width - 1 - x, image.Height - 1 - y),
                    4 => (x, image.Height - 1 - y),
                    5 => (y, x),
                    6 => (image.Height - 1 - y, x),
                    7 => (image.Height - 1 - y, image.Width - 1 - x),
                    _ => (y, image.Width - 1 - x)
                };
                Array.Copy(image.Rgb, (y * image.Width + x) * 3, pixels, (dy * width + dx) * 3, 3);
            }
        return image with { Width = width, Height = height, Rgb = pixels };
    }
}
