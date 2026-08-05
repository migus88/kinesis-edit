using System.Globalization;
using System.Text;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Renders a macro to its serialized line per specs/06-macros.md §3: layer prefix,
    /// co-triggers, trigger, <c>&gt;</c>, <c>{sN}</c>, <c>{xN}</c>, then the keystrokes with their
    /// modifier transitions. This is the RGB-family/Gen2 form of §3 — the Advantage2
    /// <c>{speedN}</c> syntax and the old Freestyle omissions are serializer variants and live
    /// with the serializers, as does everything above one line (layer headers, file assembly,
    /// invalid-line preservation). The model itself uses only
    /// <see cref="KeystrokeTextLength"/>, the Advantage360 500-character metric of 06 §6.
    /// </summary>
    public static class MacroKeystrokeRenderer
    {
        /// <summary>Lowest speed/repeat value that is written as a token (06 §3).</summary>
        public const int MinTokenValue = 0;

        /// <summary>Highest speed/repeat value that is written as a token (06 §3).</summary>
        public const int MaxTokenValue = 9;

        /// <summary>The pedal different-press-release token — a single space between braces (06 §2.2, §7.6).</summary>
        public const string DifferentPressReleaseToken = "{ }";

        /// <summary>The Gen1-family bottom-layer line prefix (04 §3.1, 06 §3 step 1).</summary>
        public const string FnLayerPrefix = "fn ";

        /// <summary>Index of the bottom (Fn/keypad) layer on the two-layer devices (04 §3.1, §3.2).</summary>
        public const int BottomLayerIndex = 1;

        /// <summary>
        /// The layer prefix a line of <paramref name="dialect"/> carries on layer
        /// <paramref name="layerIndex"/> (04 §3.1–§3.3, 06 §3 step 1): <c>fn </c> for the bottom
        /// layer of the Gen1 family, and nothing anywhere else — the Legacy dialect marks the
        /// keypad layer with a <c>kp-</c> prefix <em>inside</em> the first bracket and Gen2 with a
        /// <c>&lt;...&gt;</c> header line, neither of which is part of the line's own text.
        /// </summary>
        public static string LayerPrefixFor(TokenDialect dialect, int layerIndex)
        {
            return dialect == TokenDialect.Gen1 && layerIndex == BottomLayerIndex ? FnLayerPrefix : string.Empty;
        }

        /// <summary>
        /// Renders the full macro line of 06 §3 in <paramref name="dialect"/>'s tokens.
        /// <paramref name="layerPrefix"/> is the caller's layer prefix (<see cref="LayerPrefixFor"/>
        /// answers it: "fn " for the bottom layer on the Gen1 family, empty on Gen2 where the layer
        /// comes from the <c>&lt;...&gt;</c> header; 04 §3.1, §3.3).
        /// </summary>
        public static string Render(Macro macro, TokenDialect dialect, string layerPrefix = "")
        {
            ArgumentNullException.ThrowIfNull(macro);
            ArgumentNullException.ThrowIfNull(layerPrefix);

            var builder = new StringBuilder();

            builder.Append(layerPrefix);

            foreach (var coTrigger in macro.CoTriggers)
            {
                AppendToken(builder, ResolveToken(coTrigger, dialect));
            }

            AppendToken(builder, ResolveTriggerToken(macro, dialect));
            builder.Append('>');
            AppendValue(builder, 's', macro.Speed);
            AppendValue(builder, 'x', macro.RepeatFrequency);
            AppendKeystrokes(builder, macro, dialect);

            return builder.ToString();
        }

        /// <summary>
        /// Renders only the keystroke section of 06 §3 — the tokens right of the speed/repeat
        /// markers, including the trailing close-out of still-held modifiers.
        /// </summary>
        public static string RenderKeystrokes(Macro macro, TokenDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(macro);

            var builder = new StringBuilder();

            AppendKeystrokes(builder, macro, dialect);

            return builder.ToString();
        }

        /// <summary>
        /// Length of the serialized *macro* text — the metric the Advantage360 per-macro limit of
        /// 500 is measured in (06 §6, <see cref="Devices.MacroCapability.MaxCharactersPerMacro"/>).
        /// That is the value side of the §3 line, so it measures
        /// <see cref="RenderKeystrokes"/> and excludes the trigger, the co-triggers, the layer
        /// prefix and the <c>{sN}</c>/<c>{xN}</c> markers. Distinct from
        /// <see cref="Macro.WeightedKeystrokeCount"/>, which feeds the 7200 layout budget and the
        /// 300 per-macro cap of every other family.
        /// </summary>
        public static int KeystrokeTextLength(Macro macro, TokenDialect dialect)
        {
            return RenderKeystrokes(macro, dialect).Length;
        }

        private static void AppendKeystrokes(StringBuilder builder, Macro macro, TokenDialect dialect)
        {
            var held = MacroModifiers.None;

            foreach (var keystroke in macro.Keystrokes)
            {
                // 05 §3.9: a key with no file token in any dialect (the Advantage2 Program button)
                // writes no group at all, so it must not drag its modifier transitions in either.
                var token = ResolveToken(keystroke.Key, dialect);

                if (token.Length == 0)
                {
                    continue;
                }

                var current = MacroModifierCodes.Canonicalize(keystroke.Modifiers);

                AppendModifierTransitions(builder, held, current, dialect);
                AppendKeystroke(builder, keystroke, token);

                held = current;
            }

            // 06 §3: after the last keystroke every still-held modifier is closed with {+token}.
            AppendModifierTransitions(builder, held, MacroModifiers.None, dialect);
        }

        /// <summary>
        /// 06 §3: the transitions are the diff of the previous keystroke's held-modifier set
        /// against the current one — every modifier no longer held emits <c>{+token}</c>, every
        /// newly held one emits <c>{-token}</c>. Both sets arrive canonicalised by modifier key code
        /// (05 §5.1), so the diff is between held *keys*: a set holding both <c>W </c> and
        /// <c>LW</c> presses Left Win once, and a keystroke spelling it <c>W </c> followed by one
        /// spelling it <c>LW</c> writes no transition at all — the key never left the held set.
        /// </summary>
        private static void AppendModifierTransitions(
            StringBuilder builder,
            MacroModifiers previous,
            MacroModifiers current,
            TokenDialect dialect)
        {
            foreach (var released in MacroModifierCodes.Enumerate(previous & ~current))
            {
                AppendDirectedToken(builder, KeyDirection.Up, ResolveModifierToken(released, dialect));
            }

            foreach (var pressed in MacroModifierCodes.Enumerate(current & ~previous))
            {
                AppendDirectedToken(builder, KeyDirection.Down, ResolveModifierToken(pressed, dialect));
            }
        }

        private static void AppendKeystroke(StringBuilder builder, Keystroke keystroke, string token)
        {
            if (keystroke.DiffPressRelease)
            {
                // 06 §3, §7.6: {-token}{ }{+token} marks the pedal "different press and release" key.
                AppendDirectedToken(builder, KeyDirection.Down, token);
                builder.Append(DifferentPressReleaseToken);
                AppendDirectedToken(builder, KeyDirection.Up, token);

                return;
            }

            AppendDirectedToken(builder, keystroke.EffectiveDirection, token);
        }

        private static void AppendDirectedToken(StringBuilder builder, KeyDirection direction, string token)
        {
            if (token.Length == 0)
            {
                return;
            }

            builder.Append('{');

            switch (direction)
            {
                case KeyDirection.Down:
                    builder.Append('-');
                    break;

                case KeyDirection.Up:
                    builder.Append('+');
                    break;
            }

            builder.Append(token).Append('}');
        }

        private static void AppendToken(StringBuilder builder, string token)
        {
            AppendDirectedToken(builder, KeyDirection.None, token);
        }

        private static void AppendValue(StringBuilder builder, char prefix, int value)
        {
            if (value is < MinTokenValue or > MaxTokenValue)
            {
                return;
            }

            builder
                .Append('{')
                .Append(prefix)
                .Append(value.ToString(CultureInfo.InvariantCulture))
                .Append('}');
        }

        private static string ResolveTriggerToken(Macro macro, TokenDialect dialect)
        {
            if (macro.TriggerKey == Macro.UnassignedIndex)
            {
                return string.Empty;
            }

            var key = KeyRegistry.FindByCode(macro.TriggerKey);

            return key is null ? string.Empty : ResolveToken(key, dialect);
        }

        private static string ResolveModifierToken(MacroModifiers modifier, TokenDialect dialect)
        {
            var key = KeyRegistry.FindByCode(MacroModifierCodes.GetKeyCode(modifier));

            return key is null ? string.Empty : ResolveToken(key, dialect);
        }

        /// <summary>
        /// The entry's token in <paramref name="dialect"/>, falling back to the first dialect that
        /// names the key: the generic Shift/Ctrl/Alt keys are registered only in the Legacy table
        /// (05 §3.5) yet appear inside Gen1 macro values (06 §7.3). Empty when the key has no file
        /// token at all (05 §3.9, the Program button), in which case no group is written.
        /// </summary>
        private static string ResolveToken(KeyDefinition key, TokenDialect dialect)
        {
            var token = key.GetToken(dialect);

            if (token.Length > 0)
            {
                return token;
            }

            if (key.LegacyToken.Length > 0)
            {
                return key.LegacyToken;
            }

            return key.Gen1Token.Length > 0 ? key.Gen1Token : key.Gen2Token;
        }
    }
}
