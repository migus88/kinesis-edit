using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// One keystroke inside a macro (specs/05-key-model.md §1.1 <c>TKey</c> in its macro role,
    /// §5.1, §5.2, §5.8; specs/06-macros.md §1): the key that fires plus the modifiers held while
    /// it is struck, its explicit up/down direction, and the two macro-mode flags. Mutable POCO —
    /// the editable runtime model; the immutable key-table entry it points at stays shared.
    /// </summary>
    public sealed class Keystroke
    {
        private const MacroModifiers ControlCodes =
            MacroModifiers.Control | MacroModifiers.LeftControl | MacroModifiers.RightControl;

        private const MacroModifiers AltCodes =
            MacroModifiers.Alt | MacroModifiers.LeftAlt | MacroModifiers.RightAlt;

        /// <summary>
        /// The key-table entry this keystroke fires (§1.1 <c>Key</c>/<c>SaveValue</c>). Assigning a
        /// modifier key clears <see cref="Modifiers"/>: a modifier string is never attached to a key
        /// that is itself a modifier (§5.1).
        /// </summary>
        public KeyDefinition Key
        {
            get => _key;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                _key = value;

                if (MacroModifierCodes.IsModifierKeyCode(_key.Code))
                {
                    _modifiers = MacroModifiers.None;
                }
            }
        }

        /// <summary>
        /// Modifiers held while this keystroke is struck (§1.1 <c>Modifiers</c>, §5.1). Always
        /// <see cref="MacroModifiers.None"/> on a key that is itself a modifier — assignments to
        /// such a key are dropped, per §5.1. Bits outside the §5.1 table name no modifier code and
        /// are dropped on assignment, so two keystrokes that render alike also compare alike
        /// (<see cref="IsEquivalentTo"/>).
        /// </summary>
        public MacroModifiers Modifiers
        {
            get => _modifiers;
            set => _modifiers = Normalize(_key, value);
        }

        /// <summary>Requested key direction (§1.1 <c>UpDown</c>); see <see cref="EffectiveDirection"/> for when it applies.</summary>
        public KeyDirection UpDown { get; set; }

        /// <summary>
        /// Whether the key writes separate press/release events (§1.1 <c>WriteDownUp</c>). True by
        /// default; the constructor sets it false for the speed/delay pseudo-keys, which the key
        /// table flags <see cref="KeyDefinitionFlags.SingleEvent"/> (§3.12).
        /// </summary>
        public bool WriteDownUp { get; set; }

        /// <summary>
        /// Pedal "different press and release" marker (§1.1 <c>DiffPressRel</c>); serialized as
        /// <c>{-token}{ }{+token}</c> (specs/06-macros.md §2.2, §3, §7.6). False by default.
        /// </summary>
        public bool DiffPressRelease { get; set; }

        /// <summary>Whether <see cref="Key"/> is itself one of the modifier keys of §5.1.</summary>
        public bool IsModifierKey => MacroModifierCodes.IsModifierKeyCode(_key.Code);

        /// <summary>Whether the modifier set is exactly generic, left, or right Shift (§5.2).</summary>
        public bool IsShifted =>
            _modifiers is MacroModifiers.Shift or MacroModifiers.LeftShift or MacroModifiers.RightShift;

        /// <summary>
        /// Whether the modifier set is exactly one Ctrl code plus one Alt code and nothing else
        /// — the AltGr condition of §5.2.
        /// </summary>
        public bool IsAltGr => MacroModifierCodes.Count(_modifiers) == 2 && HasSingleControl && HasSingleAlt;

        /// <summary>
        /// The direction actually written (§5.8): a requested key-up/key-down applies only when the
        /// key has no modifiers or is itself a modifier, and never when
        /// <see cref="WriteDownUp"/> is false (§1.1).
        /// </summary>
        public KeyDirection EffectiveDirection
        {
            get
            {
                if (!WriteDownUp)
                {
                    return KeyDirection.None;
                }

                return _modifiers == MacroModifiers.None || IsModifierKey ? UpDown : KeyDirection.None;
            }
        }

        /// <summary>
        /// Number of distinct modifier codes attached to this keystroke (§5.1). Codes that resolve
        /// to the same physical modifier key count once — <c>W ,LW</c> is one held key.
        /// </summary>
        public int ModifierCount => MacroModifierCodes.Count(_modifiers);

        /// <summary>
        /// Cost of this keystroke against the layout keystroke budget: 1 for the keystroke plus 2
        /// per attached modifier (specs/04-layout-file-format.md §5.3, specs/06-macros.md §6).
        /// </summary>
        public int WeightedKeystrokeCount => 1 + (2 * ModifierCount);

        private bool HasSingleControl =>
            (_modifiers & ControlCodes) is MacroModifiers.Control
                or MacroModifiers.LeftControl
                or MacroModifiers.RightControl;

        private bool HasSingleAlt =>
            (_modifiers & AltCodes) is MacroModifiers.Alt
                or MacroModifiers.LeftAlt
                or MacroModifiers.RightAlt;

        private KeyDefinition _key;
        private MacroModifiers _modifiers;

        /// <summary>Creates a keystroke firing <paramref name="key"/> with no modifiers and no direction.</summary>
        public Keystroke(KeyDefinition key)
            : this(key, MacroModifiers.None)
        {
        }

        /// <summary>
        /// Creates a keystroke firing <paramref name="key"/> with <paramref name="modifiers"/> held
        /// (dropped when the key is itself a modifier, §5.1).
        /// </summary>
        public Keystroke(KeyDefinition key, MacroModifiers modifiers)
        {
            ArgumentNullException.ThrowIfNull(key);

            _key = key;
            _modifiers = Normalize(key, modifiers);
            WriteDownUp = (key.Flags & KeyDefinitionFlags.SingleEvent) == 0;
        }

        /// <summary>Renders the modifier set as the comma-separated two-character codes of §5.1.</summary>
        public string FormatModifiers()
        {
            return MacroModifierCodes.Format(_modifiers);
        }

        /// <summary>
        /// Whether this keystroke matches <paramref name="other"/> field by field — the per-item
        /// key equality the macro comparison of §1.2 relies on. Kept as an explicit method:
        /// keystrokes are mutable, so reference identity stays their list identity.
        /// </summary>
        public bool IsEquivalentTo(Keystroke? other)
        {
            return other is not null
                   && _key == other._key
                   && _modifiers == other._modifiers
                   && UpDown == other.UpDown
                   && WriteDownUp == other.WriteDownUp
                   && DiffPressRelease == other.DiffPressRelease;
        }

        /// <summary>Deep copy of this keystroke (§1.1: keys support deep copy).</summary>
        public Keystroke Clone()
        {
            return new Keystroke(_key, _modifiers)
            {
                UpDown = UpDown,
                WriteDownUp = WriteDownUp,
                DiffPressRelease = DiffPressRelease
            };
        }

        private static MacroModifiers Normalize(KeyDefinition key, MacroModifiers modifiers)
        {
            return MacroModifierCodes.IsModifierKeyCode(key.Code)
                ? MacroModifiers.None
                : modifiers & MacroModifierCodes.Known;
        }
    }
}
