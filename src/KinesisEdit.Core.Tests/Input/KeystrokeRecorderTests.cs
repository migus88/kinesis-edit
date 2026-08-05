using KinesisEdit.Core.Input;

namespace KinesisEdit.Core.Tests.Input
{
    /// <summary>
    /// Pins the capture state machine of specs/10-apps-and-ui.md ("Keyboard-input capture") and
    /// specs/12-savant-elite.md §"Programming a pedal" step 3: modifier accumulation and
    /// release, auto-repeat suppression for every held key, swallowing (and the refusal to
    /// swallow a key that was never recorded), the Print Screen key-up rule with the modifiers
    /// sampled at its key-down, and the modifier-tapped-alone rule.
    /// </summary>
    public class KeystrokeRecorderTests
    {
        [Fact]
        public void Handle_WithLeftShiftHeldThenLetterDown_CapturesTheLetterWithThatModifier()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.True(result.ShouldSwallow);
            Assert.NotNull(result.Keystroke);
            Assert.Equal(65, result.Keystroke.Key.Code);
            Assert.Equal(PhysicalKeyCode.A, result.Keystroke.PhysicalKey);
            Assert.Equal(new[] { 160 }, result.Keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void Handle_WithRightShiftHeldThenLetterDown_CapturesTheRightShiftCode()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftRight, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.NotNull(result.Keystroke);
            Assert.Equal(new[] { 161 }, result.Keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void Handle_WithTwoModifiersHeld_CapturesBothInPressOrder()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ControlLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.AltRight, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.Delete, KeyTransition.Down);

            Assert.NotNull(result.Keystroke);
            Assert.Equal(new[] { 162, 165 }, result.Keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void Handle_WithModifierAutoRepeat_DoesNotDuplicateTheHeldModifier()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.Single(recorder.HeldModifiers);
            Assert.NotNull(result.Keystroke);
            Assert.Equal(new[] { 160 }, result.Keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void Handle_WithNonModifierAutoRepeat_CapturesItOnlyOnce()
        {
            var recorder = new KeystrokeRecorder();

            var first = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            var second = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            var third = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.NotNull(first.Keystroke);
            Assert.Equal(65, first.Keystroke.Key.Code);
            Assert.True(second.ShouldSwallow);
            Assert.Null(second.Keystroke);
            Assert.True(third.ShouldSwallow);
            Assert.Null(third.Keystroke);
        }

        [Fact]
        public void Handle_WithNonModifierPressedAgainAfterItsRelease_CapturesItTwice()
        {
            var recorder = new KeystrokeRecorder();

            var first = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Up);
            var second = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.NotNull(first.Keystroke);
            Assert.NotNull(second.Keystroke);
            Assert.Equal(65, second.Keystroke.Key.Code);
        }

        [Fact]
        public void Handle_WithModifierDown_AccumulatesItAndSwallowsWithoutCapturing()
        {
            var recorder = new KeystrokeRecorder();

            var result = recorder.Handle(PhysicalKeyCode.ControlLeft, KeyTransition.Down);

            Assert.True(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
            Assert.Equal(new[] { 162 }, recorder.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void Handle_WithModifierUp_ReleasesItFromTheHeldSet()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ControlLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.ControlLeft, KeyTransition.Up);

            Assert.Empty(recorder.HeldModifiers);
        }

        [Fact]
        public void Handle_WithModifierTappedAlone_CapturesThatModifierOnKeyUp()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.True(result.ShouldSwallow);
            Assert.NotNull(result.Keystroke);
            Assert.Equal(160, result.Keystroke.Key.Code);
            Assert.Equal(PhysicalKeyCode.ShiftLeft, result.Keystroke.PhysicalKey);
            Assert.Empty(result.Keystroke.HeldModifiers);
        }

        [Fact]
        public void Handle_WithModifierUsedInACombination_DoesNotCaptureItOnKeyUp()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Up);
            var result = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.True(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithModifierRepressedAfterACombination_CapturesItWhenTappedAlone()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Up);
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.NotNull(result.Keystroke);
            Assert.Equal(160, result.Keystroke.Key.Code);
        }

