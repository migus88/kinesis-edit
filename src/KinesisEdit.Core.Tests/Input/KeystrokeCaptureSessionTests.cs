using KinesisEdit.Core.Input;

namespace KinesisEdit.Core.Tests.Input
{
    /// <summary>
    /// Pins the capture/suspend gating of specs/10-apps-and-ui.md ("Keyboard-input capture"):
    /// a stopped or suspended session captures nothing and swallows nothing, suspension has two
    /// independent sources that <see cref="KeystrokeCaptureSession.IsSuspended"/> combines,
    /// stopping clears an explicit suspension, and every state transition is announced exactly
    /// once.
    /// </summary>
    public class KeystrokeCaptureSessionTests
    {
        [Fact]
        public void Handle_OnANotStartedSession_PassesThroughWithoutEmittingOrSwallowing()
        {
            var session = new KeystrokeCaptureSession();
            var captured = new List<CapturedKeystroke>();
            session.KeystrokeCaptured += captured.Add;

            var result = session.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.False(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
            Assert.Empty(captured);
        }

        [Fact]
        public void Handle_OnAnExplicitlySuspendedSession_PassesThroughWithoutEmittingOrSwallowing()
        {
            var session = new KeystrokeCaptureSession();
            var captured = new List<CapturedKeystroke>();
            session.KeystrokeCaptured += captured.Add;

            session.Start();
            session.Suspend();
            var result = session.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.False(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
            Assert.Empty(captured);
        }

        [Fact]
        public void Handle_WithTextInputFocused_PassesThroughWithoutEmittingOrSwallowing()
        {
            var session = new KeystrokeCaptureSession();
            var captured = new List<CapturedKeystroke>();
            session.KeystrokeCaptured += captured.Add;

            session.Start();
            session.SetTextInputFocused(true);
            var result = session.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.False(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
            Assert.Empty(captured);
        }

        [Fact]
        public void Handle_WhileCapturing_SwallowsAndRaisesKeystrokeCapturedOncePerKeystroke()
        {
            var session = new KeystrokeCaptureSession();
            var captured = new List<CapturedKeystroke>();
            session.KeystrokeCaptured += captured.Add;

            session.Start();
            var result = session.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            session.Handle(PhysicalKeyCode.A, KeyTransition.Up);

            Assert.True(result.ShouldSwallow);
            Assert.NotNull(result.Keystroke);
            Assert.Single(captured);
            Assert.Equal(65, captured[0].Key.Code);
        }

        [Fact]
        public void Handle_AfterResumingATextInputSuspension_CapturesAgain()
        {
            var session = new KeystrokeCaptureSession();

            session.Start();
            session.SetTextInputFocused(true);
            session.SetTextInputFocused(false);
            var result = session.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.True(result.ShouldSwallow);
            Assert.NotNull(result.Keystroke);
        }

        [Fact]
        public void IsSuspended_WithEitherSuspensionSource_IsTrueAndOnlyFalseWhenNeitherApplies()
        {
            var session = new KeystrokeCaptureSession();

            session.Start();
            Assert.False(session.IsSuspended);

            session.Suspend();
            Assert.True(session.IsSuspended);

            session.SetTextInputFocused(true);
            Assert.True(session.IsSuspended);

            session.Resume();
            Assert.True(session.IsSuspended);

            session.SetTextInputFocused(false);
            Assert.False(session.IsSuspended);
        }

        [Fact]
        public void Start_AfterSuspendAndStop_LeavesTheSessionCapturingAndNotSuspended()
        {
            var session = new KeystrokeCaptureSession();

            session.Suspend();
            session.Stop();
            session.Start();

            Assert.True(session.IsCapturing);
            Assert.False(session.IsSuspended);
            Assert.True(session.Handle(PhysicalKeyCode.A, KeyTransition.Down).ShouldSwallow);
        }

        [Fact]
        public void Start_WhenAlreadyCapturing_IsANoOp()
        {
            var session = new KeystrokeCaptureSession();
            var stateChanges = 0;

            session.Start();
            session.StateChanged += () => stateChanges++;
            session.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            session.Start();

            Assert.True(session.IsCapturing);
            Assert.Equal(0, stateChanges);
            Assert.Single(session.HeldModifiers);
        }

        [Fact]
        public void Stop_WhenAlreadyStopped_IsANoOp()
        {
            var session = new KeystrokeCaptureSession();
            var stateChanges = 0;
            session.StateChanged += () => stateChanges++;

            session.Stop();
            session.Stop();

            Assert.False(session.IsCapturing);
            Assert.Equal(0, stateChanges);
        }

        [Fact]
        public void Suspend_WhenAlreadySuspended_IsANoOp()
        {
            var session = new KeystrokeCaptureSession();
            var stateChanges = 0;

            session.Start();
            session.Suspend();
            session.StateChanged += () => stateChanges++;
            session.Suspend();

            Assert.True(session.IsSuspended);
            Assert.Equal(0, stateChanges);
        }

        [Fact]
        public void Resume_WhenNotSuspended_IsANoOp()
        {
            var session = new KeystrokeCaptureSession();
            var stateChanges = 0;

            session.Start();
            session.StateChanged += () => stateChanges++;
            session.Resume();

            Assert.False(session.IsSuspended);
            Assert.True(session.IsCapturing);
            Assert.Equal(0, stateChanges);
        }

        [Fact]
        public void Resume_OnAStoppedSession_DoesNotStartCapturing()
        {
            var session = new KeystrokeCaptureSession();

            session.Suspend();
            session.Resume();

            Assert.False(session.IsCapturing);
            Assert.False(session.IsSuspended);
            Assert.False(session.Handle(PhysicalKeyCode.A, KeyTransition.Down).ShouldSwallow);
        }

        [Fact]
        public void Stop_WithModifiersHeld_ClearsThem()
        {
            var session = new KeystrokeCaptureSession();

            session.Start();
            session.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            session.Stop();

            Assert.Empty(session.HeldModifiers);
        }

        [Fact]
        public void Suspend_WithModifiersHeld_ClearsThem()
        {
            var session = new KeystrokeCaptureSession();

            session.Start();
            session.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            session.Suspend();

            Assert.Empty(session.HeldModifiers);
        }

        [Fact]
        public void SetTextInputFocused_WithModifiersHeld_ClearsThem()
        {
            var session = new KeystrokeCaptureSession();

            session.Start();
            session.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            session.SetTextInputFocused(true);

            Assert.Empty(session.HeldModifiers);
        }

        [Fact]
        public void StateChanged_OnEveryRealTransition_IsRaisedOnce()
        {
            var session = new KeystrokeCaptureSession();
            var stateChanges = 0;
            session.StateChanged += () => stateChanges++;

            session.Start();
            Assert.Equal(1, stateChanges);

            session.Suspend();
            Assert.Equal(2, stateChanges);

            session.Resume();
            Assert.Equal(3, stateChanges);

            session.SetTextInputFocused(true);
            Assert.Equal(4, stateChanges);

            session.SetTextInputFocused(false);
            Assert.Equal(5, stateChanges);

            session.Stop();
            Assert.Equal(6, stateChanges);
        }

        [Fact]
        public void StateChanged_WhenTheObservableStateDoesNotMove_IsNotRaised()
        {
            var session = new KeystrokeCaptureSession();
            var stateChanges = 0;

            session.Start();
            session.SetTextInputFocused(true);
            session.Suspend();
            session.StateChanged += () => stateChanges++;

            // Already suspended by the text-input focus, so clearing the explicit flag changes
            // nothing an observer can see.
            session.Resume();
            session.SetTextInputFocused(true);

            Assert.Equal(0, stateChanges);
            Assert.True(session.IsSuspended);
        }
    }
}
