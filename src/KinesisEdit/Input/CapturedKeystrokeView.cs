using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Input
{
    /// <summary>
    /// One row of the capture spike's log: a <see cref="CapturedKeystroke"/> flattened into the
    /// display strings <c>MainWindow</c> binds to. Presentation only — it holds no capture logic.
    /// </summary>
    public sealed class CapturedKeystrokeView
    {
        private const string MissingToken = "—";
        private const string NoModifiers = "(none)";
        private const string ModifierSeparator = "+";

        private static readonly TokenDialect[] _tokenPreference =
        {
            TokenDialect.Legacy,
            TokenDialect.Gen1,
            TokenDialect.Gen2
        };

        /// <summary>Key-cap caption of the captured key, on one line (specs/05-key-model.md §1.1).</summary>
        public string DisplayText { get; }

        /// <summary>The key-table code (specs/05-key-model.md §2), e.g. <c>160</c> for left Shift.</summary>
        public string KeyCode { get; }

        /// <summary>Legacy-dialect file token, or a dash when the key has none.</summary>
        public string LegacyToken { get; }

        /// <summary>Gen1-dialect file token, or a dash when the key has none.</summary>
        public string Gen1Token { get; }

        /// <summary>Gen2-dialect file token, or a dash when the key has none.</summary>
        public string Gen2Token { get; }

        /// <summary>
        /// The modifiers held at capture time, as tokens joined with <c>+</c> (for example
        /// <c>lshift+lctrl</c>) — the visible proof of the left/right distinction.
        /// </summary>
        public string HeldModifiers { get; }

        /// <summary>Flattens <paramref name="keystroke"/> into its display strings.</summary>
        public CapturedKeystrokeView(CapturedKeystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);

            DisplayText = DescribeKey(keystroke.Key);
            KeyCode = keystroke.Key.Code.ToString();
            LegacyToken = DescribeToken(keystroke.Key, TokenDialect.Legacy);
            Gen1Token = DescribeToken(keystroke.Key, TokenDialect.Gen1);
            Gen2Token = DescribeToken(keystroke.Key, TokenDialect.Gen2);
            HeldModifiers = DescribeModifiers(keystroke.HeldModifiers);
        }

        private static string DescribeKey(KeyDefinition key)
        {
            // GetDisplayText covers the per-dialect overrides only; the macOS caption is a
            // separate property (specs/05-key-model.md §3.3/§3.5).
            var caption = OperatingSystem.IsMacOS() && key.MacDisplayText.Length > 0
                ? key.MacDisplayText
                : key.DisplayText;

            if (caption.Length == 0)
            {
                caption = PreferredToken(key);
            }

            return caption.Replace("\n", " ");
        }

        private static string DescribeToken(KeyDefinition key, TokenDialect dialect)
        {
            var token = key.GetToken(dialect);

            return token.Length > 0 ? token : MissingToken;
        }

        private static string DescribeModifiers(IReadOnlyList<KeyDefinition> modifiers)
        {
            if (modifiers.Count == 0)
            {
                return NoModifiers;
            }

            return string.Join(ModifierSeparator, modifiers.Select(PreferredToken));
        }

        private static string PreferredToken(KeyDefinition key)
        {
            foreach (var dialect in _tokenPreference)
            {
                var token = key.GetToken(dialect);

                if (token.Length > 0)
                {
                    return token;
                }
            }

            return MissingToken;
        }
    }
}
