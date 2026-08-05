using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// The UI-free gate in front of <see cref="KeystrokeRecorder"/>: it owns the started/stopped
    /// and suspended states that specs/10-apps-and-ui.md ("Keyboard-input capture") requires, so
    /// a host adapter only has to translate platform events and report where focus sits.
    /// <list type="bullet">
    /// <item>While not capturing, or while suspended, every key passes through untouched —
    /// nothing is captured and nothing is swallowed. The spec demands that whenever a dialog
    /// needs real typing.</item>
    /// <item>Suspension has two independent sources: the explicit
    /// <see cref="Suspend"/>/<see cref="Resume"/> pair and text-input focus reported through
    /// <see cref="SetTextInputFocused"/>. <see cref="IsSuspended"/> is the authoritative
    /// combination of both.</item>
    /// <item><see cref="Stop"/> also clears an explicit suspension, so a stopped session can
    /// never be restarted into a silently deaf state.</item>
    /// </list>
    /// </summary>
    public sealed class KeystrokeCaptureSession
    {
        /// <summary>Whether capture is started. Unaffected by <see cref="IsSuspended"/>.</summary>
        public bool IsCapturing { get; private set; }

        /// <summary>
        /// Whether capture is suspended — true when it was suspended explicitly <b>or</b> while
        /// text input has focus. While true, no keystroke is captured and none is swallowed.
        /// </summary>
        public bool IsSuspended
        {
            get => IsExplicitlySuspended || IsTextInputFocused;
        }

        /// <summary>Whether <see cref="Suspend"/> was called and not yet undone.</summary>
        public bool IsExplicitlySuspended { get; private set; }

        /// <summary>Whether the host last reported focus sitting in a text-input control.</summary>
        public bool IsTextInputFocused { get; private set; }

        /// <summary>
        /// The modifiers currently held by the recorder, in press order
        /// (specs/05-key-model.md §5.1).
        /// </summary>
        public IReadOnlyList<KeyDefinition> HeldModifiers
        {
            get => _recorder.HeldModifiers;
        }

        /// <summary>Raised for every keystroke the recorder completes while capture is live.</summary>
        public event Action<CapturedKeystroke>? KeystrokeCaptured;

        /// <summary>
        /// Raised whenever <see cref="IsCapturing"/> or <see cref="IsSuspended"/> actually change,
        /// so a UI can refresh its status without polling. No-op calls raise nothing.
        /// </summary>
        public event Action? StateChanged;

        private readonly KeystrokeRecorder _recorder = new();

        /// <summary>Starts capturing from a clean recorder state. A no-op when already capturing.</summary>
        public void Start()
        {
            if (IsCapturing)
            {
                return;
            }

            var wasSuspended = IsSuspended;

            _recorder.Reset();
            IsCapturing = true;

            RaiseStateChangedIfChanged(wasCapturing: false, wasSuspended);
        }

        /// <summary>
        /// Stops capturing, resets the recorder, and clears an explicit suspension — a stopped
        /// session must not stay deaf across the next <see cref="Start"/>. A no-op when already
        /// stopped and not explicitly suspended.
        /// </summary>
        public void Stop()
        {
            if (!IsCapturing && !IsExplicitlySuspended)
            {
                return;
            }

            var wasCapturing = IsCapturing;
            var wasSuspended = IsSuspended;

            IsCapturing = false;
            IsExplicitlySuspended = false;
            _recorder.Reset();

            RaiseStateChangedIfChanged(wasCapturing, wasSuspended);
        }

        /// <summary>
        /// Suspends capture and resets the recorder, letting every keystroke through while a
        /// dialog needs real typing (specs/10-apps-and-ui.md). A no-op when already explicitly
        /// suspended.
        /// </summary>
        public void Suspend()
        {
            if (IsExplicitlySuspended)
            {
                return;
            }

            var wasSuspended = IsSuspended;

            IsExplicitlySuspended = true;
            _recorder.Reset();

            RaiseStateChangedIfChanged(IsCapturing, wasSuspended);
        }

        /// <summary>
        /// Clears an explicit suspension. A no-op when not explicitly suspended, and it never
        /// starts capture on a stopped session; it also leaves a text-input suspension standing.
        /// </summary>
        public void Resume()
        {
            if (!IsExplicitlySuspended)
            {
                return;
            }

            var wasSuspended = IsSuspended;

            IsExplicitlySuspended = false;

            RaiseStateChangedIfChanged(IsCapturing, wasSuspended);
        }

        /// <summary>
        /// Reports where focus sits. Focus landing in a text-input control suspends capture and
        /// resets the recorder, so keys typed there reach the control untouched and no modifier
        /// leaks into the next recording. Only real transitions raise <see cref="StateChanged"/>.
        /// </summary>
        public void SetTextInputFocused(bool isFocused)
        {
            if (IsTextInputFocused == isFocused)
            {
                return;
            }

            var wasSuspended = IsSuspended;

            IsTextInputFocused = isFocused;

            if (isFocused)
            {
                _recorder.Reset();
            }

            RaiseStateChangedIfChanged(IsCapturing, wasSuspended);
        }

        /// <summary>
        /// Feeds one physical key event to the recorder and returns what the host must do with it.
        /// While not capturing or while suspended the event passes through untouched and nothing
        /// is emitted; otherwise a completed keystroke also raises <see cref="KeystrokeCaptured"/>.
        /// </summary>
        public KeystrokeCaptureResult Handle(PhysicalKeyCode key, KeyTransition transition)
        {
            if (!IsCapturing || IsSuspended)
            {
                return KeystrokeCaptureResult.PassThrough;
            }

            var result = _recorder.Handle(key, transition);

            if (result.Keystroke is not null)
            {
                KeystrokeCaptured?.Invoke(result.Keystroke);
            }

            return result;
        }

        private void RaiseStateChangedIfChanged(bool wasCapturing, bool wasSuspended)
        {
            if (IsCapturing == wasCapturing && IsSuspended == wasSuspended)
            {
                return;
            }

            StateChanged?.Invoke();
        }
    }
}
