using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// One in-progress edit of one pedal input — the "edit box" of specs/12-savant-elite.md §5 as a
    /// model. It holds its own entry (a single key or a macro buffer) and <b>never touches the
    /// <see cref="PedalInput"/> until <see cref="ApplyTo"/> is called</b>: that is what makes §5's
    /// Cancel a plain discard, with no backup to restore.
    /// <para>
    /// The workflow of §5 steps 2-6 maps one-to-one: <see cref="SetMode"/> is Single Action /
    /// Multiple Actions (and clears the entry on a real change), <see cref="AddKeystroke"/> is a
    /// captured keypress, <see cref="Apply"/> is the Special Actions menu (§6),
    /// <see cref="Backspace"/> and <see cref="Clear"/> are those buttons, and
    /// <see cref="ApplyTo"/> is Done. <see cref="DiffersFrom"/> answers the <c>*Modified</c> label.
    /// </para>
    /// </summary>
    public sealed class PedalEditSession
    {
        /// <summary>The input being edited; <see cref="ApplyTo"/> accepts no other.</summary>
        public PedalInputId Input { get; }

        /// <summary>
        /// Single-action or macro mode (§5 step 2). Never <see cref="PedalInputMode.None"/> — an
        /// unassigned input is edited as an empty entry, not as a third mode.
        /// </summary>
        public PedalInputMode Mode { get; private set; }

        /// <summary>
        /// Whether the §6 "Windows Combination (Win + _)" checkbox is on: the Win/Cmd modifier rides
        /// along with every keystroke the user <b>captures</b> while it is (§6, <see cref="AddKeystroke"/>)
        /// — never with a Special Actions item, which carries the modifiers §6 gives it. Reset by
        /// <see cref="Clear"/> and by a mode change.
        /// </summary>
        public bool HoldWinModifier { get; private set; }

        /// <summary>Whether the entry would program nothing — committing it clears the input.</summary>
        public bool IsEmpty => Mode == PedalInputMode.Macro
            ? _macro is null || _macro.IsEmpty
            : _singleAction is null;

        /// <summary>The single action, non-null only in <see cref="PedalInputMode.Single"/> with a key entered.</summary>
        public KeyDefinition? SingleAction => _singleAction;

        /// <summary>
        /// The macro buffer, non-null only in <see cref="PedalInputMode.Macro"/>. It is the live
        /// buffer — <see cref="ApplyTo"/> clones it, so committing does not hand it to the model.
        /// </summary>
        public Macro? Macro => _macro;

        /// <summary>
        /// The entry as the memo shows it (§5 display conventions): one <c>[token]</c> item in
        /// single mode, one item per macro display item otherwise — with the left-mouse double-click
        /// triple collapsed by <see cref="PedalActionDescriber.DescribeMacroItems"/>. Empty while the
        /// entry is empty.
        /// </summary>
        public IReadOnlyList<string> DisplayItems
        {
            get
            {
                if (Mode == PedalInputMode.Macro)
                {
                    return _macro is null ? [] : PedalActionDescriber.DescribeMacroItems(_macro);
                }

                return _singleAction is null ? [] : ["[" + PedalTokenMap.GetSingleToken(_singleAction) + "]"];
            }
        }

        private KeyDefinition? _singleAction;
        private Macro? _macro;

        private PedalEditSession(PedalInputId input)
        {
            Input = input;
            Mode = PedalInputMode.Single;
        }

        /// <summary>
        /// Opens an edit of <paramref name="input"/> with its current programming loaded — the macro
        /// deep-cloned, so nothing the session does reaches the model before <see cref="ApplyTo"/>.
        /// An unassigned input opens as an empty single-action entry.
        /// <para>
        /// Deliberate deviation from §5 step 1, which always defaulted to Single Action and so threw
        /// an existing macro away the moment a Configure button was pressed. Loading the current
        /// entry makes Configure non-destructive; the mode is still one click away.
        /// </para>
        /// </summary>
        public static PedalEditSession BeginFor(PedalInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var session = new PedalEditSession(input.Id);

            switch (input.Mode)
            {
                case PedalInputMode.Macro:
                    session.Mode = PedalInputMode.Macro;
                    session._macro = input.Macro?.Clone() ?? new Macro();
                    break;

                case PedalInputMode.Single:
                    session._singleAction = input.Action;
                    break;
            }

            return session;
        }

        /// <summary>
        /// §5 step 2: "switching modes clears the current entry". Switching to the mode already in
        /// effect is a no-op — re-clicking the latched button must not destroy the entry.
        /// </summary>
        public void SetMode(PedalInputMode mode)
        {
            if (mode is not (PedalInputMode.Single or PedalInputMode.Macro))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "A pedal edit is either single-action or macro.");
            }

            if (mode == Mode)
            {
                return;
            }

            Mode = mode;
            Clear();
        }

        /// <summary>
        /// §5 step 3: a captured keypress. In single mode it replaces the entry and
        /// <paramref name="modifiers"/> are dropped — a single-action line holds one bare token
        /// (§4.3), with no room for a modifier string. In macro mode it appends the key with those
        /// modifiers plus the sticky <see cref="HoldWinModifier"/>, which is the only place §6's
        /// "Windows Combination" applies.
        /// </summary>
        public void AddKeystroke(KeyDefinition key, MacroModifiers modifiers)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (Mode == PedalInputMode.Single)
            {
                _singleAction = key;

                return;
            }

            var keystroke = new Keystroke(key, modifiers);

            // A key that is itself a modifier carries no modifier string — Keystroke.Modifiers
            // normalizes one away (§4.4: a modifier is written as its own {-mod}/{+mod} group) —
            // so the sticky Win is only overlaid where it can survive.
            if (!keystroke.IsModifierKey)
            {
                keystroke.Modifiers = ApplyHeldWin(modifiers);
            }

            _macro ??= new Macro();
            _macro.AddKeystroke(keystroke);
        }

        /// <summary>
        /// §6: applies a Special Actions item, first switching to the mode the item requires (which
        /// clears the entry, §5 step 2) when the current mode does not support it. An item valid in
        /// both modes stays in the current one. A refusal changes nothing at all — the mode switch
        /// happens only once the item is known to be applicable.
        /// </summary>
        public PedalSpecialActionResult Apply(PedalSpecialAction action)
        {
            ArgumentNullException.ThrowIfNull(action);

            var failure = Validate(action);

            if (failure != PedalSpecialActionFailure.None)
            {
                return PedalSpecialActionResult.Refused(failure);
            }

            SetMode(GetRequiredMode(action));

            switch (action.Kind)
            {
                case PedalSpecialActionKind.Keystrokes:
                    ApplyKeystrokes(action);
                    break;

                case PedalSpecialActionKind.ToggleWinModifier:
                    HoldWinModifier = !HoldWinModifier;
                    break;

                case PedalSpecialActionKind.DifferentPressRelease:
                    ToggleDifferentPressRelease();
                    break;
            }

            return PedalSpecialActionResult.Success();
        }

        /// <summary>Whether <see cref="Apply"/> would succeed right now; the same predicate, without mutating.</summary>
        public bool CanApply(PedalSpecialAction action)
        {
            ArgumentNullException.ThrowIfNull(action);

            return Validate(action) == PedalSpecialActionFailure.None;
        }

        /// <summary>
        /// §5 step 5: removes the last entry. In macro mode that is the last *display item*, so a
        /// trailing left-mouse double click goes in one press (three keystrokes). No-op when empty.
        /// </summary>
        public void Backspace()
        {
            if (Mode == PedalInputMode.Single)
            {
                _singleAction = null;

                return;
            }

            if (_macro is null || _macro.IsEmpty)
            {
                return;
            }

            var start = PedalDisplayItems.GetLastItemStartIndex(_macro);

            for (var i = _macro.Keystrokes.Count - 1; i >= start; i--)
            {
                _macro.RemoveKeystrokeAt(i);
            }
        }

        /// <summary>§5 step 5: empties the entry and drops the held Win modifier, keeping the mode.</summary>
        public void Clear()
        {
            _singleAction = null;
            _macro = Mode == PedalInputMode.Macro ? new Macro() : null;
            HoldWinModifier = false;
        }

        /// <summary>
        /// §5 step 6 (Done): commits the entry to <paramref name="input"/>. An empty entry clears the
        /// input; a macro is cloned in, so later edits in this session cannot leak into the model.
        /// <para>
        /// A commit that would change nothing touches nothing — Done on an untouched entry must not
        /// flip an <c>{input}&gt;</c> line's mode (and therefore its bracket form) on the next save.
        /// </para>
        /// </summary>
        public void ApplyTo(PedalInput input)
        {
            EnsureSameInput(input);

            if (!DiffersFrom(input))
            {
                return;
            }

            if (IsEmpty)
            {
                input.Clear();

                return;
            }

            if (Mode == PedalInputMode.Single)
            {
                input.SetSingleAction(_singleAction);

                return;
            }

            input.SetMacro(_macro!.Clone());
        }

        /// <summary>
        /// Whether committing would change <paramref name="input"/> — the §5 <c>*Modified</c> label.
        /// An empty session over an input that fires nothing is not a change: <c>{jack1}&gt;</c> is a
        /// legitimate file line (an empty macro), and opening it and pressing Done must leave both
        /// the row and the file alone.
        /// </summary>
        public bool DiffersFrom(PedalInput input)
        {
            EnsureSameInput(input);

            if (IsEmpty)
            {
                return input.IsAssigned;
            }

            if (Mode != input.Mode)
            {
                return true;
            }

            return Mode == PedalInputMode.Single
                ? _singleAction != input.Action
                : !_macro!.IsEquivalentTo(input.Macro);
        }

        /// <summary>
        /// The mode the item runs in (§6 "selecting an item automatically switches the editor to the
        /// required mode"): the current one when the item supports it, otherwise the item's own.
        /// </summary>
        private PedalInputMode GetRequiredMode(PedalSpecialAction action)
        {
            if (action.SupportsMode(Mode))
            {
                return Mode;
            }

            return (action.Modes & PedalSpecialActionModes.Macro) != 0
                ? PedalInputMode.Macro
                : PedalInputMode.Single;
        }

        private PedalSpecialActionFailure Validate(PedalSpecialAction action)
        {
            if (action.Kind != PedalSpecialActionKind.DifferentPressRelease)
            {
                return PedalSpecialActionFailure.None;
            }

            // The split flags the last entered key (§6), so a mode switch — which clears the entry —
            // always leaves nothing to flag. That is a different refusal from an empty macro: the
            // fix is to switch to macro mode, not to type something.
            if (GetRequiredMode(action) != Mode)
            {
                return PedalSpecialActionFailure.ModeNotSupported;
            }

            if (_macro is null || _macro.IsEmpty)
            {
                return PedalSpecialActionFailure.EmptyEntry;
            }

            // {-lshift}{ }{+lshift} reads back as a Shift-modified space (§4.4), so the flag would be
            // lost on the next load; §6 appends no bare marker here either.
            return _macro.Keystrokes[^1].IsModifierKey
                ? PedalSpecialActionFailure.ModifierKeystroke
                : PedalSpecialActionFailure.None;
        }

        private void ApplyKeystrokes(PedalSpecialAction action)
        {
            var keystrokes = action.CreateKeystrokes();

            if (Mode == PedalInputMode.Single)
            {
                // Every §6 item offered in single mode emits exactly one key; PedalSpecialActions
                // enforces that at type load.
                _singleAction = keystrokes[0].Key;

                return;
            }

            _macro ??= new Macro();

            // The sticky Win modifier is deliberately not overlaid here: §6 latches it onto
            // *captured* keystrokes only. A catalog item already carries the modifiers §6 gives it,
            // and rewriting them would corrupt the item — {-win}{lmouse}{d125}{lmouse}{+win} is no
            // longer the double-click triple, and wrapping a pause in a Win press/release plays
            // back as a bare Win tap.
            foreach (var keystroke in keystrokes)
            {
                _macro.AddKeystroke(keystroke);
            }
        }

        private void ToggleDifferentPressRelease()
        {
            var last = _macro!.Keystrokes[^1];

            last.DiffPressRelease = !last.DiffPressRelease;
        }

        private MacroModifiers ApplyHeldWin(MacroModifiers modifiers)
        {
            return HoldWinModifier ? modifiers | MacroModifiers.LeftWin : modifiers;
        }

        private void EnsureSameInput(PedalInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (input.Id != Input)
            {
                throw new ArgumentException($"This session edits {Input}, not {input.Id}.", nameof(input));
            }
        }
    }
}
