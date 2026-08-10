using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The composer's <b>delay</b> section (issue #139) — <c>then wait</c>, a
    /// <c>fixed</c> / <c>random</c> segment and a millisecond field — and the place
    /// specs/11-feature-dialogs.md §11.3's <c>Macro Timing Delays</c> now lives.
    ///
    /// <para><b>§11.3's rules are unchanged; its third surface is gone.</b> Until #139 a delay was
    /// edited in a per-row editor of its own, opened by a <c>delay…</c> affordance and closed by
    /// <c>Set delay</c> / <c>No delay</c> / <c>Done</c> — a third way to change a macro on a panel
    /// that already had two. Everything the spec pins survives the move: the tokens are Core's
    /// (<see cref="MacroDelayTokens"/>), resolution goes <b>through the token and never through the
    /// code</b>, the range is 1-999 with the arrows clamped and a typed value validated, and
    /// <c>FirmwareFeature.CustomMacroDelays</c> is answered <b>in place</b> with 09 §2's own words
    /// plus an <c>Update Firmware</c> button. What changed is that there is no <c>Set delay</c>: the
    /// choice writes the moment it is made, like everything else in the composer.</para>
    ///
    /// <para><b>A delay-only row keeps its delay editable and nothing else</b> (06 §2.2 lets a macro
    /// open with a delay, or hold two in a row, and dropping such a row would be editing the file
    /// behind the user's back). On such a row this section is the only live part of the composer.</para>
    ///
    /// <para>Split from <c>MacroInspectorPanelViewModel.Composer.cs</c> rather than appended to it,
    /// per docs/guides/Coding Conventions.md: the composer is four independent controls over one
    /// selection, and the delay is the one with rules of its own.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel
    {
        /// <summary>
        /// The label <b>beside</b> the delay segment, in the step row's own voice. It was the
        /// section caption <c>THEN WAIT</c> until issue #146 folded the composer into two rows: the
        /// mock draws no section labels there at all, and this is the one whose words the row still
        /// needs — <c>fixed</c> / <c>random</c> says nothing on its own about what is being waited
        /// for. Lower case because it is now a phrase in a sentence rather than a heading over a
        /// block.
        /// </summary>
        public const string StepDelayLabel = "then wait";

        /// <summary>The segment for §11.3's custom 1-999 ms delay. This app's wording.</summary>
        public const string FixedDelayCaption = "fixed";

        /// <summary>
        /// The <b>two</b> delay states the strip draws, in that order (issue #148).
        ///
        /// <para><b><see cref="MacroStepDelayMode.None"/> is deliberately not among them, and is
        /// still a real state of the step.</b> The designer's compose bar draws <c>fixed</c> /
        /// <c>random</c> and nothing else; a step that waits for neither simply lights no segment
        /// and shows an empty field. What took the third segment's job is the field itself —
        /// <b>emptying it clears the delay</b> (see <see cref="ApplyStepDelayText"/>), which is the
        /// same write <c>none</c> used to make, from the control the number was going to come from
        /// anyway.</para>
        /// </summary>
        public static IReadOnlyList<MacroStepDelayMode> StepDelayModes { get; } =
        [
            MacroStepDelayMode.Fixed,
            MacroStepDelayMode.Random
        ];

        /// <summary>
        /// Whether the delay section may be touched: a selected step that really exists (not the
        /// <c>＋</c> placeholder, which has no step to put a delay behind yet) and a firmware that
        /// clears 09 §2's gate. A refused gate draws <c>Steps.DelayUnavailableReason</c> in place of
        /// the controls rather than hiding them — the sanctioned exception to "absent features are
        /// not shown, not disabled", because the feature is not absent, the firmware is old.
        /// </summary>
        public bool IsStepDelayEnabled => _isStepDelayEnabled;

        /// <summary>Whether the millisecond field is live — the <c>fixed</c> segment is the chosen one.</summary>
        public bool IsCustomStepDelay => _isCustomStepDelay;

        /// <summary>The two segments as they currently stand; replaced whole on every change.</summary>
        public IReadOnlyList<MacroStepDelayOption> StepDelayOptions
        {
            get => _stepDelayOptions;
            private set => SetProperty(ref _stepDelayOptions, value);
        }

        /// <summary>
        /// The custom delay in milliseconds; 0 means the field is empty. Deliberately
        /// <b>unclamped</b> on assignment — §11.3 clamps the arrows and validates what is typed, so
        /// an out-of-range value has to survive long enough to be reported. A valid value writes
        /// immediately, which is also what takes the choice off <c>random</c>.
        /// </summary>
        public int StepDelayMilliseconds
        {
            get => _stepDelayMilliseconds;
            set => ApplyStepDelayMilliseconds(value);
        }

        /// <summary>
        /// The millisecond field's text. <see cref="StepDelayMilliseconds"/> spells "empty" as 0 —
        /// a sentinel that is not itself a legal delay, since §11.3's range is 1-999 — so binding the
        /// int straight to the box drew that sentinel as a literal <c>0</c>, a number the field will
        /// not accept if you type it.
        ///
        /// <para>That was survivable while the delay editor <em>opened over a row on demand</em>: you
        /// saw it only when you meant to edit a delay. Since #139 the section is on screen for every
        /// selected step, so the wrong <c>0</c> would sit under every step that has no delay — which a
        /// captured frame is exactly how you notice. Empty text is the honest rendering of "none yet",
        /// and it is what the placeholder-free field showed in §11.3's own modal.</para>
        ///
        /// <para><b>Since issue #148 emptying it is also how a delay is taken off.</b> The strip lost
        /// its <c>none</c> segment to the designer's mock, so this field carries that write: blank
        /// clears, a number writes, and anything else is a rejected input.</para>
        /// </summary>
        public string StepDelayText
        {
            get => _stepDelayMilliseconds > 0
                ? _stepDelayMilliseconds.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            set => ApplyStepDelayText(value);
        }

        /// <summary>Lowest custom delay §11.3 accepts (1 ms), read off Core's own token rules.</summary>
        public int MinimumDelayMilliseconds => Steps.MinimumDelayMilliseconds;

        /// <summary>Highest custom delay §11.3 accepts (999 ms).</summary>
        public int MaximumDelayMilliseconds => Steps.MaximumDelayMilliseconds;

        /// <summary>§11.3's validation message while the typed value is unusable, else empty.</summary>
        public string StepDelayError
        {
            get => _stepDelayError;
            private set
            {
                if (SetProperty(ref _stepDelayError, value))
                {
                    OnPropertyChanged(nameof(HasStepDelayError));
                }
            }
        }

        /// <summary>Whether there is a validation message to draw.</summary>
        public bool HasStepDelayError => _stepDelayError.Length > 0;

        /// <summary>Writes the chosen delay state onto the selected step.</summary>
        public IRelayCommand<MacroStepDelayOption> SetStepDelayModeCommand { get; private set; } = null!;

        // THE MILLISECOND FIELD HAS NO ARROWS ANY MORE (issue #146). `IncreaseStepDelayCommand` and
        // `DecreaseStepDelayCommand` were the `+`/`-` pair beside it; the redesigned compose bar
        // draws a bare field, so the pair had no call site left and unbound commands are API nobody
        // can reach. The one job they did that typing could not — getting from "no delay" to a fixed
        // one, because the first click clamped 0 up into §11.3's range — was already taken over by
        // `fixed` latching and arming the field without writing (see `SetStepDelayMode`), which is
        // what made a fixed delay authorable at all.

        private IReadOnlyList<MacroStepDelayOption> _stepDelayOptions = [];
        private string _stepDelayError = string.Empty;
        private int _stepDelayMilliseconds;
        private bool _isStepDelayEnabled;
        private bool _isCustomStepDelay;

        /// <summary>Builds the delay section. Called from <c>CreateComposer</c>.</summary>
        private void CreateComposerDelay()
        {
            SetStepDelayModeCommand = new RelayCommand<MacroStepDelayOption>(SetStepDelayMode, CanSetStepDelayMode);
        }

        /// <summary>
        /// Re-reads the section off <paramref name="step"/>. A pure read: the millisecond field is
        /// seeded through its backing field, never through the property, because the property writes.
        /// </summary>
        private void ReadComposerDelayFromSelection(MacroInspectorStepViewModel? step, bool isComposerEnabled)
        {
            var mode = ReadDelayMode(step);

            _isStepDelayEnabled = isComposerEnabled && Steps.AreDelaysAvailable && step?.IsPlaceholder != true;
            _isCustomStepDelay = _isStepDelayEnabled && mode == MacroStepDelayMode.Fixed;

            SetProperty(ref _stepDelayMilliseconds, step?.DelayMilliseconds ?? 0, nameof(StepDelayMilliseconds));

            OnPropertyChanged(nameof(StepDelayText));

            StepDelayError = string.Empty;
            StepDelayOptions = BuildStepDelayOptions(mode, _isStepDelayEnabled);

            OnPropertyChanged(nameof(IsStepDelayEnabled));
            OnPropertyChanged(nameof(IsCustomStepDelay));
        }

        private void RefreshComposerDelayCommands()
        {
            SetStepDelayModeCommand.NotifyCanExecuteChanged();
        }

        private static MacroStepDelayMode ReadDelayMode(MacroInspectorStepViewModel? step)
        {
            if (step is null || !step.HasDelay)
            {
                return MacroStepDelayMode.None;
            }

            return step.IsRandomDelay ? MacroStepDelayMode.Random : MacroStepDelayMode.Fixed;
        }

        private static IReadOnlyList<MacroStepDelayOption> BuildStepDelayOptions(
            MacroStepDelayMode current,
            bool isEnabled)
        {
            var options = new List<MacroStepDelayOption>(StepDelayModes.Count);

            foreach (var mode in StepDelayModes)
            {
                options.Add(new MacroStepDelayOption(mode, mode == current, isEnabled));
            }

            return options;
        }

        private bool CanSetStepDelayMode(MacroStepDelayOption? option)
        {
            return option is not null && IsStepDelayEnabled;
        }

        /// <summary>
        /// Writes the chosen state. <c>fixed</c> with an empty or out-of-range field reports §11.3's
        /// own message and writes nothing — the spec's "random, 1..999, or invalid" outcome, kept
        /// intact by a control that has no <c>Ok</c> to fail on.
        ///
        /// <para><b>The segment latches on the press, before anything is written.</b> The choice used
        /// to be read back off the step alone, which closed a loop: on a step carrying no delay the
        /// field is 0, so pressing <c>fixed</c> failed validation and wrote nothing, so the step still
        /// reported "no delay", so <c>fixed</c> never latched and the field it arms never came alive —
        /// a fixed delay was unauthorable. The press is an <em>intent</em>, and the only place the
        /// number can come from is the field it arms, so it must survive not having one yet.</para>
        /// </summary>
        private void SetStepDelayMode(MacroStepDelayOption? option)
        {
            if (!CanSetStepDelayMode(option))
            {
                return;
            }

            SetStepDelayChoice(option!.Mode);

            if (option.Mode == MacroStepDelayMode.Random)
            {
                StepDelayError = string.Empty;

                Steps.TrySetSelectedDelay(MacroInspectorDelay.Random);

                return;
            }

            // `fixed` with nothing to write yet is not a refusal — it is an empty field with §11.3's
            // own message beside it, which the first usable number clears by writing.
            if (MacroDelayTokens.IsValidDelay(_stepDelayMilliseconds))
            {
                WriteCustomStepDelay(_stepDelayMilliseconds);
            }
            else
            {
                StepDelayError = MacroInspectorStepsViewModel.InvalidDelayMessage;
            }
        }

        /// <summary>
        /// Latches one segment and arms the millisecond field with it, without writing. Called on the
        /// press and again whenever a write lands, so the segment agrees with the step however the
        /// delay was reached — pressing <c>fixed</c>, or simply typing a number.
        /// </summary>
        private void SetStepDelayChoice(MacroStepDelayMode mode)
        {
            _isCustomStepDelay = _isStepDelayEnabled && mode == MacroStepDelayMode.Fixed;

            StepDelayOptions = BuildStepDelayOptions(mode, _isStepDelayEnabled);

            OnPropertyChanged(nameof(IsCustomStepDelay));
        }

        /// <summary>
        /// Takes what was typed, and answers it in <b>three</b> ways rather than two (issue #148).
        /// The field is the only control the delay has left besides the two segments, so it has to
        /// carry the write the deleted <c>none</c> segment used to make — and that write cannot be
        /// spelled as "unusable", or a rejected input would silently erase a good delay.
        /// <list type="bullet">
        /// <item><b>Blank</b> (or whitespace) is <em>no delay</em>: it clears the step, exactly as
        /// <c>none</c> did, and reports nothing.</item>
        /// <item><b>Not a number</b> is a rejected input: §11.3's message, and neither the step nor
        /// the field's own value moves, so what was typed stays on screen to be corrected.</item>
        /// <item><b>A number</b> goes on to <see cref="ApplyStepDelayMilliseconds"/>, which writes
        /// it or refuses it against §11.3's 1-999.</item>
        /// </list>
        /// </summary>
        private void ApplyStepDelayText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ClearStepDelay();

                return;
            }

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                RejectStepDelay();

                return;
            }

            ApplyStepDelayMilliseconds(value);
        }

        /// <summary>
        /// Takes a millisecond count. <b>0 is the empty field</b> — the sentinel
        /// <see cref="StepDelayText"/> already draws as blank, and §11.3's range starts at 1 — so it
        /// clears the delay rather than reporting itself; every other value outside 1-999 is a
        /// rejected input and writes nothing. It <b>validates whether or not the value moved</b>, but
        /// only <b>writes</b> when it really changed, so re-typing the number already on the step
        /// cannot dirty the profile.
        /// </summary>
        private void ApplyStepDelayMilliseconds(int value)
        {
            if (value == 0)
            {
                ClearStepDelay();

                return;
            }

            var moved = SetProperty(ref _stepDelayMilliseconds, value, nameof(StepDelayMilliseconds));

            if (moved)
            {
                OnPropertyChanged(nameof(StepDelayText));
            }

            if (!IsStepDelayEnabled)
            {
                return;
            }

            if (!MacroDelayTokens.IsValidDelay(value))
            {
                StepDelayError = MacroInspectorStepsViewModel.InvalidDelayMessage;

                return;
            }

            StepDelayError = string.Empty;

            if (moved)
            {
                WriteCustomStepDelay(value);
            }
        }

        /// <summary>
        /// Takes the delay off the selected step — the write the <c>none</c> segment used to make.
        /// The strip is left with <b>neither</b> segment lit, because "no delay" is a state of the
        /// step and no longer a choice on the strip.
        ///
        /// <para>On a <b>delay-only</b> row (06 §2.2) this drops the row, exactly as <c>none</c> did:
        /// a row that was nothing but a delay has nothing left once the delay goes
        /// (<see cref="MacroInspectorStepsViewModel.TrySetSelectedDelay"/>).</para>
        /// </summary>
        private void ClearStepDelay()
        {
            if (SetProperty(ref _stepDelayMilliseconds, 0, nameof(StepDelayMilliseconds)))
            {
                OnPropertyChanged(nameof(StepDelayText));
            }

            if (!IsStepDelayEnabled)
            {
                return;
            }

            StepDelayError = string.Empty;

            Steps.TrySetSelectedDelay(MacroInspectorDelay.None);

            SetStepDelayChoice(MacroStepDelayMode.None);
        }

        /// <summary>
        /// Reports §11.3's message for something that is not a number at all, and <b>touches nothing
        /// else</b> — not the step, not the field's value and not the strip. A rejected input is the
        /// panel's one error ramp; it must never be mistaken for the emptied field that clears.
        /// </summary>
        private void RejectStepDelay()
        {
            if (!IsStepDelayEnabled)
            {
                return;
            }

            StepDelayError = MacroInspectorStepsViewModel.InvalidDelayMessage;
        }

        /// <summary>
        /// Writes a custom delay of <paramref name="milliseconds"/>, or reports §11.3's refusal. The
        /// key is resolved by <see cref="MacroInspectorStepsViewModel.TrySetSelectedDelay"/> through
        /// the <b>token</b>, never the code — <c>dran</c> and the generated <c>d002</c> share code
        /// 10087 (05 §7).
        /// </summary>
        private void WriteCustomStepDelay(int milliseconds)
        {
            if (!MacroDelayTokens.IsValidDelay(milliseconds))
            {
                StepDelayError = MacroInspectorStepsViewModel.InvalidDelayMessage;

                return;
            }

            StepDelayError = string.Empty;

            Steps.TrySetSelectedDelay(MacroInspectorDelay.Custom(milliseconds));

            // Typing a number is itself a choice of `fixed`, so the segment follows it — otherwise a
            // delay could be on the step while the strip above still lit `random`, or nothing at all.
            SetStepDelayChoice(MacroStepDelayMode.Fixed);
        }

    }

    /// <summary>
    /// The three states a step's trailing delay can be in (specs/11-feature-dialogs.md §11.3,
    /// specs/06-macros.md §2.2). <c>Fixed</c> and <c>Random</c> are two different answers rather
    /// than one value with a sentinel — see <see cref="MacroInspectorDelay"/>, which makes the same
    /// distinction for the same reason.
    /// </summary>
    public enum MacroStepDelayMode
    {
        /// <summary>
        /// Nothing follows the step. Still a real state — <c>ReadDelayMode</c> answers it and the
        /// strip draws it by lighting neither segment — but no longer a segment of its own
        /// (issue #148).
        /// </summary>
        None = 0,

        /// <summary>§11.3's custom delay, 1-999 ms, written <c>d001</c>..<c>d999</c>.</summary>
        Fixed = 1,

        /// <summary>§11.3's random delay, written <c>dran</c>.</summary>
        Random = 2
    }

    /// <summary>
    /// One segment of the composer's delay control — <c>fixed</c> or <c>random</c>, and since issue
    /// #148 never <see cref="MacroStepDelayMode.None"/>: "no delay" is a state of the step that the
    /// strip expresses by lighting nothing, and the millisecond field is what writes it.
    ///
    /// <para><b>Immutable and not a view model</b>, exactly as <see cref="MacroChordModifier"/> and
    /// <see cref="MacroStepDirection"/> are, and for the same two reasons.</para>
    /// </summary>
    public sealed class MacroStepDelayOption
    {
        /// <summary>The state this segment writes.</summary>
        public MacroStepDelayMode Mode { get; }

        /// <summary>Its caption — <c>fixed</c> or <c>random</c>.</summary>
        public string Caption { get; }

        /// <summary>Whether the selected step is currently in this state.</summary>
        public bool IsOn { get; }

        /// <summary>Whether the segment may be touched; false while 09 §2's gate refuses.</summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// §11.3's own caption for this state, carrying the range it accepts — the segment's
        /// <c>ToolTip.Tip</c>. The strip reads <c>fixed</c> / <c>random</c> in #137's wording, which
        /// says nothing about bounds, and <b>the random delay's 1-150 ms range appears nowhere else
        /// in the app</b>: the fixed one is stated by its own caption and its validation message, but
        /// a random delay has no field to state it. Dropping these consts with the delay editor they
        /// used to label would have quietly taken that number out of the UI.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Builds one segment in one state. <paramref name="mode"/> is refused for
        /// <see cref="MacroStepDelayMode.None"/> rather than given a caption of its own: since issue
        /// #148 the strip has no such segment, and inventing one here would be a control the view
        /// cannot draw and the user cannot press.
        /// </summary>
        public MacroStepDelayOption(MacroStepDelayMode mode, bool isOn, bool isEnabled)
        {
            Mode = mode;
            Caption = CaptionFor(mode);
            Description = DescriptionFor(mode);
            IsOn = isOn;
            IsEnabled = isEnabled;
        }

        private static string CaptionFor(MacroStepDelayMode mode)
        {
            return mode switch
            {
                MacroStepDelayMode.Fixed => MacroInspectorPanelViewModel.FixedDelayCaption,
                MacroStepDelayMode.Random => MacroInspectorStepViewModel.RandomDelayText,
                _ => throw NotASegment(mode)
            };
        }

        private static string DescriptionFor(MacroStepDelayMode mode)
        {
            return mode switch
            {
                MacroStepDelayMode.Fixed => MacroInspectorStepsViewModel.CustomDelayCaption,
                MacroStepDelayMode.Random => MacroInspectorStepsViewModel.RandomDelayCaption,
                _ => throw NotASegment(mode)
            };
        }

        private static ArgumentOutOfRangeException NotASegment(MacroStepDelayMode mode)
        {
            return new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "The delay strip draws `fixed` and `random` only; `None` is a state of the step, "
                + "cleared by emptying the millisecond field.");
        }
    }
}
