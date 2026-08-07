using Avalonia.Controls;
using Avalonia.Threading;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The single-window shell: a top bar carrying Home / Settings / Help and the v-Drive status
    /// indicator, a content host that swaps between the dashboard and the open editor, and the
    /// notification overlay floating above both — toasts, the blocking loading card and the modal
    /// message box.
    /// <para>
    /// It is also where quitting meets unsaved work. Closing the window is the third way out of an
    /// editing session, next to Home and Configure, and it used to be the only one that asked
    /// nothing: <c>MainWindowViewModel.Dispose</c> runs from <c>desktop.Exit</c> and cannot await a
    /// dialog. <see cref="OnClosing"/> asks first, on the same seam the other two use.
    /// </para>
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// The surface message boxes are drawn on. <c>MessageBoxPresenter</c> is built before this
        /// window exists and resolves it through a callback at the moment a box is asked for; this
        /// is what that callback returns.
        /// </summary>
        public IMessageBoxPresenter MessageBoxHost => Notifications;

        /// <summary>
        /// Whether the close now in flight has already been approved. <see cref="OnClosing"/>
        /// cancels the first close, asks, and re-issues the close on a yes; without this latch that
        /// second <see cref="Window.Close"/> would ask again, and again.
        /// </summary>
        private bool _isClosingConfirmed;

        /// <summary>Parameterless constructor for the XAML designer.</summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>Creates the shell window and starts hosting <paramref name="notifications"/>.</summary>
        public MainWindow(MainWindowViewModel viewModel, INotificationService notifications) : this()
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(notifications);

            DataContext = viewModel;

            Notifications.Attach(notifications);
        }

        /// <summary>
        /// The unsaved-changes guard. The close is always cancelled first and only re-issued once
        /// the shell has answered, because the answer needs a dialog and this event cannot wait for
        /// one.
        /// <para>
        /// <b>Every close reason is guarded, OS shutdown included</b>, and
        /// <see cref="WindowClosingEventArgs.IsProgrammatic"/> is not special-cased. The editor
        /// holds the same unsaved edits whichever of them is happening, and the platform may or may
        /// not honour a cancellation during a session logout — but asking is never worse than not
        /// asking: if the cancellation is honoured the work is saved, and if it is not, the app is
        /// killed exactly as it would have been without the guard.
        /// </para>
        /// </summary>
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            // Somebody else already stopped this close, or this *is* the close approved below.
            if (e.Cancel || _isClosingConfirmed)
            {
                return;
            }

            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            e.Cancel = true;

            // Posted rather than started inline: this method is still on the stack, and a shell
            // with nothing to ask answers synchronously — which would re-enter Close() while the
            // framework is halfway through cancelling this one.
            Dispatcher.UIThread.Post(() => _ = ConfirmAndCloseAsync(viewModel));
        }

        /// <summary>
        /// Asks the shell whether the app may close and closes it if it may. <b>Total</b>: it runs
        /// detached from the event that started it, so anything escaping would be an unobserved
        /// exception on the UI thread — and a failure here leaves the window open, which is the
        /// safe answer anyway.
        /// </summary>
        private async Task ConfirmAndCloseAsync(MainWindowViewModel viewModel)
        {
            try
            {
                if (!await viewModel.ConfirmShutdownAsync().ConfigureAwait(true))
                {
                    return;
                }

                _isClosingConfirmed = true;

                Close();
            }
            catch (Exception)
            {
                // Deliberately swallowed; see the summary. The window stays open.
            }
        }
    }
}
