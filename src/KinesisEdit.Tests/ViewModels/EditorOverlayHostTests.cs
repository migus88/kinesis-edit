using KinesisEdit.Core.Input;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The overlay host's own contract. The everyday paths — replace, suspend/resume, dismiss —
    /// are exercised end to end through <c>KeyboardEditorViewModel</c>; what is pinned here is the
    /// edges that never occur through the editor: bad arguments, a panel that is already closed,
    /// and a host that has been shut down.
    /// <para>
    /// <b>There is nothing about nesting left to test.</b> It had one consumer — §11.1's two Search
    /// buttons over the Tap and Hold modal — and that modal is a key inspector panel now, hosting
    /// its picker inline (issue #92). The host lost <c>ShowNested</c> with it rather than keeping
    /// tested dead code.
    /// </para>
    /// </summary>
    public sealed class EditorOverlayHostTests : IDisposable
    {
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly EditorOverlayHost _host;

        public EditorOverlayHostTests()
        {
            _host = new EditorOverlayHost(_capture);
        }

        [Fact]
        public void ActiveChanged_FiresOncePerTransition()
        {
            var observed = new List<EditorOverlayViewModel?>();
            var first = new StubOverlay();
            var second = new StubOverlay();

            _host.ActiveChanged += (_, _) => observed.Add(_host.Active);

            _host.Show(first);

            // Replacing goes through null: the outgoing panel is cleared before the incoming one
            // is hosted, which is what keeps the capture suspension balanced.
            _host.Show(second);
            _host.Dismiss();

            Assert.Equal([first, null, second, null], observed);
            Assert.Null(_host.Active);
        }

        [Fact]
        public void Show_WithAnAlreadyClosedPanel_OpensNothingAndLeavesTheOpenOneAlone()
        {
            var open = new StubOverlay();
            var closed = new StubOverlay();

            closed.Cancel();

            _host.Show(open);
            _host.Show(closed);

            Assert.Same(open, _host.Active);
        }

        [Fact]
        public void Close_IsIdempotentAndRefusesEverythingAfterwards()
        {
            _host.Show(new StubOverlay());

            _host.Close();
            _host.Close();

            Assert.Null(_host.Active);
            Assert.False(_capture.IsSuspended);
            Assert.Equal(1, _capture.SuspendCount);
            Assert.Equal(1, _capture.ResumeCount);

            _host.Show(new StubOverlay());

            Assert.Null(_host.Active);
        }

        [Fact]
        public void Dismiss_WithNothingOpen_DoesNothing()
        {
            _host.Dismiss();

            Assert.Null(_host.Active);
            Assert.Equal(0, _capture.ResumeCount);
        }

        [Fact]
        public void Constructor_AndShow_RejectMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new EditorOverlayHost(null!));
            Assert.Throws<ArgumentNullException>(() => _host.Show(null!));
        }

        [Fact]
        public void Show_WithAKeystrokeSinkPanel_NeverSuspendsCapture()
        {
            _host.Show(new SinkOverlay());

            Assert.Equal(0, _capture.SuspendCount);

            _host.Dismiss();

            Assert.Equal(0, _capture.ResumeCount);
        }

        [Fact]
        public void Show_WithASinkPanelWaitingForAKeypress_StartsCaptureAndStopsItAgain()
        {
            // Nothing else turns capture on for a feature panel: the editor cancelled its
            // listening key on the way in, so an armed Tap and Hold field would be deaf.
            _host.Show(new SinkOverlay());

            Assert.Equal(1, _capture.StartCount);
            Assert.True(_capture.IsCapturing);

            _host.Dismiss();

            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public void Show_WithASinkPanelThatArmsLater_FollowsItsWantsKeystrokes()
        {
            var overlay = new SinkOverlay { WantsKeystrokes = false };

            _host.Show(overlay);

            // An idle panel leaves the keyboard to the rest of the app.
            Assert.Equal(0, _capture.StartCount);

            overlay.WantsKeystrokes = true;

            Assert.Equal(1, _capture.StartCount);
            Assert.True(_capture.IsCapturing);

            overlay.WantsKeystrokes = false;

            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public void Show_WithASinkPanelWhileSomethingElseCaptures_LeavesTheRunningCaptureAlone()
        {
            // A recording macro owns the service; stopping it here would silently deafen it.
            _capture.Start();

            var overlay = new SinkOverlay();

            _host.Show(overlay);

            Assert.Equal(1, _capture.StartCount);

            _host.Dismiss();

            Assert.Equal(0, _capture.StopCount);
            Assert.True(_capture.IsCapturing);
        }

        [Fact]
        public void Close_WithASinkPanelWaitingForAKeypress_StopsTheCaptureItStarted()
        {
            _host.Show(new SinkOverlay());

            _host.Close();

            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.IsCapturing);
        }

        public void Dispose()
        {
            _capture.Dispose();
        }

        /// <summary>A text-entry feature panel — Delays, Search Keys, Export — and no sink.</summary>
        private sealed class StubOverlay : EditorOverlayViewModel
        {
            public StubOverlay() : base("Stub")
            {
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }

        /// <summary>A feature panel that consumes captured keystrokes — the Tap and Hold shape.</summary>
        private sealed class SinkOverlay : EditorOverlayViewModel, IKeystrokeSink
        {
            public bool WantsKeystrokes
            {
                get => _wantsKeystrokes;
                set => SetProperty(ref _wantsKeystrokes, value);
            }

            private bool _wantsKeystrokes = true;

            public SinkOverlay() : base("Sink")
            {
            }

            public void ReceiveKeystroke(CapturedKeystroke keystroke)
            {
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }
    }
}
