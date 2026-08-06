using KinesisEdit.Core.Input;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The view layer's <see cref="IMessageBoxPresenter"/>. It presents nothing itself: it is a
    /// lazy forwarder onto the host that actually draws the box — <c>NotificationOverlay</c>, which
    /// renders it inline over the shell's scrim rather than in a window of its own
    /// (docs/design/mockups.md, mockup 1k).
    /// <para>
    /// The indirection exists because of construction order. This presenter is built in
    /// <c>App.BuildServices</c>, before <c>MainWindow</c> and therefore before the overlay exists —
    /// the notification service it feeds is a constructor dependency of the shell view model. The
    /// host is resolved through a callback at the moment a box is asked for, by which time the
    /// window is on screen; it is the same deferred pattern the two file pickers use for their
    /// owner window.
    /// </para>
    /// <para>
    /// It is also <b>the one place that brackets a box with the app's keystroke capture</b>. The
    /// box used to be its own <c>Window</c> — a second <c>TopLevel</c>, which
    /// <c>AvaloniaKeystrokeCaptureService</c> never previewed — and is now a card on the shell's
    /// own <c>TopLevel</c>, where a live capture swallows every key in the tunnel phase, the
    /// dialog's own Enter and Escape included, and feeds them to whatever is recording. Suspending
    /// here rather than at each call site is what makes that true of every box: the editor raises
    /// several with a key still listening (the firmware gate behind Tap and Hold, a failed save),
    /// and each of those would otherwise assign the Escape as a remap instead of closing the box.
    /// </para>
    /// </summary>
    public sealed class MessageBoxPresenter : IMessageBoxPresenter
    {
        private readonly Func<IMessageBoxPresenter?> _hostAccessor;
        private readonly Func<IKeystrokeCaptureService?>? _captureAccessor;
        private readonly object _syncRoot = new();

        private IKeystrokeCaptureService? _suspendedCapture;
        private int _openBoxes;

        /// <summary>
        /// Creates the presenter. <paramref name="hostAccessor"/> returns the overlay hosting the
        /// box, or null when there is none yet; <paramref name="captureAccessor"/> returns the
        /// app's keystroke capture service, or null when no editor has built one — nothing can be
        /// swallowing keys before that, so a null accessor simply means "there is nothing to
        /// suspend", which is what a headless or unit-test presenter passes.
        /// </summary>
        public MessageBoxPresenter(
            Func<IMessageBoxPresenter?> hostAccessor,
            Func<IKeystrokeCaptureService?>? captureAccessor = null)
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _captureAccessor = captureAccessor;
        }

        /// <summary>
        /// Presents <paramref name="request"/> on the host and completes once it is answered,
        /// with keystroke capture suspended for as long as the box is up.
        /// <para>
        /// <b>With no host registered this answers immediately rather than hanging.</b> A unit test
        /// and any headless path build the presenter over an accessor that returns null; a
        /// <see cref="Task"/> that never completes would deadlock the caller, so the request is
        /// reported as if it had been dismissed — the same result Escape produces, which is
        /// <c>Cancel</c> when the box has a Cancel button and <c>None</c> when it has not. Every
        /// caller already treats that branch as "the user did not answer", so nothing destructive
        /// can run off the back of a box nobody saw. Nothing is suspended on that branch either:
        /// no box reached the screen, so no key can be aimed at one.
        /// </para>
        /// </summary>
        public async Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var host = _hostAccessor();

            if (host is null)
            {
                var unpresented = new MessageBoxViewModel(request);

                return unpresented.Complete(unpresented.EscapeResult);
            }

            SuspendCapture();

            try
            {
                return await host.PresentAsync(request).ConfigureAwait(true);
            }
            finally
            {
                ResumeCapture();
            }
        }

        /// <summary>
        /// Suspends capture for the first box on screen. Counted, because the host queues a box
        /// that arrives while one is up and answers the first one first: resuming there would give
        /// the keyboard back while the second card is still being looked at.
        /// </summary>
        private void SuspendCapture()
        {
            IKeystrokeCaptureService? capture;

            lock (_syncRoot)
            {
                _openBoxes++;

                if (_openBoxes > 1 || _captureAccessor is null)
                {
                    return;
                }

                capture = _captureAccessor();

                // Nothing to suspend, or somebody else already holds the suspension — a text-entry
                // feature panel keeps one for its whole lifetime (EditorOverlayHost), and the way
                // out of this method would hand the keyboard back underneath it. Only a suspension
                // this presenter took is one this presenter may release.
                if (capture is null || capture.IsSuspended)
                {
                    return;
                }

                _suspendedCapture = capture;
            }

            capture.Suspend();
        }

        /// <summary>Releases the suspension taken by <see cref="SuspendCapture"/>, once the last box is gone.</summary>
        private void ResumeCapture()
        {
            IKeystrokeCaptureService capture;

            lock (_syncRoot)
            {
                _openBoxes--;

                if (_openBoxes > 0 || _suspendedCapture is null)
                {
                    return;
                }

                capture = _suspendedCapture;
                _suspendedCapture = null;
            }

            capture.Resume();
        }
    }
}
