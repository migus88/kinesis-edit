using System.ComponentModel;
using KinesisEdit.Core.Input;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Holds whichever of the editor's inline feature panels (spec 11) is open: the swap itself,
    /// the nesting §11.1's Search buttons need, and the keystroke capture a panel needs — started
    /// for a sink that is waiting for a keypress, suspended for a text-entry panel.
    /// <para>
    /// It is a collaborator of <see cref="KeyboardEditorViewModel"/> rather than part of it
    /// because none of this is about a keyboard — the editor decides <em>which</em> panel opens
    /// and hears back that the open one moved, and every subscription a shown panel needs is
    /// balanced in here. It is not a view model: the editor projects
    /// <see cref="Active"/> as its own bindable property, so a view never sees this type.
    /// </para>
    /// </summary>
    public sealed class EditorOverlayHost
    {
        /// <summary>The panel rendered over the editor, or null when none is open.</summary>
        public EditorOverlayViewModel? Active
        {
            get => _active;
            private set
            {
                if (ReferenceEquals(_active, value))
                {
                    return;
                }

                _active = value;

                ActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised whenever <see cref="Active"/> moved, in either direction.</summary>
        public event EventHandler? ActiveChanged;

        private readonly IKeystrokeCaptureService _capture;
        private readonly EventHandler _closedHandler;
        private readonly EventHandler _nestedClosedHandler;
        private readonly PropertyChangedEventHandler _sinkPropertyChangedHandler;
        private EditorOverlayViewModel? _active;
        private EditorOverlayViewModel? _nested;
        private EditorOverlayViewModel? _suspended;
        private IKeystrokeSink? _sink;
        private bool _isCaptureStarted;
        private bool _isCaptureSuspended;
        private bool _isClosed;

        /// <summary>Creates the host over the capture service it drives for the panels it shows.</summary>
        public EditorOverlayHost(IKeystrokeCaptureService capture)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _closedHandler = (_, _) => Clear();
            _nestedClosedHandler = (_, _) => RestoreSuspended();
            _sinkPropertyChangedHandler = (_, e) => OnSinkPropertyChanged(e);
        }

        /// <summary>
        /// Opens <paramref name="overlay"/>, replacing whatever was open. Any nesting in progress
        /// ends here: the panel opened this way becomes the entire overlay state, so a picker that
        /// is still up can no longer put its parent back afterwards.
        /// </summary>
        public void Show(EditorOverlayViewModel overlay)
        {
            ArgumentNullException.ThrowIfNull(overlay);

            DetachNested();
            ShowCore(overlay);
        }

        /// <summary>
        /// Opens <paramref name="child"/> over <paramref name="parent"/> and brings the parent
        /// back when the child closes, accepted or cancelled alike. Only §11.1's Search buttons
        /// need it: the picker they raise is a step inside the Tap and Hold panel, not a
        /// replacement for it.
        /// </summary>
        public void ShowNested(EditorOverlayViewModel child, EditorOverlayViewModel parent)
        {
            ArgumentNullException.ThrowIfNull(child);
            ArgumentNullException.ThrowIfNull(parent);

            DetachNested();
            ShowCore(child);

            if (!ReferenceEquals(Active, child))
            {
                // Refused (closed host, or an already-closed panel): nothing is up to nest under.
                return;
            }

            _nested = child;
            _suspended = parent;

            child.Closed += _nestedClosedHandler;
        }

        /// <summary>Cancels the open panel and drops it; a nested picker falls back to its parent.</summary>
        public void Dismiss()
        {
            var overlay = Active;

            if (overlay is null)
            {
                return;
            }

            overlay.Cancel();

            // Cancel raises Closed, which clears the property — and, for a nested picker, puts
            // its parent back in the same breath. Only a panel that did neither (one that had
            // already closed itself) still needs clearing by hand, and the reference check is
            // what keeps this from tearing down the restored parent.
            if (ReferenceEquals(Active, overlay))
            {
                Clear();
            }
        }

        /// <summary>
        /// Shuts the host down for good: no nesting is restored, the open panel is cancelled so
        /// its own teardown runs, and capture is left un-suspended. Safe to call multiple times.
        /// </summary>
        public void Close()
        {
            _isClosed = true;

            DetachNested();
            Dismiss();
        }

        private void ShowCore(EditorOverlayViewModel overlay)
        {
            if (_isClosed || overlay.IsClosed)
            {
                return;
            }

            Clear();

            Active = overlay;

            overlay.Closed += _closedHandler;

            // A sink panel — Tap and Hold — is fed by the capture service, and nothing else turns
            // it on: the editor cancelled its listening key on the way in. The panel says when it
            // is waiting for a keypress (an armed field) and capture follows that, so the rest of
            // the app keeps its keyboard while the panel merely sits there.
            if (overlay is IKeystrokeSink sink)
            {
                _sink = sink;

                overlay.PropertyChanged += _sinkPropertyChangedHandler;

                SyncSinkCapture();

                return;
            }

            // A panel that is not an IKeystrokeSink is a text-entry panel — Macro Timing Delays,
            // Search Keys, Export — and capture is explicitly suspended for as long as it is
            // open, per specs/10-apps-and-ui.md ("Capture is suspended … whenever a dialog that
            // needs real typing is open"). The Avalonia adapter also auto-suspends on TextBox
            // focus, but that is a platform judgement; this is the deterministic guarantee.
            _isCaptureSuspended = true;

            _capture.Suspend();
        }

        private void Clear()
        {
            var overlay = Active;

            if (overlay is null)
            {
                return;
            }

            overlay.Closed -= _closedHandler;

            DetachSink(overlay);

            Active = null;

            if (!_isCaptureSuspended)
            {
                return;
            }

            _isCaptureSuspended = false;

            _capture.Resume();
        }

        private void DetachSink(EditorOverlayViewModel overlay)
        {
            if (_sink is null)
            {
                return;
            }

            overlay.PropertyChanged -= _sinkPropertyChangedHandler;

            _sink = null;

            SyncSinkCapture();
        }

        private void OnSinkPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName is null or nameof(IKeystrokeSink.WantsKeystrokes))
            {
                SyncSinkCapture();
            }
        }

        /// <summary>
        /// Turns capture on while the open sink wants keystrokes and off again afterwards —
        /// but only what this host started: a macro recording may already own the service, and
        /// stopping that would silently deafen it.
        /// </summary>
        private void SyncSinkCapture()
        {
            if (_sink?.WantsKeystrokes == true)
            {
                if (_isCaptureStarted || _capture.IsCapturing)
                {
                    return;
                }

                _isCaptureStarted = true;

                _capture.Start();

                return;
            }

            if (!_isCaptureStarted)
            {
                return;
            }

            _isCaptureStarted = false;

            _capture.Stop();
        }

        private void RestoreSuspended()
        {
            var parent = _suspended;

            DetachNested();

            if (_isClosed || parent is null || parent.IsClosed)
            {
                return;
            }

            ShowCore(parent);
        }

        private void DetachNested()
        {
            if (_nested is not null)
            {
                _nested.Closed -= _nestedClosedHandler;
                _nested = null;
            }

            _suspended = null;
        }
    }
}
