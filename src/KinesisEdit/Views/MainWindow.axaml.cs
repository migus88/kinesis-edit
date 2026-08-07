using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The single-window shell: a top bar carrying Home / Settings / Help and the v-Drive status
    /// indicator, a content host that swaps between the dashboard, the open editor and the shell's
    /// own two screens, and the notification overlay floating above them — toasts, the blocking
    /// loading card and the modal message box.
    /// <para>
    /// It is also where quitting meets unsaved work. Closing the window is the third way out of an
    /// editing session, next to Home and Configure, and it used to be the only one that asked
    /// nothing: <c>MainWindowViewModel.Dispose</c> runs from <c>desktop.Exit</c> and cannot await a
    /// dialog. <see cref="OnClosing"/> asks first, on the same seam the other two use.
    /// </para>
    /// <para>
    /// And it is the one place the window's own geometry is remembered
    /// (docs/app/host-preferences.md). Reading a live window's size, position and state is Avalonia
    /// business and cannot live in a view model (app-shell.md invariant 8), so the restore runs in
    /// the constructor — before the window is shown — and the save runs the instant a close is
    /// approved.
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
        /// Where the geometry is read from and written back to, or null for a window that
        /// remembers nothing — every headless scene, and the XAML designer.
        /// </summary>
        private readonly IHostPreferencesStore? _preferences;

        /// <summary>
        /// Whether the close now in flight has already been approved. <see cref="OnClosing"/>
        /// cancels the first close, asks, and re-issues the close on a yes; without this latch that
        /// second <see cref="Window.Close"/> would ask again, and again.
        /// </summary>
        private bool _isClosingConfirmed;

        /// <summary>
        /// The last size this window had while it was an ordinary window. A maximised window's
        /// <see cref="TopLevel.ClientSize"/> is the size of the screen, and Avalonia surfaces no
        /// platform restore bounds — so persisting that as the size would make un-maximising after
        /// a restart do nothing at all. This is what is written instead.
        /// </summary>
        private double _restoreWidth;

        /// <summary>The height half of <see cref="_restoreWidth"/>.</summary>
        private double _restoreHeight;

        /// <summary>Parameterless constructor for the XAML designer.</summary>
        public MainWindow()
        {
            InitializeComponent();

            // The XAML's declared size, which is the fallback restore size until the window has
            // been laid out even once.
            _restoreWidth = Width;
            _restoreHeight = Height;
        }

        /// <summary>
        /// Creates the shell window, starts hosting <paramref name="notifications"/> and — when
        /// <paramref name="preferences"/> is given — puts the window back where it was last time.
        /// The store is optional because only the composition root has one: a headless scene builds
        /// this window to render it, and a window that read and wrote the real user's preferences
        /// file from a test would be a defect of its own.
        /// </summary>
        public MainWindow(
            MainWindowViewModel viewModel,
            INotificationService notifications,
            IHostPreferencesStore? preferences = null) : this()
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(notifications);

            DataContext = viewModel;
            _preferences = preferences;

            Notifications.Attach(notifications);

            RestoreGeometry();
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
        /// Keeps <see cref="_restoreWidth"/>/<see cref="_restoreHeight"/> on the last size this
        /// window had as an ordinary window. Guarded on the state rather than recorded
        /// unconditionally, because the resize that maximises a window would otherwise overwrite
        /// the very size the maximised window has to be restored to.
        /// </summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ClientSizeProperty && WindowState == WindowState.Normal)
            {
                _restoreWidth = ClientSize.Width;
                _restoreHeight = ClientSize.Height;
            }
        }

        /// <summary>
        /// Asks the shell whether the app may close and closes it if it may. <b>Total</b>: it runs
        /// detached from the event that started it, so anything escaping would be an unobserved
        /// exception on the UI thread — and a failure here leaves the window open, which is the
        /// safe answer anyway.
        /// <para>
        /// The geometry is written <b>here</b>, between the approval and the <see cref="Close"/>,
        /// and deliberately not from <see cref="OnClosing"/>: that override cancels <em>every</em>
        /// first-pass close and re-issues it only once the question has been answered, so a save
        /// there would record the window's geometry for closes the user then aborts. This is the
        /// one point at which the close is certain and the window is still fully alive.
        /// </para>
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

                PersistGeometry();

                Close();
            }
            catch (Exception)
            {
                // Deliberately swallowed; see the summary. The window stays open.
            }
        }

        /// <summary>
        /// Puts the window back where the stored preferences say it was. Runs from the constructor,
        /// so everything it sets is in place before the first frame.
        /// <para>
        /// Two things are refused rather than obeyed. A stored size below the window's own
        /// <see cref="Layoutable.MinWidth"/>/<see cref="Layoutable.MinHeight"/> is clamped up to it
        /// — the floor is the window's business and the stored file is not allowed to undercut it.
        /// And a stored position that is on no currently connected screen is dropped: a window last
        /// closed on a monitor that has since been unplugged would otherwise open somewhere the
        /// user cannot reach it. The size still applies; the platform picks the place.
        /// </para>
        /// <para>
        /// <b>Nothing ever restores into a minimised state</b>: the only state this sets is
        /// <see cref="WindowState.Maximized"/>, and only when the file says so.
        /// </para>
        /// </summary>
        private void RestoreGeometry()
        {
            var geometry = _preferences?.Current.Window;

            if (geometry is null || !geometry.IsUsable)
            {
                return;
            }

            Width = Math.Max(geometry.Width, MinWidth);
            Height = Math.Max(geometry.Height, MinHeight);

            _restoreWidth = Width;
            _restoreHeight = Height;

            var position = geometry.HasPosition
                ? new PixelPoint(geometry.X!.Value, geometry.Y!.Value)
                : (PixelPoint?)null;

            if (position is not null && IsOnAConnectedScreen(position.Value))
            {
                Position = position.Value;
            }
            else
            {
                // No usable place of our own to ask for, so the platform gets to choose one — and
                // "centred on a screen that exists" is a better answer than the (0, 0) a Manual
                // startup location would leave behind.
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (geometry.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// Writes this window's geometry back to the store. <b>Never throws into the close path</b>:
        /// a preference is not worth a window that refuses to shut, and the store swallows its own
        /// I/O failures for the same reason (docs/app/host-preferences.md).
        /// </summary>
        private void PersistGeometry()
        {
            if (_preferences is null)
            {
                return;
            }

            try
            {
                var geometry = CaptureGeometry();

                if (geometry is null)
                {
                    return;
                }

                // A function, not a whole record: the Settings screen writes the theme through the
                // same store and a set-the-record API would let either clobber the other.
                _preferences.Update(current => current with { Window = geometry });
            }
            catch (Exception)
            {
                // Deliberately broad and deliberately silent; see the summary.
            }
        }

        /// <summary>
        /// This window as a <see cref="WindowGeometry"/>, or null when it is not currently
        /// describable as one — which leaves whatever was stored before in place rather than
        /// replacing it with nonsense.
        /// <para>
        /// A window that is not in its ordinary state reports its <em>restore</em> size, so a
        /// maximised window is remembered as maximised <em>and</em> as the size it un-maximises to,
        /// and a minimised one is remembered as the ordinary window it last was. A position that
        /// <see cref="RestoreGeometry"/> would refuse is not stored at all — a minimised window
        /// reports an off-screen position on some platforms, and overwriting a good stored position
        /// with it would cost the user their placement.
        /// </para>
        /// </summary>
        private WindowGeometry? CaptureGeometry()
        {
            var isOrdinary = WindowState == WindowState.Normal;
            var width = isOrdinary ? ClientSize.Width : _restoreWidth;
            var height = isOrdinary ? ClientSize.Height : _restoreHeight;
            var position = Position;
            var hasPosition = IsOnAConnectedScreen(position);

            return WindowGeometry.TryCreate(
                width,
                height,
                hasPosition ? position.X : null,
                hasPosition ? position.Y : null,
                WindowState == WindowState.Maximized);
        }

        /// <summary>
        /// Whether <paramref name="position"/> — a window's top-left corner, in screen pixels —
        /// lands on a screen this machine currently has. The corner rather than the whole rectangle
        /// on purpose: it is the end of the title bar the user grabs, and a window whose corner is
        /// on a screen can always be moved. A platform that reports no screens at all answers false,
        /// which degrades to "let the platform place it".
        /// </summary>
        private bool IsOnAConnectedScreen(PixelPoint position)
        {
            try
            {
                return Screens?.ScreenFromPoint(position) is not null;
            }
            catch (Exception)
            {
                // Screen enumeration is a platform call and this runs on the close path too.
                return false;
            }
        }
    }
}
