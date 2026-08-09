using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The composer's <b>delay</b> section (issue #139) — <c>THEN WAIT</c>, a
    /// <c>none</c> / <c>fixed</c> / <c>random</c> segment and a millisecond field — and the place
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
        /// <summary>The label over the delay segment, in the step row's own voice.</summary>
        public const string StepDelayLabel = "THEN WAIT";

        /// <summary>The segment for "no delay behind this step". This app's wording.</summary>
        public const string NoDelayCaption = "none";

        /// <summary>The segment for §11.3's custom 1-999 ms delay. This app's wording.</summary>
        public const string FixedDelayCaption = "fixed";

        /// <summary>What <c>none</c> means, for the segment's tooltip: the step runs straight on.</summary>
        public const string NoDelayDescription = "No delay after this step";

        /// <summary>The three delay states, in the order the segment draws them.</summary>
        public static IReadOnlyList<MacroStepDelayMode> StepDelayModes { get; } =
        [
            MacroStepDelayMode.None,
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

        /// <summary>The three segments as they currently stand; replaced whole on every change.</summary>
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

        /// <summary>The millisecond field's Up arrow, clamped to 1-999 (§11.3).</summary>
        public IRelayCommand IncreaseStepDelayCommand { get; private set; } = null!;

        /// <summary>The millisecond field's Down arrow, clamped to 1-999 (§11.3).</summary>
        public IRelayCommand DecreaseStepDelayCommand { get; private set; } = null!;

        private IReadOnlyList<MacroStepDelayOption> _stepDelayOptions = [];
        private string _stepDelayError = string.Empty;
        private int _stepDelayMilliseconds;
        private bool _isStepDelayEnabled;
        private bool _isCustomStepDelay;

        /// <summary>Builds the delay section. Called from <c>CreateComposer</c>.</summary>
        private void CreateComposerDelay()
        {
            SetStepDelayModeCommand = new RelayCommand<MacroStepDelayOption>(SetStepDelayMode, CanSetStepDelayMode);
            IncreaseStepDelayCommand = new RelayCommand(() => StepDelayBy(1), () => IsStepDelayEnabled);
            DecreaseStepDelayCommand = new RelayCommand(() => StepDelayBy(-1), () => IsStepDelayEnabled);
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
            IncreaseStepDelayCommand.NotifyCanExecuteChanged();
            DecreaseStepDelayCommand.NotifyCanExecuteChanged();
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

            switch (option.Mode)
            {
                case MacroStepDelayMode.None:
                    StepDelayError = string.Empty;

                    Steps.TrySetSelectedDelay(MacroInspectorDelay.None);

                    break;

                case MacroStepDelayMode.Random:
                    StepDelayError = string.Empty;

                    Steps.TrySetSelectedDelay(MacroInspectorDelay.Random);

                    break;

                case MacroStepDelayMode.Fixed:
                    // Nothing to write yet is not a refusal — it is an empty field with §11.3's own
                    // message beside it, which the first usable number clears by writing.
                    if (MacroDelayTokens.IsValidDelay(_stepDelayMilliseconds))
                    {
                        WriteCustomStepDelay(_stepDelayMilliseconds);
                    }
                    else
                    {
                        StepDelayError = MacroInspectorStepsViewModel.InvalidDelayMessage;
                    }

                    break;
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
        /// Takes a typed millisecond count. It <b>validates whether or not the value moved</b> — an
        /// empty field is 0, and 0 is §11.3's "nothing chosen", which has to report itself even when
        /// the field was already empty — but it only <b>writes</b> when the value really changed, so
        /// re-typing the number that is already on the step cannot dirty the profile.
        /// </summary>
        /// <summary>
        /// Takes what was typed. An empty box is 0 — §11.3's "nothing chosen" — and so is anything
        /// that is not a number at all, which then reports itself through the same validation rather
        /// than being silently ignored.
        /// </summary>
        private void ApplyStepDelayText(string? text)
        {
            var parsed = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;

            ApplyStepDelayMilliseconds(parsed);
        }

        private void ApplyStepDelayMilliseconds(int value)
        {
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
            // delay could be on the step while the strip above still read `none`.
            SetStepDelayChoice(MacroStepDelayMode.Fixed);
        }

        private void StepDelayBy(int offset)
        {
            StepDelayMilliseconds = Math.Clamp(
                _stepDelayMilliseconds + offset,
                Steps.MinimumDelayMilliseconds,
                Steps.MaximumDelayMilliseconds);
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
        /// <summary>Nothing follows the step.</summary>
        None = 0,

        /// <summary>§11.3's custom delay, 1-999 ms, written <c>d001</c>..<c>d999</c>.</summary>
        Fixed = 1,

        /// <summary>§11.3's random delay, written <c>dran</c>.</summary>
        Random = 2
    }

    /// <summary>
    /// One segment of the composer's delay control.
    ///
    /// <para><b>Immutable and not a view model</b>, exactly as <see cref="MacroChordModifier"/> and
    /// <see cref="MacroStepDirection"/> are, and for the same two reasons.</para>
    /// </summary>
    public sealed class MacroStepDelayOption
    {
        /// <summary>The state this segment writes.</summary>
        public MacroStepDelayMode Mode { get; }

        /// <summary>Its caption — <c>none</c>, <c>fixed</c>, <c>random</c>.</summary>
        public string Caption { get; }

        /// <summary>Whether the selected step is currently in this state.</summary>
        public bool IsOn { get; }

        /// <summary>Whether the segment may be touched; false while 09 §2's gate refuses.</summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// §11.3's own caption for this state, carrying the range it accepts — the segment's
        /// <c>ToolTip.Tip</c>. The strip reads <c>none</c> / <c>fixed</c> / <c>random</c> in #137's
        /// wording, which says nothing about bounds, and <b>the random delay's 1-150 ms range appears
        /// nowhere else in the app</b>: the fixed one is stated by the field's own arrows and its
        /// validation message, but a random delay has no field to state it. Dropping these consts with
        /// the delay editor they used to label would have quietly taken that number out of the UI.
        /// </summary>
        public string Description { get; }

        /// <summary>Builds one segment in one state.</summary>
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
                _ => MacroInspectorPanelViewModel.NoDelayCaption
            };
        }

        private static string DescriptionFor(MacroStepDelayMode mode)
        {
            return mode switch
            {
                MacroStepDelayMode.Fixed => MacroInspectorStepsViewModel.CustomDelayCaption,
                MacroStepDelayMode.Random => MacroInspectorStepsViewModel.RandomDelayCaption,
                _ => MacroInspectorPanelViewModel.NoDelayDescription
            };
        }
    }
}
