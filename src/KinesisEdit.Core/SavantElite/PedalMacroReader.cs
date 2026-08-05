using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Reads the value side of a macro line group by group (specs/12-savant-elite.md §4.2): a
    /// leading <c>-</c> is a key-down, a leading <c>+</c> a key-up, a bare token a press-and-release
    /// in one step. One reader parses one line.
    /// <para>
    /// The modifier semantics are the ones of <see cref="Layouts.MacroValueReader"/> (06 §2.2 —
    /// the same firmware lineage): <c>{-mod}</c> joins the active set, <c>{+mod}</c> leaves it, a
    /// non-modifier key acquires the currently held modifiers, and a modifier pressed and released
    /// with no key in between is stored as a plain keystroke of that modifier. What is pedal-only:
    /// a non-modifier <c>{-x}</c>/<c>{+x}</c> pair collapses into a *single* keystroke (§4.3: "the
    /// factory samples instead write every character as a <c>{-x}{+x}</c> pair"), and the
    /// <c>{ }</c> group is disambiguated in context (§4.4, §6).
    /// </para>
    /// </summary>
    internal sealed class PedalMacroReader
    {
        private const char DownPrefix = '-';
        private const char UpPrefix = '+';

        private Keystroke? _pendingPress;

        private readonly List<Keystroke> _keystrokes = [];
        private readonly List<ActiveModifier> _activeModifiers = [];

        /// <summary>
        /// Reads one <c>{...}</c> group. False when the token is unknown — that segment is invalid
        /// and the whole line will not be applied.
        /// </summary>
        public bool TryRead(string groupContent)
        {
            // §4.4: "{ }" is both the space key's file token and the "different press and
            // release" split marker; only the surrounding context tells them apart.
            if (groupContent.Length > 0 && string.IsNullOrWhiteSpace(groupContent))
            {
                ReadDifferentPressReleaseOrSpace();

                return true;
            }

            var direction = KeyDirection.None;
            var token = groupContent;

            // §4.2: the direction prefix is the FIRST character only — "{-.}" is a press of the
            // "." key, "{-vol-}" a press of "vol-", "{+vol+}" a release of "vol+".
            if (token.Length > 0 && token[0] == DownPrefix)
            {
                direction = KeyDirection.Down;
                token = token[1..];
            }
            else if (token.Length > 0 && token[0] == UpPrefix)
            {
                direction = KeyDirection.Up;
                token = token[1..];
            }

            var key = PedalTokenMap.Resolve(token);

            if (key is null)
            {
                return false;
            }

            if (MacroModifierCodes.TryFromKeyCode(key.Code, out var modifier))
            {
                ReadModifier(key, modifier, direction);

                return true;
            }

            ReadKey(key, direction);

            return true;
        }

        /// <summary>
        /// Finishes the line: a modifier still held that was never attached to a keystroke would be
        /// invisible to the serializer, so it is kept as a single tap instead — nothing a field file
        /// carried is dropped. Modifiers that did shield keystrokes need nothing; the serializer
        /// closes them after the last keystroke.
        /// </summary>
        public IReadOnlyList<Keystroke> Complete()
        {
            foreach (var active in _activeModifiers)
            {
                if (!active.Used)
                {
                    _keystrokes.Add(new Keystroke(active.Key));
                }
            }

            _activeModifiers.Clear();
            _pendingPress = null;

            return _keystrokes;
        }

        /// <summary>
        /// §4.3: a key-up that closes the immediately preceding key-down of the same key is the
        /// factory <c>{-x}{+x}</c> pair spelling of one keystroke, not a second event. Anything
        /// else — a bare <c>{x}</c>, a dangling <c>{+x}</c>, a <c>{-x}</c> never closed — is one
        /// keystroke as well, so nothing the file carried is lost.
        /// </summary>
        private void ReadKey(KeyDefinition key, KeyDirection direction)
        {
            if (direction == KeyDirection.Up && _pendingPress is not null && _pendingPress.Key.Code == key.Code)
            {
                _pendingPress = null;

                return;
            }

            var keystroke = new Keystroke(key, CurrentModifiers());

            _keystrokes.Add(keystroke);
            MarkActiveModifiersUsed();

            _pendingPress = direction == KeyDirection.Down ? keystroke : null;
        }

        /// <summary>
        /// The <c>{ }</c> disambiguation of §4.4/§6. Exactly one shape is a split marker: a
        /// <c>{ }</c> arriving inside an open, not-yet-split key-down is the "different press and
        /// release" split of that key (<c>{-k}{ }{+k}</c>) — the only unambiguous spelling, and the
        /// only one <see cref="PedalMacroWriter"/> ever produces. Everything else is the space bar,
        /// whose file token is a single space (§4.4: "on load, the legacy app resolves <c>{ }</c>
        /// to the space key"), held modifiers included.
        /// <para>
        /// §6 writes a standalone marker only when the preceding key has *no* modifiers
        /// ("otherwise a standalone <c>{ }</c> marker is appended"), so a bare <c>{ }</c> after a
        /// modified key is a shape the legacy app never writes — reading it as a split would
        /// destroy modifier-shielded spaces such as <c>{-ctrl}{a}{ }{+ctrl}</c>. The accepted cost
        /// is that a legacy standalone marker after an *unmodified* key loads as a space.
        /// </para>
        /// </summary>
        private void ReadDifferentPressReleaseOrSpace()
        {
            // A second {  } on the same key-down cannot split it twice, so it falls through to the
            // space branch: "{-a}{ }{ }" is "a" (split) followed by a space.
            if (_pendingPress is not null && !_pendingPress.DiffPressRelease)
            {
                _pendingPress.DiffPressRelease = true;

                return;
            }

            ReadKey(PedalTokenMap.SpaceKey, KeyDirection.None);
        }

        /// <summary>
        /// 06 §2.2 modifier semantics, unchanged for the pedal (12 §4.2): down joins the active set
        /// (deduplicated by key code), up leaves it — but an up with no matching down, or one
        /// arriving before the modifier shielded anything, is kept as a single tap; no prefix is a
        /// single tap.
        /// </summary>
        private void ReadModifier(KeyDefinition key, MacroModifiers modifier, KeyDirection direction)
        {
            switch (direction)
            {
                case KeyDirection.Down:
                    if (FindActiveModifier(key.Code) < 0)
                    {
                        _activeModifiers.Add(new ActiveModifier(key, modifier));
                    }

                    break;

                case KeyDirection.Up:
                    var index = FindActiveModifier(key.Code);

                    if (index < 0)
                    {
                        _keystrokes.Add(new Keystroke(key));
                        break;
                    }

                    var wasUsed = _activeModifiers[index].Used;

                    _activeModifiers.RemoveAt(index);

                    if (!wasUsed)
                    {
                        _keystrokes.Add(new Keystroke(key));
                    }

                    break;

                default:
                    _keystrokes.Add(new Keystroke(key));
                    break;
            }

            // A modifier group ends any open key-down: "{-k}{+shift}" leaves nothing to close.
            _pendingPress = null;
        }

        private int FindActiveModifier(int keyCode)
        {
            for (var i = 0; i < _activeModifiers.Count; i++)
            {
                if (_activeModifiers[i].Key.Code == keyCode)
                {
                    return i;
                }
            }

            return -1;
        }

        private MacroModifiers CurrentModifiers()
        {
            var modifiers = MacroModifiers.None;

            foreach (var active in _activeModifiers)
            {
                modifiers |= active.Modifier;
            }

            return modifiers;
        }

        private void MarkActiveModifiersUsed()
        {
            foreach (var active in _activeModifiers)
            {
                active.Used = true;
            }
        }

        private sealed class ActiveModifier
        {
            public KeyDefinition Key { get; }

            public MacroModifiers Modifier { get; }

            public bool Used { get; set; }

            public ActiveModifier(KeyDefinition key, MacroModifiers modifier)
            {
                Key = key;
                Modifier = modifier;
            }
        }
    }
}
