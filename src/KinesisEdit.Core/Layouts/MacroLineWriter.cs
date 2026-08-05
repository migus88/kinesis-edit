using System.Globalization;
using System.Text;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Writes one macro line per specs/06-macros.md §3, wrapping
    /// <see cref="MacroKeystrokeRenderer.RenderKeystrokes"/> for the keystroke section so the
    /// modifier-diffing logic lives in one place. The dialect variants of §3 are handled here:
    /// the RGB-family/Gen2 form always writes <c>{sN}</c>/<c>{xN}</c> for values 0..9, the old
    /// FS form writes them only when ≥ 1 and truncates to its persisted co-trigger count, the
    /// Adv2 form writes <c>{speedN}</c> (1..9), no repeat token, and <c>kp-</c> prefixes on the
    /// trigger/co-trigger tokens of keypad-layer macros.
    /// </summary>
    internal static class MacroLineWriter
    {
        /// <summary>
        /// Writes the line: layer prefix, up to the persisted number of co-triggers, the
        /// trigger, <c>&gt;</c>, the speed/repeat markers, then the keystrokes (06 §3).
        /// </summary>
        public static string Write(
            Macro macro,
            KeyDefinition trigger,
            LayoutDialectRules rules,
            bool isBottomLayer,
            string linePrefix)
        {
            var builder = new StringBuilder(linePrefix);
            var coTriggerCount = Math.Min(macro.CoTriggers.Count, rules.PersistedCoTriggers);

            for (var i = 0; i < coTriggerCount; i++)
            {
                AppendGroup(builder, macro.CoTriggers[i], rules, isBottomLayer);
            }

            AppendGroup(builder, trigger, rules, isBottomLayer);
            builder.Append('>');
            AppendSpeedAndRepeat(builder, macro, rules);
            builder.Append(MacroKeystrokeRenderer.RenderKeystrokes(macro, rules.Tokens));

            return builder.ToString();
        }

        private static void AppendGroup(StringBuilder builder, KeyDefinition key, LayoutDialectRules rules, bool isBottomLayer)
        {
            var token = LayoutTokenResolver.GetWriteToken(key, rules.Tokens);

            if (token.Length == 0)
            {
                // A key with no file token in any dialect writes nothing (05 §3.9).
                return;
            }

            builder.Append('{');

            // 06 §3: the Adv2 serializer writes kp- prefixes on trigger/co-trigger tokens of
            // keypad-layer macros, except for the keypad-exception tokens (04 §3.2).
            if (isBottomLayer && rules.UsesKeypadTokenPrefix && !Advantage2KeypadExceptions.IsExceptionToken(token))
            {
                builder.Append(Advantage2KeypadExceptions.KeypadPrefix);
            }

            builder.Append(token).Append('}');
        }

        private static void AppendSpeedAndRepeat(StringBuilder builder, Macro macro, LayoutDialectRules rules)
        {
            AppendValue(builder, rules.SpeedTokenPrefix, macro.Speed, rules);

            if (rules.ParsesRepeatToken)
            {
                AppendValue(builder, "x", macro.RepeatFrequency, rules);
            }
        }

        private static void AppendValue(StringBuilder builder, string prefix, int value, LayoutDialectRules rules)
        {
            if (value < rules.MinimumWrittenSpeedOrRepeat || value > MacroKeystrokeRenderer.MaxTokenValue)
            {
                return;
            }

            builder
                .Append('{')
                .Append(prefix)
                .Append(value.ToString(CultureInfo.InvariantCulture))
                .Append('}');
        }
    }
}
