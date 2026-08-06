using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace KinesisEdit.Tests.Headless
{
    /// <summary>
    /// Reads a single pixel out of a rendered frame. It is the one assertion that reaches all the
    /// way through the design system to the glass: a token, resolved under a variant, applied by a
    /// style, drawn by Skia, and read back as the colour the handoff names.
    /// </summary>
    internal static class FramePixels
    {
        /// <summary>The colour at <paramref name="x"/>, <paramref name="y"/> of <paramref name="frame"/>.</summary>
        public static Color At(WriteableBitmap frame, int x, int y)
        {
            ArgumentNullException.ThrowIfNull(frame);

            using var buffer = frame.Lock();

            var offset = (y * buffer.RowBytes) + (x * 4);
            var bytes = new byte[4];

            Marshal.Copy(buffer.Address + offset, bytes, 0, bytes.Length);

            return buffer.Format switch
            {
                { } format when format == PixelFormat.Bgra8888 => Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]),
                { } format when format == PixelFormat.Rgba8888 => Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2]),
                _ => throw new NotSupportedException($"Unhandled frame format {buffer.Format}.")
            };
        }
    }
}
