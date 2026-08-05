using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using KinesisEdit.Core.Input;
using KinesisEdit.Input;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The keystroke-capture spike harness of issue #12: it drives an
    /// <see cref="AvaloniaKeystrokeCaptureService"/> over this window and logs every captured
    /// keystroke with its key-table code, the Legacy/Gen1/Gen2 tokens and the held modifiers, so
    /// the capture contract of specs/10-apps-and-ui.md can be checked by hand — including the
    /// swallowing itself, by unticking auto-suspend and typing into the probe text box. It is not
    /// the app's window: <see cref="MainWindow"/> is the shell, and this harness opens in its place
    /// only when the app is started with the <c>--keystroke-spike</c> argument (see
    /// docs/app/keystroke-capture.md). Plain code-behind on purpose — the project has no MVVM
    /// framework, and this window is a throwaway harness that the per-device editor UIs will
    /// replace. It holds no capture rules: every state question is asked of the service.
    /// </summary>
    public partial class KeystrokeCaptureSpikeWindow : Window
    {
        private const string StoppedStatus = "Stopped";
        private const string CapturingStatus = "Capturing";
        private const string ManuallySuspendedStatus = "Suspended (manual)";
        private const string TextEntrySuspendedStatus = "Suspended (text entry focused)";
        private const string StartCaptureCaption = "Start capture";
        private const string StopCaptureCaption = "Stop capture";
        private const string SuspendCaption = "Suspend";
        private const string ResumeCaption = "Resume";

        private readonly ObservableCollection<CapturedKeystrokeView> _keystrokes = new();
        private readonly AvaloniaKeystrokeCaptureService _captureService;

        /// <summary>Builds the harness window and wires it to a capture service over itself.</summary>
        public KeystrokeCaptureSpikeWindow()
        {
            InitializeComponent();

            _captureService = new AvaloniaKeystrokeCaptureService(this);
            _captureService.KeystrokeCaptured += OnKeystrokeCaptured;
            _captureService.StateChanged += OnCaptureStateChanged;

            // Subscribed here rather than in the markup: the service must exist before the
            // check box can report its state to it.
            AutoSuspendCheckBox.IsChecked = _captureService.SuspendOnTextInputFocus;
            AutoSuspendCheckBox.IsCheckedChanged += OnAutoSuspendChanged;

            KeystrokeList.ItemsSource = _keystrokes;
            UpdateStatus();
        }

        /// <inheritdoc />
        protected override void OnClosed(EventArgs e)
        {
            AutoSuspendCheckBox.IsCheckedChanged -= OnAutoSuspendChanged;
            _captureService.KeystrokeCaptured -= OnKeystrokeCaptured;
            _captureService.StateChanged -= OnCaptureStateChanged;
            _captureService.Dispose();

            base.OnClosed(e);
        }

        private void OnCaptureToggleClick(object? sender, RoutedEventArgs e)
        {
            if (_captureService.IsCapturing)
            {
                _captureService.Stop();
            }
            else
            {
                _captureService.Start();
            }

            UpdateStatus();
        }

        private void OnSuspendToggleClick(object? sender, RoutedEventArgs e)
        {
            if (_captureService.IsExplicitlySuspended)
            {
                _captureService.Resume();
            }
            else
            {
                _captureService.Suspend();
            }

            UpdateStatus();
        }

        private void OnAutoSuspendChanged(object? sender, RoutedEventArgs e)
        {
            _captureService.SuspendOnTextInputFocus = AutoSuspendCheckBox.IsChecked == true;

            UpdateStatus();
        }

        private void OnClearClick(object? sender, RoutedEventArgs e)
        {
            _keystrokes.Clear();
        }

        private void OnCaptureStateChanged()
        {
            UpdateStatus();
        }

        private void OnKeystrokeCaptured(CapturedKeystroke keystroke)
        {
            // Newest first: the most recent keystroke is the one being inspected.
            _keystrokes.Insert(0, new CapturedKeystrokeView(keystroke));
        }

        private void UpdateStatus()
        {
            CaptureToggleButton.Content = _captureService.IsCapturing ? StopCaptureCaption : StartCaptureCaption;
            SuspendToggleButton.Content = _captureService.IsExplicitlySuspended ? ResumeCaption : SuspendCaption;
            StatusText.Text = DescribeState();
        }

        private string DescribeState()
        {
            if (!_captureService.IsCapturing)
            {
                return StoppedStatus;
            }

            if (_captureService.IsExplicitlySuspended)
            {
                return ManuallySuspendedStatus;
            }

            return _captureService.IsSuspended ? TextEntrySuspendedStatus : CapturingStatus;
        }
    }
}
