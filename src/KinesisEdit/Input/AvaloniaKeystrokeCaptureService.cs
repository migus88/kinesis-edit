using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using KinesisEdit.Core.Input;

namespace KinesisEdit.Input
{
    /// <summary>
    /// The Avalonia adapter of <see cref="IKeystrokeCaptureService"/>: it previews the key events
    /// of one <see cref="TopLevel"/> and feeds them to a <see cref="KeystrokeCaptureSession"/>,
    /// which owns every decision. This class only translates events, judges where focus sits, and
    /// marshals the results — it holds no capture rules of its own.
    /// <para>
    /// Key handlers are attached in the <b>tunnel</b> phase with <c>handledEventsToo: true</c>, so
    /// the window sees a keystroke before any focused control does and can consume it with
    /// <c>e.Handled = true</c>. That reproduces the legacy contract of specs/10-apps-and-ui.md
    /// ("Keyboard-input capture"): on macOS "the main form previews all key events … and consumes
    /// the event". Note that the OS has already delivered the event by then — <c>Handled</c> stops
    /// the in-app route (and the derived text-input event), it does not unhook the OS.
    /// </para>
    /// <para>
    /// Because <see cref="KeyEventArgs.PhysicalKey"/> reports the physical position, the left/right
    /// modifier distinction and the keypad-Enter distinction come from the toolkit — no native
    /// interop and no macOS Accessibility permission are involved. Capture is therefore
    /// focused-window only: OS-reserved combinations (macOS Cmd+Tab/Cmd+Q/Cmd+Space, Windows
    /// Win+L) never reach the app and cannot be captured or swallowed.
    /// </para>
    /// </summary>
    public sealed class AvaloniaKeystrokeCaptureService : IKeystrokeCaptureService
    {
        /// <summary>
        /// Phases the focus handlers are attached for. <see cref="InputElement.GotFocusEvent"/>
        /// and <see cref="InputElement.LostFocusEvent"/> are registered as bubbling events, which
        /// is the phase that actually delivers them to the <see cref="TopLevel"/>; the tunnel flag
        /// is defensive, in case a future Avalonia adds that route.
        /// </summary>
        private const RoutingStrategies FocusRoutes = RoutingStrategies.Tunnel | RoutingStrategies.Bubble;

        /// <inheritdoc />
        public bool IsCapturing
        {
            get => _session.IsCapturing;
        }

        /// <inheritdoc />
        public bool IsSuspended
        {
            get => _session.IsSuspended;
        }

        /// <summary>Whether <see cref="Suspend"/> was called and not yet undone.</summary>
        public bool IsExplicitlySuspended
        {
            get => _session.IsExplicitlySuspended;
        }

        /// <summary>
        /// Whether focus landing in a <see cref="TextBox"/> auto-suspends capture. On by default:
        /// it is the safety net for text boxes hosted <i>in</i> the capturing window. Turning it
        /// off makes such a text box a swallow probe — with capture live, typing into it produces
        /// no text because the keystrokes are consumed before the box sees them.
        /// <para>
        /// <b>It is a safety net and never the answer on its own</b> (issue #122). The suspension is
        /// <em>silent</em>: nothing above this class is told, so a surface that arms capture and
        /// then lets focus reach a text box goes on claiming it is recording while the keystrokes
        /// go into the box — state that lies. A consumer that shows the user it is capturing must
        /// therefore stand <em>itself</em> down when focus enters a text input rather than lean on
        /// this; the keyboard editor does (<c>KeyboardEditorViewModel.StopRecordingOnInteraction</c>,
        /// docs/app/keystroke-capture.md). What this flag still guarantees is the deterministic part
        /// — that nothing is captured or swallowed while the caret is in a field.
        /// </para>
        /// </summary>
        public bool SuspendOnTextInputFocus
        {
            get => _suspendOnTextInputFocus;
            set
            {
                _suspendOnTextInputFocus = value;
                UpdateTextInputFocus();
            }
        }

        /// <inheritdoc />
        public event Action<CapturedKeystroke>? KeystrokeCaptured
        {
            add => _session.KeystrokeCaptured += value;
            remove => _session.KeystrokeCaptured -= value;
        }

        /// <summary>
        /// Raised whenever <see cref="IsCapturing"/> or <see cref="IsSuspended"/> change, so a UI
        /// can refresh its status the moment focus moves rather than on the next keypress.
        /// </summary>
        public event Action? StateChanged
        {
            add => _session.StateChanged += value;
            remove => _session.StateChanged -= value;
        }

