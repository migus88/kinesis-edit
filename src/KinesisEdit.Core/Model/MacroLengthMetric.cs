using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The one place that answers "how long is this macro?" — because specs/06-macros.md §6
    /// measures the per-macro limit two different ways and every consumer has to agree with
    /// <see cref="KeyboardLayout.Validate"/>, which is what stops a save.
    /// <list type="bullet">
    /// <item>The Advantage360's 500 is "the length of the serialized <b>macro text</b>": the value
    /// side of the 06 §3 line, i.e. the keystroke tokens alone
    /// (<see cref="MacroKeystrokeRenderer.KeystrokeTextLength"/>), without the trigger, the
    /// co-triggers or the <c>{sN}</c>/<c>{xN}</c> markers.</item>
    /// <item>Every other family's 300 is a keystroke count, and 04 §5.3 defines keystroke
    /// accounting once for the whole document — 1 per keystroke plus 2 per attached modifier — so
    /// it is the same <see cref="Macro.WeightedKeystrokeCount"/> that feeds the 7200 layout
    /// budget.</item>
    /// </list>
    /// Which of the two applies is a property of the macro <em>store</em>, not of the device id:
    /// the serialized-text metric belongs to the flat-list (Gen2) family
    /// (<see cref="KeyboardLayout.UsesFlatMacroList"/>).
    /// </summary>
    public static class MacroLengthMetric
    {
        /// <summary>Name of the serialized-text metric, for a report that quotes its unit.</summary>
        public const string SerializedTextUnit = "serialized keystroke characters";

        /// <summary>Name of the weighted-keystroke metric, for a report that quotes its unit.</summary>
        public const string WeightedKeystrokeUnit = "weighted keystrokes";

        /// <summary>
        /// Whether <paramref name="layout"/>'s per-macro limit is measured in serialized macro-text
        /// characters (Advantage360) rather than in weighted keystrokes.
        /// </summary>
        public static bool UsesSerializedTextLength(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            return layout.UsesFlatMacroList;
        }

        /// <summary>
        /// The unit <see cref="Measure(Macro, KeyboardLayout)"/> reports in for
        /// <paramref name="layout"/>.
        /// </summary>
        public static string UnitFor(KeyboardLayout layout)
        {
            return UsesSerializedTextLength(layout) ? SerializedTextUnit : WeightedKeystrokeUnit;
        }

        /// <summary>
        /// Measures <paramref name="macro"/> against <paramref name="layout"/>'s per-macro metric
        /// (<see cref="Devices.MacroCapability.MaxCharactersPerMacro"/>).
        /// </summary>
        public static int Measure(Macro macro, KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(macro);
            ArgumentNullException.ThrowIfNull(layout);

            return Measure(macro, UsesSerializedTextLength(layout), layout.Dialect);
        }

        /// <summary>
        /// The metric without a layout in hand: <paramref name="usesSerializedTextLength"/> picks
        /// the Advantage360 rule, <paramref name="dialect"/> is the tokens it renders in.
        /// </summary>
        public static int Measure(Macro macro, bool usesSerializedTextLength, TokenDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(macro);

            return usesSerializedTextLength
                ? MacroKeystrokeRenderer.KeystrokeTextLength(macro, dialect)
                : macro.WeightedKeystrokeCount;
        }
    }
}