        [Fact]
        public void Handle_WithModifierAutoRepeatAfterACombination_DoesNotCaptureItOnKeyUp()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Up);

            // The modifier never came up, so this is auto-repeat, not a fresh press: it must not
            // clear the consumed flag and resurrect the modifier as a tapped-alone keystroke.
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.True(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithModifierAutoRepeatWhileAlone_StillCapturesItAsATap()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);

            // Auto-repeat of a lone modifier is not a chord — the key is only held "with itself",
            // so it still registers as tapped alone on key-up.
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.NotNull(result.Keystroke);
            Assert.Equal(160, result.Keystroke.Key.Code);
            Assert.Empty(result.Keystroke.HeldModifiers);
        }

        [Fact]
        public void Handle_WithModifierPressedWhileAnotherKeyIsHeld_DoesNotCaptureItOnKeyUp()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.True(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithModifierUpThatWasNeverHeld_PassesThroughWithoutCapturing()
        {
            var recorder = new KeystrokeRecorder();

            var result = recorder.Handle(PhysicalKeyCode.MetaLeft, KeyTransition.Up);

            Assert.False(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithKeyUpForAKeyThatWasNeverPressed_PassesThroughWithoutCapturing()
        {
            var recorder = new KeystrokeRecorder();

            var result = recorder.Handle(PhysicalKeyCode.Q, KeyTransition.Up);

            Assert.False(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithPrintScreenDown_SwallowsWithoutCapturing()
        {
            var recorder = new KeystrokeRecorder();

            var result = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Down);

            Assert.True(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithPrintScreenUp_CapturesItOnKeyUpOnly()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Up);

            Assert.True(result.ShouldSwallow);
            Assert.NotNull(result.Keystroke);
            Assert.Equal(0x2C, result.Keystroke.Key.Code);
            Assert.Equal(PhysicalKeyCode.PrintScreen, result.Keystroke.PhysicalKey);
        }

        [Fact]
        public void Handle_WithModifierHeldAndPrintScreenTapped_AttachesTheModifierAndConsumesIt()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.AltLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Down);
            var printScreenResult = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Up);
            var modifierResult = recorder.Handle(PhysicalKeyCode.AltLeft, KeyTransition.Up);

            Assert.NotNull(printScreenResult.Keystroke);
            Assert.Equal(new[] { 164 }, printScreenResult.Keystroke.HeldModifiers.Select(modifier => modifier.Code));
            Assert.Null(modifierResult.Keystroke);
        }

        [Fact]
        public void Handle_WithModifierReleasedBeforePrintScreenUp_KeepsTheModifierItWasPressedWith()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);
            var result = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Up);

            Assert.NotNull(result.Keystroke);
            Assert.Equal(0x2C, result.Keystroke.Key.Code);
            Assert.Equal(new[] { 160 }, result.Keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void Handle_WithPrintScreenAutoRepeat_CapturesItOnceOnKeyUp()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Down);
            var repeat = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Down);
            var release = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Up);
            var strayRelease = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Up);

            Assert.Null(repeat.Keystroke);
            Assert.NotNull(release.Keystroke);
            Assert.Null(strayRelease.Keystroke);
            Assert.False(strayRelease.ShouldSwallow);
        }

        [Fact]
        public void Handle_WithNonModifierKeyUp_SwallowsWithoutCapturing()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);
            var result = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Up);

            Assert.True(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.AudioVolumeMute, 173)]
        [InlineData(PhysicalKeyCode.MediaPlayPause, 179)]
        [InlineData(PhysicalKeyCode.Eject, 11150)]
        public void Handle_WithMediaKeyDown_CapturesAndSwallowsIt(PhysicalKeyCode key, int expectedCode)
        {
            var recorder = new KeystrokeRecorder();

            var result = recorder.Handle(key, KeyTransition.Down);

            Assert.True(result.ShouldSwallow);
            Assert.NotNull(result.Keystroke);
            Assert.Equal(expectedCode, result.Keystroke.Key.Code);
        }

        [Theory]
        [InlineData(KeyTransition.Down)]
        [InlineData(KeyTransition.Up)]
        public void Handle_WithUnresolvablePosition_PassesThroughWithoutCapturing(KeyTransition transition)
        {
            var recorder = new KeystrokeRecorder();

            var result = recorder.Handle((PhysicalKeyCode)9999, transition);

            Assert.False(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithNoneTransition_PassesThroughWithoutCapturing()
        {
            var recorder = new KeystrokeRecorder();

            var result = recorder.Handle(PhysicalKeyCode.A, KeyTransition.None);

            Assert.False(result.ShouldSwallow);
            Assert.Null(result.Keystroke);
        }

        [Fact]
        public void Handle_WithAbandonedModifierChord_CapturesNeitherModifier()
        {
            var recorder = new KeystrokeRecorder();

            // Ctrl+Shift held and then let go: neither modifier was tapped alone
            // (specs/12-savant-elite.md §"Programming a pedal" step 3), so neither registers.
            recorder.Handle(PhysicalKeyCode.ControlLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var shiftResult = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);
            var controlResult = recorder.Handle(PhysicalKeyCode.ControlLeft, KeyTransition.Up);

            Assert.True(shiftResult.ShouldSwallow);
            Assert.Null(shiftResult.Keystroke);
            Assert.True(controlResult.ShouldSwallow);
            Assert.Null(controlResult.Keystroke);
        }

        [Fact]
        public void Handle_WithUnresolvableKeyDownWhileAModifierIsHeld_ConsumesThatModifier()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var unknownDown = recorder.Handle((PhysicalKeyCode)9999, KeyTransition.Down);
            var unknownUp = recorder.Handle((PhysicalKeyCode)9999, KeyTransition.Up);
            var modifierResult = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.False(unknownDown.ShouldSwallow);
            Assert.False(unknownUp.ShouldSwallow);
            Assert.True(modifierResult.ShouldSwallow);
            Assert.Null(modifierResult.Keystroke);
        }

        [Fact]
        public void HeldModifiers_OnANewRecorder_IsEmpty()
        {
            var recorder = new KeystrokeRecorder();

            Assert.Empty(recorder.HeldModifiers);
        }

        [Fact]
        public void HeldModifiers_AfterReadingASnapshot_DoesNotReflectLaterPresses()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            var snapshot = recorder.HeldModifiers;
            recorder.Handle(PhysicalKeyCode.ControlLeft, KeyTransition.Down);

            Assert.Single(snapshot);
            Assert.Equal(2, recorder.HeldModifiers.Count);
        }

        [Fact]
        public void Reset_WithModifiersHeld_ClearsThem()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.ControlRight, KeyTransition.Down);
            recorder.Reset();

            Assert.Empty(recorder.HeldModifiers);
        }

        [Fact]
        public void Reset_AfterModifiersHeld_StopsThemLeakingIntoTheNextKeystroke()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Reset();
            var result = recorder.Handle(PhysicalKeyCode.A, KeyTransition.Down);

            Assert.NotNull(result.Keystroke);
            Assert.Empty(result.Keystroke.HeldModifiers);
        }

        [Fact]
        public void Reset_AfterAModifierWentDown_SuppressesItsTappedAloneKeystroke()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Reset();
            var result = recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Up);

            Assert.Null(result.Keystroke);
            Assert.False(result.ShouldSwallow);
        }

        [Fact]
        public void Reset_AfterPrintScreenWentDown_SuppressesItsKeyUpKeystroke()
        {
            var recorder = new KeystrokeRecorder();

            recorder.Handle(PhysicalKeyCode.ShiftLeft, KeyTransition.Down);
            recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Down);
            recorder.Reset();
            var result = recorder.Handle(PhysicalKeyCode.PrintScreen, KeyTransition.Up);

            Assert.Null(result.Keystroke);
            Assert.False(result.ShouldSwallow);
        }
    }
}
