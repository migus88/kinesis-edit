using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The shell's non-modal notification surface: it hosts the self-dismissing toasts and the
    /// loading indicator of specs/11-feature-dialogs.md §11.9 by subscribing to
    /// <see cref="INotificationService"/>. Toasts are split by
    /// <see cref="ToastPosition"/> into the two placements the spec allows, and each one is
    /// removed by a one-shot timer once its own timeout elapses.
    /// </summary>
    public partial class NotificationOverlay : UserControl
    {
        /// <summary>
        /// Resource key of the toast's in/out duration, defined by the motion budget in
        /// Themes/Motion.axaml. It is read rather than duplicated so the budget has one home.
        /// </summary>
        private const string ToastDurationResourceKey = "DurationToast";

        /// <summary>
        /// Used when <see cref="ToastDurationResourceKey"/> cannot be resolved — in a test host
        /// with no application resources, for instance. It only decides how long a dismissed toast
        /// lingers while it fades, so an approximation is harmless.
        /// </summary>
        private static readonly TimeSpan _fallbackToastDuration = TimeSpan.FromMilliseconds(180);

        /// <summary>Toasts placed over the center of the content.</summary>
        public ObservableCollection<ToastViewModel> CenteredToasts { get; } = [];

        /// <summary>Toasts placed in the bottom-right corner.</summary>
        public ObservableCollection<ToastViewModel> CornerToasts { get; } = [];

        /// <summary>The loading indicator; it renders only while it is visible.</summary>
        public LoadingViewModel Loading { get; } = new();

        private INotificationService? _notifications;

        /// <summary>Creates the overlay; it binds to itself, so it needs no view model of its own.</summary>
        public NotificationOverlay()
        {
            InitializeComponent();

            DataContext = this;
        }

        /// <summary>
        /// Starts hosting the notifications raised by <paramref name="notifications"/>. Attaching
        /// a second time replaces the previous subscription.
        /// </summary>
        public void Attach(INotificationService notifications)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            Detach();

            _notifications = notifications;
            notifications.ToastRequested += OnToastRequested;
            notifications.LoadingChanged += OnLoadingChanged;
        }

        /// <summary>Stops hosting notifications; safe when nothing is attached.</summary>
        public void Detach()
        {
            if (_notifications is null)
            {
                return;
            }

            _notifications.ToastRequested -= OnToastRequested;
            _notifications.LoadingChanged -= OnLoadingChanged;
            _notifications = null;
        }

        private void OnToastRequested(ToastRequest request)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnToastRequested(request));

                return;
            }

            var toast = new ToastViewModel(request);
            var toasts = GetCollectionFor(request.Position);

            toasts.Add(toast);

            // Raised on the next pass rather than here: the view does not exist yet, so setting it
            // now would put the toast on screen already shown and there would be nothing for the
            // fade to run on.
            Dispatcher.UIThread.Post(() => toast.IsShown = true, DispatcherPriority.Background);

            // The spec's dwell is the request's own timeout; the toast then fades for as long as
            // the budget says before it is actually dropped, so it is never removed mid-animation.
            DispatcherTimer.RunOnce(
                () =>
                {
                    toast.IsShown = false;

                    DispatcherTimer.RunOnce(() => toasts.Remove(toast), ResolveToastDuration());
                },
                request.Timeout);
        }

        private static TimeSpan ResolveToastDuration()
        {
            var application = Application.Current;

            if (application is not null
                && application.TryFindResource(ToastDurationResourceKey, out var value)
                && value is TimeSpan duration)
            {
                return duration;
            }

            return _fallbackToastDuration;
        }

        private void OnLoadingChanged(string? caption)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnLoadingChanged(caption));

                return;
            }

            if (caption is not null)
            {
                Loading.Caption = caption;
            }

            Loading.IsVisible = caption is not null;
        }

        private ObservableCollection<ToastViewModel> GetCollectionFor(ToastPosition position)
        {
            return position == ToastPosition.Center ? CenteredToasts : CornerToasts;
        }
    }
}
