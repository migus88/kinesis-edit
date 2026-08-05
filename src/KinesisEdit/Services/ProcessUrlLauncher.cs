using System.ComponentModel;
using System.Diagnostics;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Cross-platform <see cref="IUrlLauncher"/>: hands the URL to the OS shell
    /// (<c>UseShellExecute</c>), which opens the default browser on macOS, Windows, and Linux
    /// alike. Failures are reported, never thrown — a dead support link must not crash a dialog.
    /// </summary>
    public sealed class ProcessUrlLauncher : IUrlLauncher
    {
        /// <summary>Opens <paramref name="url"/> in the default browser.</summary>
        public bool Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                };

                using var process = Process.Start(startInfo);

                return true;
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or ObjectDisposedException or PlatformNotSupportedException)
            {
                return false;
            }
        }
    }
}
