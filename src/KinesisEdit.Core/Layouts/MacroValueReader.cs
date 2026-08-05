using System.Globalization;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Reads the value side of a macro line (specs/06-macros.md §2.2) group by group: the
    /// leading <c>{sN}</c>/<c>{xN}</c>/<c>{speedN}</c> markers, then the keystroke stream with
    /// its active-modifier tracking — <c>{-mod}</c> joins the held set, <c>{+mod}</c> leaves
    /// it, plain keys record the currently held modifiers, a bare modifier is a single tap.
    /// One reader parses one line; the caller applies the outcome only when every segment of
    /// the line proved valid.
    /// </summary>
    internal sealed class MacroValueReader
    {
        private const char DownPrefix = '-';
        private const char UpPrefix = '+';
        private const string RepeatTokenPrefix = "x";

        /// <summary>Speed carried by a consumed speed token; null when absent or ignored (06 §2.2, §4).</summary>
        public int? Speed { get; private set; }

        /// <summary>Repeat carried by a consumed repeat token; null when absent or ignored (06 §2.2, §4).</summary>
        public int? Repeat { get; private set; }

        private bool _speedSeen;
        private bool _repeatSeen;

        private readonly LayoutDialectRules _rules;
        private readonly List<Keystroke> _keystrokes = [];
        private readonly List<ActiveModifier> _activeModifiers = [];

        public MacroValueReader(LayoutDialectRules rules)
        {
            _rules = rules;
        }

        /// <summary>
        /// Consumes a leading speed/repeat marker (06 §2.2: "only honored as the first
        /// token(s), before any keystroke"). Returns false when the group is no such marker —
        /// the caller then leaves the header zone for good. Out-of-range values follow the
        /// dialect's policy: ignored (RGB family), replaced by the default (FS, Adv2), or
        /// clamped into range (Adv360).
        /// </summary>
        public bool TryConsumeHeaderToken(string groupContent)
        {
            if (!_speedSeen && TryReadHeaderValue(groupContent, _rules.SpeedTokenPrefix, _rules.Device.Macros.Speed, out var speed))
            {
                _speedSeen = true;
                Speed = speed;

                return true;
            }

            if (_rules.ParsesRepeatToken
                && !_repeatSeen
                && TryReadHeaderValue(groupContent, RepeatTokenPrefix, _rules.Device.Macros.Repeat, out var repeat))
            {
                _repeatSeen = true;
                Repeat = repeat;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads one keystroke group (06 §2.2). False when the token is unknown — that segment
        /// is invalid and the whole line will not be applied.
        /// </summary>
        public bool TryReadKeystroke(string groupContent)
        {
            var direction = KeyDirection.None;
            var token = groupContent;

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

            var key = token.Length == 0 ? null : LayoutTokenResolver.ResolveMacroValueToken(token, _rules);

            if (key is null)
            {
                return false;
            }

            if (MacroModifierCodes.TryFromKeyCode(key.Code, out var modifier))
            {
                ReadModifier(key, modifier, direction);

                return true;
            }

            var keystroke = new Keystroke(key, CurrentModifiers())
            {
                UpDown = direction
            };

            _keystrokes.Add(keystroke);
            MarkActiveModifiersUsed();

            return true;
        }

        /// <summary>
        /// Finishes the line: a modifier still held that was never attached to a keystroke
        /// would be invisible to the serializer, so it is kept as a single tap instead —
        /// nothing a field file carried is dropped. Modifiers that did shield keystrokes need
        /// nothing; the serializer closes them after the last keystroke (06 §3).
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

            return _keystrokes;
        }

        /// <summary>
        /// 06 §2.2 modifier semantics: down joins the active set (deduplicated by key code,
        /// 05 §5.1); up leaves it — but an up with no matching down, or one arriving before the
        /// modifier shielded anything, is kept as a single tap; no prefix is a single tap.
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

        /// <summary>
        /// Reads <c>prefix + digits</c> (06 §2.2: "Speed/repeat digits must be numeric"); false
        /// when the group is not that shape — it then falls through to token resolution, so
        /// <c>{s}</c> stays the letter <c>s</c> and <c>{spc}</c> stays the space bar.
        /// </summary>
        private bool TryReadHeaderValue(string groupContent, string prefix, Devices.ValueRange? range, out int? result)
        {
            result = null;

            if (!groupContent.StartsWith(prefix, StringComparison.Ordinal) || groupContent.Length == prefix.Length)
            {
                return false;
            }

            var digits = groupContent[prefix.Length..];

            foreach (var character in digits)
            {
                if (!char.IsAsciiDigit(character))
                {
                    return false;
                }
            }

            // A digit run too long for int is simply a huge out-of-range value.
            var value = int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : int.MaxValue;

            if (_rules.OutOfRangeValues == OutOfRangeValueHandling.Clamp)
            {
                // 06 §2.2, §4: the Advantage360 clamps into the device range ({s0} loads as 1).
                result = Math.Clamp(
                    value,
                    range?.Minimum ?? MacroKeystrokeRenderer.MinTokenValue,
                    range?.Maximum ?? MacroKeystrokeRenderer.MaxTokenValue);

                return true;
            }

            if (value is >= MacroKeystrokeRenderer.MinTokenValue and <= MacroKeystrokeRenderer.MaxTokenValue)
            {
                result = value;

                return true;
            }

            // Out of range: the RGB family ignores the value (the default applies); the old FS
            // and Adv2 parsers substitute the device default (06 §2.2).
            if (_rules.OutOfRangeValues == OutOfRangeValueHandling.SubstituteDefault)
            {
                result = range?.Default ?? 0;
            }

            return true;
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
