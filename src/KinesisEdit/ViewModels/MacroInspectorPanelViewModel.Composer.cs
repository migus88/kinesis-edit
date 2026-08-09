using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macro panel's <b>composer</b> (issue #139): one always-present block that edits
    /// <em>the selected step</em> — its key, the modifiers held with it, its direction, and the
    /// delay behind it.
    ///
    /// <para><b>What it replaced, and why.</b> Issue #128 put a collapsible <em>append-a-chord</em>
    /// strip here: tick eight sided modifiers, search a key in a <see cref="TokenPickerViewModel"/>,
    /// press <c>Insert chord</c>, and a new step appeared at the <b>end</b>. It could not change a
    /// step that already existed — and the per-step delay was edited in a <em>third</em> surface
    /// again, the <c>DELAY AFTER THIS STEP</c> editor on <see cref="MacroInspectorStepsViewModel"/>.
    /// So the panel had three ways to change a macro and no way to change a step. All of it collapses
    /// into this one block.</para>
    ///
    /// <para><b>Nothing here has an <c>Apply</c>.</b> Every control writes the moment it is touched,
    /// through <see cref="MacroInspectorStepsViewModel"/>'s <c>TrySet…</c> methods and so through the
    /// panel's existing <c>OnMacroWritten</c> hop — the session goes dirty exactly as it does for a
    /// recorded step, and there is no half-committed state to abandon.</para>
    ///
    /// <para><b>The key is set by <c>Record</c> and by nothing else.</b> There is no text box and no
    /// picker: the composer arms the panel's <em>own</em> sink for exactly one keystroke
    /// (<see cref="MacroCaptureMode.SingleStep"/>) and writes what it hears onto the selected step.
    /// #128's reason for existing survives that: <c>Ctrl+1</c> is still authorable without pressing
    /// it — record the bare <c>1</c>, which the window server does deliver, then tick <c>⌃</c> here —
    /// see <see cref="OsReservedNote"/>. What was lost is only the search-by-name route to a key,
    /// which was never what #128 was for.</para>
    ///
    /// <para><b>Four latches, not eight.</b> Authoring is left-hand only, exactly as the Trigger
    /// strip's three are (issue #137): the right-hand and generic spellings a <em>file</em> carries
    /// are preserved by Core and rendered by the step row, but nothing new is authored that does not
    /// say which side it means. The composer goes one step further than the strip and
    /// <b>normalizes on selection</b> — see <see cref="NormalizeSelectedStep"/>, which is the one
    /// place a mere click is entitled to write.</para>
    ///
    /// <para><b>The toggles are rebuilt, not mutated.</b> <see cref="MacroChordModifier"/>,
    /// <see cref="MacroStepDirection"/> and <see cref="MacroStepDelayOption"/> are immutable and
    /// carry no change notification, so any change replaces the whole strip. All three are
    /// deliberately <b>not</b> <c>ViewModelBase</c>, for the reason <see cref="RecentTokenStore"/> is
    /// not: a type rendered only through a <c>DataTemplate</c> should not have to be excused in
    /// <c>ViewResolutionTests</c>.</para>
    ///
    /// <para>The delay half lives in <c>MacroInspectorPanelViewModel.ComposerDelay.cs</c> — a further
    /// partial rather than a longer file, per docs/guides/Coding Conventions.md.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel
    {
        /// <summary>
        /// The composer's section label, in the rail's own voice — issue #137's own ASCII, which is the
        /// closest thing to a spec this surface has (no mockup draws a composer at all; #128 invented it).
        /// It replaces <c>COMPOSE A CHORD</c> and stays a verb phrase deliberately: a bare <c>STEP</c>
        /// would read as a second heading for the <c>Sequence</c> list directly above it.
        /// </summary>
        public const string ComposerLabel = "COMPOSE A STEP";

        /// <summary>
        /// What the composer is for, said once, under the label. It is on screen while every control
        /// below it is dead, which is the state it exists to explain.
        /// </summary>
        public const string ComposerHint = "Select a step to edit its key, modifiers, action and delay.";

        /// <summary>The label over the key chip and its record button. This app's wording.</summary>
        public const string StepKeyLabel = "KEY";

        /// <summary>The label over the four modifier latches — 2i's own word for a modified step.</summary>
        public const string StepModifiersLabel = "HELD";

        /// <summary>The label over the tap/press/release segment. This app's wording.</summary>
        public const string StepDirectionLabel = "ACTION";

        /// <summary>What the key chip reads while the selected step has no key of its own.</summary>
        public const string NoStepKeyText = "—";

        /// <summary>
        /// The four left-hand modifier flags the composer offers, in specs/05-key-model.md §5.1
        /// table order: Shift, Ctrl, Alt and the Win/Command key.
        ///
        /// <para><b>Left-hand only, and that is authoring policy rather than a model limit.</b> The
        /// §5.1 table distinguishes a generic, a left and a right spelling of each; capture always
        /// reports a side, a file may carry any of the three, and Core reads and writes all of them
        /// unchanged. What this strip refuses is <em>authoring</em> the other two: a right-hand
        /// modifier is a different physical key that a macro almost never means specifically, and a
        /// generic one says less than capture already knows. Eight latches in a 300 px rail also cost
        /// twice the width to express the same intent — which is why the Trigger strip made the same
        /// cut for its three (issue #137).</para>
        /// </summary>
        public static IReadOnlyList<MacroModifiers> ChordModifierFlags { get; } =
        [
            MacroModifiers.LeftShift,
            MacroModifiers.LeftControl,
            MacroModifiers.LeftAlt,
            MacroModifiers.LeftWin
        ];

        /// <summary>The three directions of 06 §2.2, in the order the segment draws them.</summary>
        public static IReadOnlyList<KeyDirection> StepDirectionValues { get; } =
        [
            KeyDirection.None,
            KeyDirection.Down,
            KeyDirection.Up
        ];

        /// <summary>
        /// Whether anything in the composer may be touched at all: a position that can carry a macro,
        /// a loaded layout, and <b>a selected step</b>. With nothing selected every control below is
        /// dead and only the Sequence header's <c>Record</c> / <c>＋</c> pair is live — which is how a
        /// selection comes to exist in the first place.
        /// <para>
        /// It is a <em>disabled</em> block rather than an absent one, which is a deliberate exception
        /// to docs/design/README.md's "features a device lacks are not rendered at all": nothing is
        /// missing here, the user simply has not pointed at a step yet, and a composer that appeared
        /// and vanished as rows were clicked would move everything below it on every click.
        /// </para>
        /// </summary>
        public bool IsComposerEnabled => CanRecord() && Steps.SelectedStep is not null;

        /// <summary>
        /// Whether the selected step has a key the composer may set — false with nothing selected and
        /// on a delay-only row, which carries a delay and no keystroke of its own (06 §2.2).
        /// </summary>
        public bool IsStepKeyEnabled => IsComposerEnabled && Steps.SelectedStep?.IsDelayOnly != true;

        /// <summary>
        /// Whether the four latches may be touched. They go dead on the <c>＋</c> placeholder (there
        /// is no key for a modifier to qualify yet), on a delay-only row, and — the interesting case
        /// — on a step whose key is <b>itself a modifier</b>: 05 §5.1 attaches no modifier string to
        /// such a key and <see cref="Keystroke"/> drops one that is offered, so a live latch there
        /// would be a control that visibly does nothing.
        /// </summary>
        public bool AreStepModifiersEnabled => _areStepModifiersEnabled;

        /// <summary>The selected step's key as the file spells it — <c>[1]</c> — or a dash.</summary>
        public string StepTokenText
        {
            get => _stepTokenText;
            private set => SetProperty(ref _stepTokenText, value);
        }

        /// <summary>Whether there is a real key token to draw rather than the placeholder dash.</summary>
        public bool HasStepKey => _hasStepKey;

        /// <summary>The four latches as they currently stand; replaced whole on every change.</summary>
        public IReadOnlyList<MacroChordModifier> ChordModifiers
        {
            get => _chordModifiers;
            private set => SetProperty(ref _chordModifiers, value);
        }

        /// <summary>The tap/press/release segment as it currently stands; replaced whole.</summary>
        public IReadOnlyList<MacroStepDirection> StepDirections
        {
            get => _stepDirections;
            private set => SetProperty(ref _stepDirections, value);
        }

        /// <summary>The composer's record button caption, which moves with the single-shot arm.</summary>
        public string RecordStepKeyCaption =>
            _captureMode == MacroCaptureMode.SingleStep ? RecordingCaption : RecordCaption;

        /// <summary>
        /// Arms the panel's own sink for <b>exactly one</b> keystroke and writes it onto the selected
        /// step. Pressing it again stands the arm down without writing.
        /// </summary>
        public IRelayCommand RecordStepKeyCommand { get; private set; } = null!;

        /// <summary>Ticks one modifier on or off. No cap: a step may hold every one of them.</summary>
        public IRelayCommand<MacroChordModifier> ToggleChordModifierCommand { get; private set; } = null!;

        /// <summary>Writes the selected step's direction — <c>tap</c>, <c>press</c> or <c>release</c>.</summary>
        public IRelayCommand<MacroStepDirection> SetStepDirectionCommand { get; private set; } = null!;

        private IReadOnlyList<MacroChordModifier> _chordModifiers = [];
        private IReadOnlyList<MacroStepDirection> _stepDirections = [];
        private string _stepTokenText = NoStepKeyText;
        private bool _hasStepKey;
        private bool _areStepModifiersEnabled;

        /// <summary>
        /// Whether the composer is currently <em>reading</em> the selection rather than reacting to
        /// the user moving it. <see cref="NormalizeSelectedStep"/> writes, and every write rebuilds
        /// the rows and re-establishes the selection — so without this the normalization would
        /// re-enter itself, and a plain <c>Refresh</c> would write.
        /// </summary>
        private bool _isReadingSelection;

        /// <summary>
        /// Builds the composer. Called from the constructor, which is why the commands above carry a
        /// private setter rather than being get-only: a get-only auto-property can be assigned in the
        /// declaring constructor alone, and putting these assignments there would be exactly the
        /// spill into the main file this partial exists to prevent.
        /// </summary>
        private void CreateComposer()
        {
            RecordStepKeyCommand = new RelayCommand(ToggleStepKeyCapture, () => IsStepKeyEnabled);
            ToggleChordModifierCommand = new RelayCommand<MacroChordModifier>(
                ToggleChordModifier,
                _ => AreStepModifiersEnabled);
            SetStepDirectionCommand = new RelayCommand<MacroStepDirection>(SetStepDirection, CanSetStepDirection);

            CreateComposerDelay();

            ReadComposerFromSelection();
        }

        /// <summary>
        /// The one moment a mere <b>click</b> is entitled to write (issue #139, and a deliberate one).
        ///
        /// <para>The composer offers four left-hand latches, so a step that arrived from a file
        /// carrying <c>RS</c> or the generic <c>S </c> cannot be shown honestly by them — lighting
        /// <c>⇧</c> for a right-hand modifier would make the next tick silently rewrite the other
        /// side, and lighting nothing would deny a modifier the row visibly draws. Selecting the step
        /// therefore rewrites its set to the left-hand spellings of the physical modifiers it holds,
        /// once, and everything after that is honest.</para>
        ///
        /// <para><b>This can mark the profile dirty from a click, and that is accepted.</b> The
        /// alternative is a composer that shows one thing and writes another. It is also the
        /// deliberate <em>difference</em> from the Trigger strip beside it, which is preserve-on-load
        /// / normalize-on-<em>edit</em> (issue #137): a co-trigger the user only looked at is never
        /// touched. The strip can afford that because its latches are lit by
        /// <see cref="MacroCoTriggerViewModel.Family"/> and it rewrites the <em>whole</em> set on the
        /// first touch anyway; the composer cannot, because its latches are also the control that
        /// edits, and a half-normalized set would be authored one tick later regardless.</para>
        ///
        /// <para>The families come from <see cref="MacroCoTriggerViewModel.FamilyOf"/> — the same
        /// grouping of §5.1's three spellings the Trigger strip lights from, so the two surfaces can
        /// never disagree about which physical key a code names.</para>
        /// </summary>
        private void NormalizeSelectedStep()
        {
            if (Steps.SelectedStep is not { } step
                || step.IsPlaceholder
                || step.IsDelayOnly
                || step.IsModifierKeyStep)
            {
                return;
            }

            var normalized = NormalizeModifiers(step.ModifierFlags);

            if (normalized == step.ModifierFlags)
            {
                return;
            }

            Steps.TrySetSelectedModifiers(normalized);
        }

        /// <summary>
        /// <paramref name="modifiers"/> rewritten to the left-hand spelling of every physical
        /// modifier it names. Bits that name none are dropped, exactly as
        /// <see cref="Keystroke.Modifiers"/> drops them on assignment.
        /// </summary>
        private static MacroModifiers NormalizeModifiers(MacroModifiers modifiers)
        {
            var normalized = MacroModifiers.None;

            foreach (var flag in ChordModifierFlags)
            {
                if ((MacroCoTriggerViewModel.FamilyOf(flag) & modifiers) != MacroModifiers.None)
                {
                    normalized |= flag;
                }
            }

            return normalized;
        }

        /// <summary>
        /// Rebuilds every control in the composer from the selected step. It is a pure read: the one
        /// write a selection can cause is <see cref="NormalizeSelectedStep"/>, which has already run
        /// by the time this does.
        /// </summary>
        private void ReadComposerFromSelection()
        {
            var step = Steps.SelectedStep;
            var enabled = IsComposerEnabled;
            var isKeyed = enabled && step is { IsDelayOnly: false, IsPlaceholder: false };

            _areStepModifiersEnabled = isKeyed && step?.IsModifierKeyStep != true;
            _hasStepKey = step?.HasToken == true;

            StepTokenText = _hasStepKey ? step!.TokenText : NoStepKeyText;
            ChordModifiers = BuildChordModifiers(step?.ModifierFlags ?? MacroModifiers.None, _areStepModifiersEnabled);
            StepDirections = BuildStepDirections(step, isKeyed);

            ReadComposerDelayFromSelection(step, enabled);

            OnPropertyChanged(nameof(IsComposerEnabled));
            OnPropertyChanged(nameof(IsStepKeyEnabled));
            OnPropertyChanged(nameof(AreStepModifiersEnabled));
            OnPropertyChanged(nameof(HasStepKey));
            OnPropertyChanged(nameof(RecordingBanner));

            RecordStepKeyCommand.NotifyCanExecuteChanged();
            ToggleChordModifierCommand.NotifyCanExecuteChanged();
            SetStepDirectionCommand.NotifyCanExecuteChanged();

            RefreshComposerDelayCommands();
        }

        /// <summary>
        /// Reacts to the step list's selection moving — the pointer, <c>⌥↑↓</c>, a delete, or a
        /// rebuild re-establishing it after a write. Normalization runs first and is suppressed
        /// against itself; the read that follows sees the normalized step.
        /// </summary>
        private void OnSelectedStepChanged()
        {
            if (_isReadingSelection)
            {
                return;
            }

            _isReadingSelection = true;

            try
            {
                NormalizeSelectedStep();
            }
            finally
            {
                _isReadingSelection = false;
            }

            ReadComposerFromSelection();
        }

        /// <summary>The four latches for <paramref name="held"/> in one enabled state; static and pure.</summary>
        private static IReadOnlyList<MacroChordModifier> BuildChordModifiers(MacroModifiers held, bool isEnabled)
        {
            var toggles = new List<MacroChordModifier>(ChordModifierFlags.Count);

            foreach (var flag in ChordModifierFlags)
            {
                toggles.Add(new MacroChordModifier(flag, held.HasFlag(flag), isEnabled));
            }

            return toggles;
        }

        /// <summary>
        /// The three direction segments for <paramref name="step"/>.
        ///
        /// <para><b><c>press</c> and <c>release</c> die on a chord, and that is 05 §5.8 rather than a
        /// preference.</b> <see cref="Keystroke.EffectiveDirection"/> answers <c>None</c> for a
        /// keystroke that carries modifiers, so a direction chosen there would be discarded on the
        /// way to the file — the segment would be a control whose effect the next save deletes. A
        /// step whose key is itself a modifier is the exception, because §5.8 exempts it and because
        /// the modifier set on such a step is always empty anyway.</para>
        /// </summary>
        private static IReadOnlyList<MacroStepDirection> BuildStepDirections(
            MacroInspectorStepViewModel? step,
            bool isEnabled)
        {
            var current = step?.Direction ?? KeyDirection.None;
            var allowsDirection = isEnabled && step?.ModifierFlags == MacroModifiers.None;
            var segments = new List<MacroStepDirection>(StepDirectionValues.Count);

            foreach (var direction in StepDirectionValues)
            {
                segments.Add(new MacroStepDirection(
                    direction,
                    current == direction,
                    direction == KeyDirection.None ? isEnabled : allowsDirection));
            }

            return segments;
        }

        /// <summary>
        /// Arms the single shot, or stands it down again. It runs <see cref="StopRecording"/> first
        /// whichever way it goes, so a run recording started from the Sequence header cannot be left
        /// live beside it — <see cref="MacroCaptureMode"/> holds "one owner at a time" by being one
        /// field, and this is the transition that would otherwise try to hold two.
        /// </summary>
        private void ToggleStepKeyCapture()
        {
            if (_captureMode == MacroCaptureMode.SingleStep)
            {
                StopRecording();

                return;
            }

            if (!IsStepKeyEnabled)
            {
                return;
            }

            Message = string.Empty;

            SetCaptureMode(MacroCaptureMode.SingleStep);
        }

        /// <summary>
        /// Writes the captured key onto the selected step, creating the macro first when the
        /// <c>＋</c> placeholder is what is selected on a position that carries none — "the first
        /// keystroke <em>is</em> the macro" applies to a composed step exactly as it does to a
        /// recorded one, and <see cref="EnsureMacro"/> is the single path that reports 06 §6's count
        /// refusal.
        /// </summary>
        private bool TryWriteKeyToSelectedStep(KeyDefinition key)
        {
            if (!CanRecord())
            {
                return false;
            }

            if (Steps.HasPlaceholder && EnsureMacro() is null)
            {
                return false;
            }

            if (!Steps.TrySetSelectedKey(key))
            {
                return false;
            }

            Message = string.Empty;

            return true;
        }

        private void ToggleChordModifier(MacroChordModifier? modifier)
        {
            if (modifier is null || !AreStepModifiersEnabled || Steps.SelectedStep is not { } step)
            {
                return;
            }

            var held = modifier.IsOn
                ? step.ModifierFlags & ~modifier.Modifier
                : step.ModifierFlags | modifier.Modifier;

            Steps.TrySetSelectedModifiers(NormalizeModifiers(held));
        }

        private bool CanSetStepDirection(MacroStepDirection? direction)
        {
            return direction is not null && direction.IsEnabled && IsComposerEnabled;
        }

        private void SetStepDirection(MacroStepDirection? direction)
        {
            if (!CanSetStepDirection(direction))
            {
                return;
            }

            Steps.TrySetSelectedDirection(direction!.Direction);
        }
    }

    /// <summary>
    /// One modifier latch of the composer, drawn as mockup <c>2i</c>'s mark — <c>⇧</c>, <c>⌃</c>,
    /// <c>⌥</c>, <c>⌘</c> — under the same "left unmarked, right spelled <c>R</c>" rule the step rows
    /// and the co-trigger strip follow (<see cref="MacroModifierMarks"/>). Since issue #139 only the
    /// four left-hand flags are offered, so no latch here ever draws a side letter; the field stays
    /// because the mark table is shared with surfaces that do.
    ///
    /// <para><b>Immutable.</b> Ticking one rebuilds the strip rather than mutating a latch, so there
    /// is no change notification here and nothing to keep in step — the same choice
    /// <see cref="MacroSlotOption"/> makes, and for the same reason.</para>
    ///
    /// <para><b>Not a view model</b>, deliberately: it is rendered only through a
    /// <c>DataTemplate</c> in <c>Views/MacroInspectorPanelView.axaml</c>, and a type that never
    /// resolves to a view of its own should not have to be excused in
    /// <c>ViewResolutionTests</c> — the reasoning <see cref="RecentTokenStore"/> already records.</para>
    /// </summary>
    public sealed class MacroChordModifier
    {
        /// <summary>The 05 §5.1 flag this latch carries — always a left-hand one.</summary>
        public MacroModifiers Modifier { get; }

        /// <summary>
        /// The <c>R</c> prefix, or empty for a left-hand latch: left is the unmarked side. A Latin
        /// letter, so it belongs in the ordinary mono face and never in <c>.keySymbol</c>, whose
        /// 19-codepoint subset carries no letters.
        /// </summary>
        public string Side { get; }

        /// <summary>The mark itself — <c>⇧</c>, <c>⌃</c>, <c>⌥</c>, <c>⌘</c> — drawn in <c>.keySymbol</c>.</summary>
        public string Symbol { get; }

        /// <summary>Whether this latch draws a side letter beside its mark.</summary>
        public bool HasSide => Side.Length > 0;

        /// <summary>
        /// The modifier in words — <c>Left Ctrl</c>, <c>Left Win</c>. The latch's tooltip, and the
        /// only thing on it that says which side it is.
        /// </summary>
        public string Description { get; }

        /// <summary>Whether the selected step is currently struck with this modifier held.</summary>
        public bool IsOn { get; }

        /// <summary>
        /// Whether the latch may be touched. False with nothing selected, on the <c>＋</c>
        /// placeholder, on a delay-only row, and on a step whose key is itself a modifier — the last
        /// because 05 §5.1 refuses a modifier string there and the model would drop the write.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>Builds the latch for one flag in one state; the mark comes from the shared table.</summary>
        public MacroChordModifier(MacroModifiers modifier, bool isOn, bool isEnabled)
        {
            var mark = MacroModifierMarks.For(modifier);

            Modifier = modifier;
            Side = mark.Side;
            Symbol = mark.Symbol;
            Description = mark.Description;
            IsOn = isOn;
            IsEnabled = isEnabled;
        }
    }

    /// <summary>
    /// One segment of the composer's <c>tap</c> / <c>press</c> / <c>release</c> control — the three
    /// directions of 06 §2.2, in the step row's own words so the control and the row it edits cannot
    /// read differently.
    ///
    /// <para><b>Immutable and not a view model</b>, exactly as <see cref="MacroChordModifier"/> is,
    /// and for the same two reasons.</para>
    /// </summary>
    public sealed class MacroStepDirection
    {
        /// <summary>The direction this segment writes (06 §2.2).</summary>
        public KeyDirection Direction { get; }

        /// <summary>
        /// Its caption — <see cref="MacroInspectorStepViewModel.TapAction"/> and its two siblings,
        /// borrowed rather than restated so the segment and the row always spell it the same way.
        /// </summary>
        public string Caption { get; }

        /// <summary>Whether the selected step currently carries this direction.</summary>
        public bool IsOn { get; }

        /// <summary>
        /// Whether the segment may be touched. <c>press</c> and <c>release</c> are dead on a step
        /// that carries modifiers, because 05 §5.8 discards a direction there.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>Builds one segment in one state.</summary>
        public MacroStepDirection(KeyDirection direction, bool isOn, bool isEnabled)
        {
            Direction = direction;
            Caption = CaptionFor(direction);
            IsOn = isOn;
            IsEnabled = isEnabled;
        }

        private static string CaptionFor(KeyDirection direction)
        {
            return direction switch
            {
                KeyDirection.Down => MacroInspectorStepViewModel.PressAction,
                KeyDirection.Up => MacroInspectorStepViewModel.ReleaseAction,
                _ => MacroInspectorStepViewModel.TapAction
            };
        }
    }
}
