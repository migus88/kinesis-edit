using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

namespace KinesisEdit.Tests.Headless
{
    /// <summary>
    /// Puts one control on screen under a chosen theme variant and drives it far enough that the
    /// styles have been applied, the resources resolved and a frame drawn.
    /// <para>
    /// The theme is pinned on the window rather than on the application, because
    /// <c>RequestedThemeVariant</c> scopes to the subtree it is set on: two hosts can therefore
    /// exist in the same session, one dark and one light, without either reaching into global
    /// state that the next test would inherit.
    /// </para>
    /// </summary>
    internal sealed class ThemedHost : IDisposable
    {
        /// <summary>The app's default window size (MainWindow.axaml), used unless a test says otherwise.</summary>
        public const double DefaultWidth = 1000;

        /// <summary>The app's default window height (MainWindow.axaml).</summary>
        public const double DefaultHeight = 680;

        /// <summary>How many render-timer ticks a frame is waited for; see <see cref="Capture"/>.</summary>
        private const int CaptureAttempts = 5;

        /// <summary>The window the content was hosted in.</summary>
        public Window Window { get; }

        private ThemedHost(Window window)
        {
            Window = window;
        }

        /// <summary>
        /// Shows <paramref name="content"/> under <paramref name="variant"/>. A control that is
        /// already a <see cref="Window"/> is shown as itself; anything else is hosted in a plain
        /// window sized like the shell.
        /// </summary>
        public static ThemedHost Show(
            Control content,
            ThemeVariant variant,
            double width = DefaultWidth,
            double height = DefaultHeight)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(variant);

            var window = content as Window ?? new Window { Content = content };

            window.RequestedThemeVariant = variant;
            window.Width = width;
            window.Height = height;
            window.Show();

            // Show() only queues the first layout pass; the jobs have to run before anything has
            // measured, arranged or resolved a DynamicResource.
            Dispatcher.UIThread.RunJobs();

            return new ThemedHost(window);
        }

        /// <summary>
        /// Rasterises the current frame. Requires the harness's Skia drawing.
        /// <para>
        /// The render loop is driven by a timer the headless platform only ticks on demand, and a
        /// window that has not been through a render pass yet answers null rather than a frame.
        /// Ticking it is therefore part of asking for the picture, not an optimisation — and it is
        /// retried, because the first tick after a window opens can be spent on the layout pass.
        /// </para>
        /// </summary>
        public WriteableBitmap Capture()
        {
            for (var attempt = 0; attempt < CaptureAttempts; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                if (Window.CaptureRenderedFrame() is { } frame)
                {
                    return frame;
                }
            }

            throw new InvalidOperationException("The headless platform rendered no frame.");
        }

        public void Dispose()
        {
            Window.Close();

            Dispatcher.UIThread.RunJobs();
        }
    }
}
