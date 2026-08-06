using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The shell's notification surface: it hosts the self-dismissing toasts, the blocking loading
    /// card and the modal message box of specs/11-feature-dialogs.md §11.9 (drawn as mockup 1k
    /// draws them) by subscribing to <see cref="INotificationService"/> and by standing in as the
    /// <see cref="IMessageBoxPresenter"/> that <see cref="MessageBoxPresenter"/> forwards to.
    /// Toasts are split by <see cref="ToastPosition"/> into the two placements the spec allows.
    /// <para>
    /// The message box is <b>queued, never stacked</b>: a request that arrives while one is up
    /// waits for it. Two cards centred on the same scrim would hide one another, and answering a
    /// box the user cannot see is worse than making them answer the first one.
    /// </para>
    /// </summary>
    public partial class NotificationOverlay : UserControl, IMessageBoxPresenter
    {
        /// <summary>
        /// Resource key of the toast's in/out duration, defined by the motion budget in
        /// Themes/Motion.axaml. It is read rather than duplicated so the budget has one home.
        /// </summary>
        private const string ToastDurationResourceKey = "DurationToast";

        /// <summary>Resource key of the modal fade, from the same budget.</summary>
        private const string ModalDurationResourceKey = "DurationModalIn";

        /// <summary>The class that opens a faded surface; the styles in Styles/Surfaces.axaml own the rest.</summary>
        private const string OpenClass = "open";

        /// <summary>
        /// Used when a duration resource cannot be resolved — in a test host with no application
        /// resources, for instance. It only decides how long a dismissed surface lingers while it
        /// fades, so an approximation is harmless.
        /// </summary>
        private static readonly TimeSpan _fallbackFadeDuration = TimeSpan.FromMilliseconds(180);

        /// <summary>Toasts placed over the center of the content.</summary>
        public ObservableCollection<ToastViewModel> CenteredToasts { get; } = [];

        /// <summary>Toasts placed in the bottom-right corner.</summary>
        public ObservableCollection<ToastViewModel> CornerToasts { get; } = [];

        /// <summary>The loading indicator; it renders only while it is visible.</summary>
        public LoadingViewModel Loading { get; } = new();

        /// <summary>The message box on screen right now, or null when none is up.</summary>
        public MessageBoxViewModel? MessageBox { get; private set; }

        private readonly Queue<PendingMessageBox> _pendingMessageBoxes = new();

        private INotificationService? _notifications;
        private PendingMessageBox? _currentMessageBox;
        private IDisposable? _modalRemoval;

        /// <summary>Creates the overlay; it binds to itself, so it needs no view model of its own.</summary>
        public NotificationOverlay()
        {
            InitializeComponent();

            DataContext = this;

            Loading.PropertyChanged += OnLoadingPropertyChanged;
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

        /// <summary>
        /// Puts <paramref name="request"/> on screen over the scrim and completes with the user's
        /// answer. A box arriving while another is up is queued behind it.
        /// </summary>
        public Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!Dispatcher.UIThread.CheckAccess())
            {
                return Dispatcher.UIThread.InvokeAsync(() => PresentAsync(request));
            }

            var pending = new PendingMessageBox(new MessageBoxViewModel(request));

            _pendingMessageBoxes.Enqueue(pending);

            ShowNextMessageBox();

            return pending.Completion.Task;
        }

        private static TimeSpan ResolveDuration(string resourceKey)
        {
            var application = Application.Current;

            if (application is not null
                && application.TryFindResource(resourceKey, out var value)
                && value is TimeSpan duration)
            {
                return duration;
            }

            return _fallbackFadeDuration;
        }

        private void ShowNextMessageBox()
        {
            if (_currentMessageBox is not null || _pendingMessageBoxes.Count == 0)
            {
                return;
            }

            // A box that arrives while the previous one is still fading out reuses the host, so the
            // scheduled removal must not fire underneath it.
            _modalRemoval?.Dispose();
            _modalRemoval = null;

            var pending = _pendingMessageBoxes.Dequeue();

            _currentMessageBox = pending;
            MessageBox = pending.ViewModel;

            pending.ViewModel.Completed += OnMessageBoxCompleted;

            ModalHost.Content = pending.ViewModel;
            UpdateScrim();

            // Raised on the next pass rather than here, for the same reason the toast's is: the
            // card does not exist yet, so opening it now would put it on screen already shown and
            // the 140 ms fade would have nothing to run on.
            Dispatcher.UIThread.Post(() => ModalHost.Classes.Add(OpenClass), DispatcherPriority.Background);
        }

        private void OnMessageBoxCompleted(MessageBoxOutcome outcome)
        {
            var pending = _currentMessageBox;

            if (pending is null)
            {
                return;
            }

            pending.ViewModel.Completed -= OnMessageBoxCompleted;

            _currentMessageBox = null;
            MessageBox = null;

            ModalHost.Classes.Remove(OpenClass);
            UpdateScrim();

            // The caller is answered now rather than after the fade: an outcome that waited on a
            // timer would make every `await ShowMessageBoxAsync` depend on the dispatcher still
            // pumping, and the fade is cosmetic.
            pending.Completion.TrySetResult(outcome);

            // The card stays in the host while it fades, then leaves - unless the next box in the
            // queue claims the host first, which ShowNextMessageBox handles.
            _modalRemoval = DispatcherTimer.RunOnce(ClearModalHost, ResolveDuration(ModalDurationResourceKey));

            ShowNextMessageBox();
        }

        private void ClearModalHost()
        {
            _modalRemoval = null;

            if (_currentMessageBox is null)
            {
                ModalHost.Content = null;
            }
        }

        /// <summary>
        /// The scrim is up while anything blocking is: the modal message box, or the loading card,
        /// which mockup 1k draws as "Loading · blocking". It is hit-testable only then — at rest it
        /// must not swallow a click aimed at the dashboard behind it.
        /// </summary>
        private void UpdateScrim()
        {
            var isBlocking = _currentMessageBox is not null || Loading.IsVisible;

            Scrim.IsHitTestVisible = isBlocking;

            if (isBlocking)
            {
                Scrim.Classes.Add(OpenClass);
            }
            else
            {
                Scrim.Classes.Remove(OpenClass);
            }
        }

        private void OnLoadingPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoadingViewModel.IsVisible))
            {
                UpdateScrim();
            }
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

            // One lifetime per toast owns both of its timers, so an early `×` cancels the pending
            // dwell instead of leaving it to fire into a toast that has already gone.
            _ = new ToastLifetime(toast, toasts, ResolveDuration(ToastDurationResourceKey));
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

        /// <summary>One queued message box and the task its caller is awaiting.</summary>
        private sealed class PendingMessageBox
        {
            /// <summary>The dialog itself.</summary>
            public MessageBoxViewModel ViewModel { get; }

            /// <summary>Completed once the dialog is answered.</summary>
            public TaskCompletionSource<MessageBoxOutcome> Completion { get; } = new();

            public PendingMessageBox(MessageBoxViewModel viewModel)
            {
                ViewModel = viewModel;
            }
        }

        /// <summary>
        /// The dwell and the fade-out of one toast. Both timers live here so that a `×` dismiss
        /// <b>cancels</b> the dwell rather than racing it: without that, a toast closed early would
        /// be scheduled for removal twice, and the second removal would land while a later toast
        /// was mid-fade.
        /// </summary>
        private sealed class ToastLifetime
        {
            private readonly ToastViewModel _toast;
            private readonly ObservableCollection<ToastViewModel> _host;
            private readonly TimeSpan _fadeDuration;

            private IDisposable? _dwell;
            private IDisposable? _removal;
            private bool _isHiding;

            public ToastLifetime(
                ToastViewModel toast,
                ObservableCollection<ToastViewModel> host,
                TimeSpan fadeDuration)
            {
                _toast = toast;
                _host = host;
                _fadeDuration = fadeDuration;

                _toast.DismissRequested += OnDismissRequested;

                // The spec's dwell is the request's own timeout; the toast then fades for as long as
                // the budget says before it is actually dropped, so it is never removed mid-animation.
                _dwell = DispatcherTimer.RunOnce(Hide, toast.Timeout);
            }

            private void OnDismissRequested(ToastViewModel toast)
            {
                Hide();
            }

            private void Hide()
            {
                if (_isHiding)
                {
                    return;
                }

                _isHiding = true;

                _dwell?.Dispose();
                _dwell = null;

                _toast.IsShown = false;
                _removal = DispatcherTimer.RunOnce(Remove, _fadeDuration);
            }

            private void Remove()
            {
                _removal = null;

                _toast.DismissRequested -= OnDismissRequested;
                _host.Remove(_toast);
            }
        }
    }
}