        private readonly TopLevel _topLevel;
        private readonly KeystrokeCaptureSession _session = new();
        private readonly EventHandler<KeyEventArgs> _keyDownHandler;
        private readonly EventHandler<KeyEventArgs> _keyUpHandler;
        private readonly EventHandler<GotFocusEventArgs> _gotFocusHandler;
        private readonly EventHandler<RoutedEventArgs> _lostFocusHandler;

        private bool _suspendOnTextInputFocus = true;
        private bool _isDisposed;

        /// <summary>
        /// Creates a service that captures the key events of <paramref name="topLevel"/> — the
        /// window whose keystrokes are recorded. Nothing is attached until <see cref="Start"/>.
        /// </summary>
        public AvaloniaKeystrokeCaptureService(TopLevel topLevel)
        {
            ArgumentNullException.ThrowIfNull(topLevel);

            _topLevel = topLevel;
            _keyDownHandler = OnKeyDown;
            _keyUpHandler = OnKeyUp;
            _gotFocusHandler = OnGotFocus;
            _lostFocusHandler = OnLostFocus;
        }

        /// <inheritdoc />
        public void Start()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_session.IsCapturing)
            {
                return;
            }

            AttachHandlers();
            UpdateTextInputFocus();
            _session.Start();
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (_session.IsCapturing)
            {
                DetachHandlers();
            }

            // Always delegated, even when already stopped: the session clears an explicit
            // suspension here, so the next Start() cannot come up silently deaf.
            _session.Stop();
        }

        /// <inheritdoc />
        public void Suspend()
        {
            _session.Suspend();
        }

        /// <inheritdoc />
        public void Resume()
        {
            _session.Resume();
        }

        private void AttachHandlers()
        {
            _topLevel.AddHandler(InputElement.KeyDownEvent, _keyDownHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
            _topLevel.AddHandler(InputElement.KeyUpEvent, _keyUpHandler, RoutingStrategies.Tunnel, handledEventsToo: true);

            // Focus events are routed to the TopLevel from the element that gained or lost focus,
            // so the session learns about a focus move immediately instead of on the next keypress.
            _topLevel.AddHandler(InputElement.GotFocusEvent, _gotFocusHandler, FocusRoutes, handledEventsToo: true);
            _topLevel.AddHandler(InputElement.LostFocusEvent, _lostFocusHandler, FocusRoutes, handledEventsToo: true);
        }

        private void DetachHandlers()
        {
            _topLevel.RemoveHandler(InputElement.KeyDownEvent, _keyDownHandler);
            _topLevel.RemoveHandler(InputElement.KeyUpEvent, _keyUpHandler);
            _topLevel.RemoveHandler(InputElement.GotFocusEvent, _gotFocusHandler);
            _topLevel.RemoveHandler(InputElement.LostFocusEvent, _lostFocusHandler);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            HandleKeyEvent(e, KeyTransition.Down);
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            HandleKeyEvent(e, KeyTransition.Up);
        }

        private void OnGotFocus(object? sender, GotFocusEventArgs e)
        {
            UpdateTextInputFocus();
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            UpdateTextInputFocus();
        }

        private void HandleKeyEvent(KeyEventArgs e, KeyTransition transition)
        {
            UpdateTextInputFocus();

            var result = _session.Handle(AvaloniaPhysicalKeyBridge.Translate(e.PhysicalKey), transition);

            if (result.ShouldSwallow)
            {
                e.Handled = true;
            }
        }

        private void UpdateTextInputFocus()
        {
            _session.SetTextInputFocused(_suspendOnTextInputFocus && HasTextInputFocus());
        }

        /// <summary>
        /// Whether focus currently sits inside a text-entry control — the adapter's only platform
        /// judgement, pushed into the session, which owns what to do with it. Visual ancestors are
        /// walked as well, so focus landing on a <see cref="TextBox"/>'s inner presenter (or on a
        /// composite control that embeds one) counts too.
        /// </summary>
        private bool HasTextInputFocus()
        {
            if (_topLevel.FocusManager?.GetFocusedElement() is not Visual focused)
            {
                return false;
            }

            return focused.GetSelfAndVisualAncestors().Any(element => element is TextBox);
        }

        /// <summary>Detaches the key and focus handlers and stops capture. Idempotent.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Stop();
            _isDisposed = true;
        }
    }
}
