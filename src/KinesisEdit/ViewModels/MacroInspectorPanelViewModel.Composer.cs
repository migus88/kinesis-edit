using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macro panel's <b>chord composer</b> (issue #128): tick the modifiers, pick the key, insert
    /// the step — <em>without pressing the chord</em>.
    ///
    /// <para><b>Why it exists, precisely.</b> A user cannot record <c>Ctrl+1</c>: macOS switches
    /// virtual desktop on it, and the same is true of every chord a hotkey utility has registered.
    /// This is <b>not</b> a capture defect and no amount of capture work fixes it — such a chord is
    /// consumed by the window server <em>above</em> the application, so the event is never delivered
    /// to the window and no Avalonia handler and no local event monitor can observe it, let alone
    /// swallow it. Reaching it would need a system-wide event tap, the Accessibility permission and
    /// a signed bundle this repo does not produce (docs/app/keystroke-capture.md, "Permissions and
    /// platform reach"; deferred to issue #129). So the fix is not to hear the chord — it is to make
    /// pressing it unnecessary. Every such chord is authorable here, on every platform, with no
    /// permission.</para>
    ///
    /// <para><b>It reuses the insertion path rather than adding one.</b> The key comes from the same
    /// <see cref="TokenPickerViewModel"/> over the same <see cref="KeySearchCatalog"/> that backs the
    /// rail's own key search and §11.6's <c>Search Keys (Macro)</c> modal; the write is
    /// <c>TryAppendKeystroke</c>, which is the one path <see cref="ReceiveKeystroke"/> uses too — so
    /// a composed step is indistinguishable from a recorded one, creates the macro on the first
    /// insert exactly as recording does, and goes out through the same <c>Assigned</c> hop to the
    /// editor's refresh funnel. What the composer adds is the <b>modifier set</b>: the picker alone
    /// inserts a bare key.</para>
    ///
    /// <para><b>The modifier toggles are rebuilt, not mutated.</b> <see cref="MacroChordModifier"/>
    /// is immutable and carries no change notification, so ticking one replaces the whole strip of
    /// eight — which the picker beside it already does per keystroke with hundreds of rows. It is
    /// also deliberately <b>not</b> a <c>ViewModelBase</c>, for the reason
    /// <see cref="RecentTokenStore"/> is not: a type rendered only through a <c>DataTemplate</c>
    /// should not have to be named in <c>ViewResolutionTests</c>.</para>
    ///
    /// <para><b>Left is unmarked and only the right side is spelled</b>, exactly as the step rows and
    /// the co-trigger strip spell it (<see cref="MacroModifierMarks"/>): a bare <c>⌃</c> is Left
    /// Ctrl, <c>R⌃</c> is Right Ctrl, and the words that say which are the toggle's tooltip.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel
    {
        /// <summary>The composer's section label, in the rail's own voice. This app's wording.</summary>
        public const string ComposerLabel = "COMPOSE A CHORD";

        /// <summary>The disclosure caption at rest — and the phrase <see cref="OsReservedNote"/> points at.</summary>
        public const string ComposeChordCaption = "Compose chord";

        /// <summary>The same control once the composer is showing.</summary>
        public const string HideComposerCaption = "Hide composer";

        /// <summary>What the composer is for, said once, above the toggles. This app's wording.</summary>
        public const string ComposerHint =
            "Tick the modifiers, pick the key, and insert it as a step. Nothing is pressed, so a chord the system reserves is authorable here.";

        /// <summary>The composer's write action. Deliberately not <c>insert step</c>, which is the recording row.</summary>
        public const string InsertChordCaption = "Insert chord";

        /// <summary>
        /// The eight modifier toggles, in specs/05-key-model.md §5.1 table order: Shift, Ctrl, Alt
        /// and the Win/Command key, each in its left and right spelling. <b>Only the sided flags are
        /// offered</b> — the generic codes (<c>S </c>, <c>C </c>) exist so a file an older app wrote
        /// still round-trips, and authoring a new one that does not say which side it means would be
        /// writing a worse macro than capture produces (capture always reports a side).
        /// </summary>
        public static IReadOnlyList<MacroModifiers> ChordModifierFlags { get; } =
        [
            MacroModifiers.LeftShift,
            MacroModifiers.RightShift,
            MacroModifiers.LeftControl,
            MacroModifiers.RightControl,
            MacroModifiers.LeftAlt,
            MacroModifiers.RightAlt,
            MacroModifiers.LeftWin,
            MacroModifiers.RightWin
        ];

        /// <summary>
        /// The key search behind the composer — the same picker, over the same catalog, that the
        /// rail's Remap panel and §11.6's modal host. Assigned once, at construction.
        /// </summary>
        public TokenPickerViewModel ChordPicker { get; private set; } = null!;

        /// <summary>The eight toggles as they currently stand; replaced whole on every tick.</summary>
        public IReadOnlyList<MacroChordModifier> ChordModifiers { get; private set; } = [];

        /// <summary>The modifiers ticked right now (05 §5.1), or <see cref="MacroModifiers.None"/>.</summary>
        public MacroModifiers ComposedModifiers
        {
            get => _composedModifiers;
            private set
            {
                if (SetProperty(ref _composedModifiers, value))
                {
                    ChordModifiers = BuildChordModifiers(value);

                    OnPropertyChanged(nameof(ChordModifiers));

                    RefreshComposedPreview();
                }
            }
        }

        /// <summary>Whether the composer is disclosed. Closed is its resting state.</summary>
        public bool IsComposerOpen
        {
            get => _isComposerOpen;
            private set
            {
                if (SetProperty(ref _isComposerOpen, value))
                {
                    OnPropertyChanged(nameof(ComposerToggleCaption));

                    InsertChordCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>The disclosure control's caption, which moves with <see cref="IsComposerOpen"/>.</summary>
        public string ComposerToggleCaption => _isComposerOpen ? HideComposerCaption : ComposeChordCaption;

        /// <summary>
        /// The chord as the step row would draw it — the marks of the modifiers that will actually
        /// be written. Read back off a real <see cref="Keystroke"/> rather than off the ticked set,
        /// so it cannot promise a modifier the model drops: §5.1 attaches none to a key that is
        /// itself a modifier, and <see cref="Keystroke"/> enforces that on assignment.
        /// </summary>
        public IReadOnlyList<MacroModifierMarks.Mark> ComposedMarks
        {
            get => _composedMarks;
            private set => SetProperty(ref _composedMarks, value);
        }

        /// <summary>The picked key as the file spells it — <c>[1]</c> — or an empty string.</summary>
        public string ComposedTokenText
        {
            get => _composedTokenText;
            private set
            {
                if (SetProperty(ref _composedTokenText, value))
                {
                    OnPropertyChanged(nameof(HasComposedKey));
                }
            }
        }

        /// <summary>Whether a key has been picked, and so whether there is a chord to insert.</summary>
        public bool HasComposedKey => _composedTokenText.Length > 0;

        /// <summary>Discloses the composer, or folds it away again and forgets what was ticked.</summary>
        public IRelayCommand ToggleComposerCommand { get; private set; } = null!;

        /// <summary>Ticks one modifier on or off. No cap: a chord may hold every one of them.</summary>
        public IRelayCommand<MacroChordModifier> ToggleChordModifierCommand { get; private set; } = null!;

        /// <summary>Appends the composed chord to the macro as one step.</summary>
        public IRelayCommand InsertChordCommand { get; private set; } = null!;

        private IReadOnlyList<MacroModifierMarks.Mark> _composedMarks = [];
        private string _composedTokenText = string.Empty;
        private MacroModifiers _composedModifiers;
        private bool _isComposerOpen;

        /// <summary>
        /// The eight toggles for <paramref name="ticked"/>. Static and pure: the strip is a
        /// projection of the flag set and of nothing else, which is what makes replacing it whole
        /// cheaper to reason about than mutating eight objects.
        /// </summary>
        public static IReadOnlyList<MacroChordModifier> BuildChordModifiers(MacroModifiers ticked)
        {
            var toggles = new List<MacroChordModifier>(ChordModifierFlags.Count);

            foreach (var flag in ChordModifierFlags)
            {
                toggles.Add(new MacroChordModifier(flag, ticked.HasFlag(flag)));
            }

            return toggles;
        }

        /// <summary>
        /// Builds the composer. Called from the constructor, which is why the properties above carry
        /// a private setter rather than being get-only: a get-only auto-property can be assigned in
        /// the declaring constructor alone, and putting five assignments and two subscriptions there
        /// would be exactly the spill into the main file this partial exists to prevent.
        /// </summary>
        private void CreateComposer(TokenDialect dialect, RecentTokenStore? recentTokens)
        {
            ChordPicker = new TokenPickerViewModel(dialect, recentTokens);
            ChordModifiers = BuildChordModifiers(MacroModifiers.None);

            ToggleComposerCommand = new RelayCommand(ToggleComposer, CanCompose);
            ToggleChordModifierCommand = new RelayCommand<MacroChordModifier>(ToggleChordModifier, _ => CanCompose());
            InsertChordCommand = new RelayCommand(InsertChord, CanInsertChord);

            // ↵ on a highlighted row, and the double click §11.6 accepts on, mean "insert this
            // chord" — the picker itself knows nothing about what taking a row means.
            ChordPicker.Chosen += _ => InsertChord();

            // The picker announces its own highlight; the preview and the write action both follow
            // it. Nothing is unsubscribed: the picker is this panel's and dies with it.
            ChordPicker.PropertyChanged += OnChordPickerPropertyChanged;
        }

        /// <summary>
        /// Re-asks whether the composer may be open at all, and folds it away when the rail moves to
        /// another position — a half-composed chord belongs to the key it was started on, exactly as
        /// a half-recorded step does.
        /// </summary>
        private void RefreshComposer(bool isNewKey)
        {
            if (isNewKey || !CanCompose())
            {
                CloseComposer();
            }

            ToggleComposerCommand.NotifyCanExecuteChanged();
            ToggleChordModifierCommand.NotifyCanExecuteChanged();
            InsertChordCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Whether this position can carry a composed step. It is <see cref="CanRecord"/> exactly:
        /// the composer is a second way to make a step, so it is available wherever making one is —
        /// and nowhere else. With no selection, on a modifier position (05 §5.3) or on a device
        /// without macros the panel draws its refusal instead, and the composer is not reachable.
        /// </summary>
        private bool CanCompose()
        {
            return CanRecord();
        }

        private void ToggleComposer()
        {
            if (_isComposerOpen)
            {
                CloseComposer();

                return;
            }

            if (!CanCompose())
            {
                return;
            }

            Message = string.Empty;

            IsComposerOpen = true;

            // The caret goes to the query box, which is also what stands any recording down: focus
            // in a text input is one of the stand-down seam's three legs, so the panel can never be
            // left claiming to record while the user types a search.
            ChordPicker.RequestFocus();
        }

        /// <summary>
        /// Folds the composer away and forgets the chord. Clearing is not housekeeping: a modifier
        /// left ticked from a previous chord would silently attach itself to the next one.
        /// </summary>
        private void CloseComposer()
        {
            IsComposerOpen = false;
            ComposedModifiers = MacroModifiers.None;

            ChordPicker.Clear();
        }

        private void ToggleChordModifier(MacroChordModifier? modifier)
        {
            if (modifier is null || !CanCompose())
            {
                return;
            }

            ComposedModifiers = modifier.IsOn
                ? ComposedModifiers & ~modifier.Modifier
                : ComposedModifiers | modifier.Modifier;
        }

        private bool CanInsertChord()
        {
            return _isComposerOpen && CanCompose() && ChordPicker.SelectedKey is not null;
        }

        /// <summary>
        /// Writes the composed chord as one step. The composer stays open and stays ticked, because
        /// the chords that need it come in runs — <c>Ctrl+1</c>, <c>Ctrl+2</c>, <c>Ctrl+3</c> is
        /// three picks and three inserts, not three re-ticks.
        /// </summary>
        private void InsertChord()
        {
            if (!CanInsertChord() || BuildComposedKeystroke() is not { } keystroke)
            {
                return;
            }

            if (TryAppendKeystroke(keystroke))
            {
                // The session history the rail's own `Recent` chip reads, so a chord built here is
                // one chip away next time.
                ChordPicker.Remember(keystroke.Key);
            }
        }

        /// <summary>
        /// The chord as it would be written, or null while no key is picked. Building a real
        /// <see cref="Keystroke"/> is what makes the preview honest — the model drops a modifier set
        /// attached to a modifier key (05 §5.1), and the preview reads the result rather than the
        /// request.
        /// </summary>
        private Keystroke? BuildComposedKeystroke()
        {
            return ChordPicker.SelectedKey is { } key ? new Keystroke(key, _composedModifiers) : null;
        }

        private void OnChordPickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (nameof(TokenPickerViewModel.SelectedKey) or nameof(TokenPickerViewModel.SelectedRow)))
            {
                return;
            }

            RefreshComposedPreview();

            InsertChordCommand.NotifyCanExecuteChanged();
        }

        private void RefreshComposedPreview()
        {
            if (BuildComposedKeystroke() is not { } keystroke)
            {
                ComposedMarks = [];
                ComposedTokenText = string.Empty;

                return;
            }

            ComposedMarks = MacroModifierMarks.Split(keystroke.Modifiers);
            ComposedTokenText = KeyboardKeyViewModel.FormatToken(keystroke.Key, _dialect);
        }
    }

    /// <summary>
    /// One modifier toggle of the chord composer, drawn as mockup <c>2i</c>'s mark — <c>⇧</c>,
    /// <c>R⌃</c>, <c>⌘</c> — with the same "left unmarked, right spelled <c>R</c>" rule the step rows
    /// and the co-trigger strip follow (<see cref="MacroModifierMarks"/>).
    ///
    /// <para><b>Immutable.</b> Ticking one rebuilds the strip of eight rather than mutating a toggle,
    /// so there is no change notification here and nothing to keep in step — the same choice
    /// <see cref="TokenPickerViewModel"/> makes for its result rows, and for the same reason.</para>
    ///
    /// <para><b>Not a view model</b>, deliberately: it is rendered only through a
    /// <c>DataTemplate</c> in <c>Views/MacroInspectorPanelView.axaml</c>, and a type that never
    /// resolves to a view of its own should not have to be excused in
    /// <c>ViewResolutionTests</c> — the reasoning <see cref="RecentTokenStore"/> already records.</para>
    /// </summary>
    public sealed class MacroChordModifier
    {
        /// <summary>The 05 §5.1 flag this toggle carries — always a sided one.</summary>
        public MacroModifiers Modifier { get; }

        /// <summary>
        /// The <c>R</c> prefix, or empty for the left-hand toggle: left is the unmarked side. A
        /// Latin letter, so it belongs in the ordinary mono face and never in <c>.keySymbol</c>,
        /// whose 19-codepoint subset carries no letters.
        /// </summary>
        public string Side { get; }

        /// <summary>The mark itself — <c>⇧</c>, <c>⌃</c>, <c>⌥</c>, <c>⌘</c> — drawn in <c>.keySymbol</c>.</summary>
        public string Symbol { get; }

        /// <summary>Whether this toggle draws a side letter beside its mark.</summary>
        public bool HasSide => Side.Length > 0;

        /// <summary>
        /// The modifier in words — <c>Left Ctrl</c>, <c>Right Win</c>. The toggle's tooltip, and the
        /// only thing on a left-hand one that says which side it is.
        /// </summary>
        public string Description { get; }

        /// <summary>Whether this modifier is currently ticked into the chord.</summary>
        public bool IsOn { get; }

        /// <summary>Builds the toggle for one flag in one state; the mark comes from the shared table.</summary>
        public MacroChordModifier(MacroModifiers modifier, bool isOn)
        {
            var mark = MacroModifierMarks.For(modifier);

            Modifier = modifier;
            Side = mark.Side;
            Symbol = mark.Symbol;
            Description = mark.Description;
            IsOn = isOn;
        }
    }
}
