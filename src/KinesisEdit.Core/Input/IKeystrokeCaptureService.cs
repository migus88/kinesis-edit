namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// The keystroke-capture abstraction the remap and macro-recording editors code against.
    /// An implementation wires a platform's key events into a
    /// <see cref="KeystrokeCaptureSession"/> and raises <see cref="KeystrokeCaptured"/> for every
    /// keystroke the session completes, per specs/10-apps-and-ui.md ("Keyboard-input capture").
    /// <para>
    /// Contract:
    /// </para>
    /// <list type="bullet">
    /// <item>While capturing, keys that belong to the recording are swallowed so the OS never
    /// sees them; keys the recorder does not resolve pass through untouched.</item>
    /// <item>While suspended, <b>nothing</b> is captured and <b>no</b> key is swallowed — every
    /// keystroke passes through normally. specs/10-apps-and-ui.md requires this whenever a
    /// dialog that needs real typing is open (for example Save As).</item>
    /// <item><see cref="Stop"/> and <see cref="Suspend"/> reset the held-modifier state, so a
    /// modifier released outside the capture window cannot leak into the next recording;
    /// <see cref="Stop"/> also clears an explicit suspension.</item>
    /// <item><see cref="Start"/> on an already-started service, <see cref="Stop"/> on a stopped
    /// one, <see cref="Suspend"/> while suspended, and <see cref="Resume"/> while not suspended
    /// are all no-ops.</item>
    /// <item><see cref="Resume"/> on a stopped service does not start capturing; it only clears
    /// the suspension.</item>
    /// </list>
    /// <para>
    /// The interface extends <see cref="IDisposable"/> because implementations attach to
    /// platform key events and need deterministic teardown. Disposing stops capture.
    /// </para>
    /// </summary>
    public interface IKeystrokeCaptureService : IDisposable
    {
        /// <summary>Whether capture is currently started. Unaffected by <see cref="IsSuspended"/>.</summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Whether capture is suspended, from <b>any</b> source — the explicit
        /// <see cref="Suspend"/> call or a platform condition such as text-input focus. This flag
        /// is authoritative: while it is true, no keystroke is captured or swallowed even when
        /// <see cref="IsCapturing"/> is true.
        /// </summary>
        bool IsSuspended { get; }

        /// <summary>Raised for every completed keystroke while capturing and not suspended.</summary>
        event Action<CapturedKeystroke> KeystrokeCaptured;

        /// <summary>Starts capturing. A no-op when already capturing.</summary>
        void Start();

        /// <summary>
        /// Stops capturing, resets the held-modifier state, and clears an explicit suspension so
        /// the next <see cref="Start"/> cannot come up silently deaf. A no-op when already stopped
        /// and not suspended.
        /// </summary>
        void Stop();

        /// <summary>
        /// Suspends capture and resets the held-modifier state, letting every keystroke through
        /// while a dialog needs real typing (specs/10-apps-and-ui.md). A no-op when already
        /// suspended.
        /// </summary>
        void Suspend();

        /// <summary>Resumes a suspended capture. A no-op when not suspended.</summary>
        void Resume();
    }
}
