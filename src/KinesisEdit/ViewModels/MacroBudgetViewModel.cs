using System.Globalization;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The three macro budgets the panel reads out, all taken from the device's
    /// <see cref="MacroCapability"/> and never from a literal (specs/06-macros.md §6,
    /// specs/04-layout-file-format.md §5.3):
    /// <list type="bullet">
    /// <item>the macro under edit against <see cref="MacroCapability.MaxCharactersPerMacro"/> (300
    /// on Gen1, 500 on the Advantage360), measured by <see cref="MacroLengthMetric"/> — which is
    /// the same code <see cref="KeyboardLayout.Validate"/> uses, so the readout and the gate that
    /// stops the save can never disagree about a macro's length;</item>
    /// <item>the profile against <see cref="MacroCapability.MaxTotalKeystrokes"/> (7200), measured
    /// as <see cref="KeyboardLayout.TotalKeystrokes"/> — always the weighted keystroke count, on
    /// every family;</item>
    /// <item>the number of macros against the firmware-resolved macro count the panel passes in
    /// (<see cref="MacroCapability.MaxMacroCount"/>, raised to
    /// <see cref="MacroCapability.GatedMaxMacroCount"/> where the gate is met), which is
    /// <b>null where the spec states none</b> — the Advantage2 has no macro-count limit, and a
    /// null must read as "no limit", never as zero.</item>
    /// </list>
    /// <para>
    /// Breaching a budget is <b>reported, never refused</b>: Core's model keeps the same rule
    /// (docs/app/keyboard-model.md, invariant 1) and <see cref="KeyboardLayout.Validate"/> is the
    /// backstop that stops the save.
    /// </para>
    /// </summary>
    public sealed class MacroBudgetViewModel : ViewModelBase
    {
        /// <summary>
        /// The per-macro overflow warning of specs/06-macros.md §6 and specs/04 §5.3, verbatim
        /// but for the limit, which comes from the device rather than from a literal 300.
        /// </summary>
        public const string LengthLimitMessageFormat = "Macros are limited to approximately {0} characters.";

        /// <summary>Separator of a "value / limit" readout.</summary>
        public const string CaptionSeparator = " / ";

        /// <summary>Builds the §6 overflow warning for <paramref name="limit"/> characters.</summary>
        public static string BuildLengthLimitMessage(int limit)
        {
            return string.Format(CultureInfo.InvariantCulture, LengthLimitMessageFormat, limit);
        }

        /// <summary>
        /// Length of the macro under edit in the device's own per-macro metric: weighted
        /// keystrokes (1 per keystroke + 2 per attached modifier) everywhere except the
        /// Advantage360, whose 500 counts serialized macro-text characters
        /// (<see cref="MacroLengthMetric"/>).
        /// </summary>
        public int MacroLength
        {
            get => _macroLength;
            private set
            {
                if (SetProperty(ref _macroLength, value))
                {
                    OnPropertyChanged(nameof(MacroCaption));
                    OnPropertyChanged(nameof(IsMacroOverBudget));
                    OnPropertyChanged(nameof(OverflowMessage));
                }
            }
        }

        /// <summary>The device's per-macro cap, or null where the spec states none.</summary>
        public int? MaxMacroLength { get; }

        /// <summary>Whether the macro under edit is past its cap (06 §6).</summary>
        public bool IsMacroOverBudget => MaxMacroLength is int limit && MacroLength > limit;

        /// <summary>"12 / 300" for the macro under edit.</summary>
        public string MacroCaption => BuildCaption(MacroLength, MaxMacroLength);

        /// <summary>The profile's keystroke total (04 §5.3).</summary>
        public int TotalKeystrokes
        {
            get => _totalKeystrokes;
            private set
            {
                if (SetProperty(ref _totalKeystrokes, value))
                {
                    OnPropertyChanged(nameof(TotalCaption));
                    OnPropertyChanged(nameof(IsTotalOverBudget));
                }
            }
        }

        /// <summary>The device's per-layout keystroke budget (7200), or null where none is stated.</summary>
        public int? MaxTotalKeystrokes { get; }

        /// <summary>Whether the profile is past its keystroke budget.</summary>
        public bool IsTotalOverBudget => MaxTotalKeystrokes is int limit && TotalKeystrokes > limit;

        /// <summary>"120 / 7200" for the profile.</summary>
        public string TotalCaption => BuildCaption(TotalKeystrokes, MaxTotalKeystrokes);

        /// <summary>How many macros the profile holds (<see cref="KeyboardLayout.MacroCount"/>).</summary>
        public int MacroCount
        {
            get => _macroCount;
            private set
            {
                if (SetProperty(ref _macroCount, value))
                {
                    OnPropertyChanged(nameof(MacroCountCaption));
                    OnPropertyChanged(nameof(IsMacroCountOverBudget));
                }
            }
        }

        /// <summary>The firmware-resolved macro count limit (09 §2), or null where there is none.</summary>
        public int? MaxMacroCount { get; }

        /// <summary>Whether the profile already holds more macros than the device allows.</summary>
        public bool IsMacroCountOverBudget => MaxMacroCount is int limit && MacroCount > limit;

        /// <summary>"3 / 100" for the profile's macro count; just "3" where the device states no limit.</summary>
        public string MacroCountCaption => BuildCaption(MacroCount, MaxMacroCount);

        /// <summary>The §6 overflow warning while the macro under edit is over its cap, else null.</summary>
        public string? OverflowMessage =>
            IsMacroOverBudget && MaxMacroLength is int limit ? BuildLengthLimitMessage(limit) : null;

        private int _macroLength;
        private int _totalKeystrokes;
        private int _macroCount;

        /// <summary>
        /// Creates the readouts for <paramref name="capability"/>, with the macro count limit the
        /// firmware gate resolved (<paramref name="maxMacroCount"/>) rather than the raw baseline;
        /// null there means the device states no macro count at all.
        /// </summary>
        public MacroBudgetViewModel(MacroCapability capability, int? maxMacroCount)
        {
            ArgumentNullException.ThrowIfNull(capability);

            MaxMacroLength = capability.MaxCharactersPerMacro;
            MaxTotalKeystrokes = capability.MaxTotalKeystrokes;
            MaxMacroCount = maxMacroCount;
        }

        /// <summary>Re-reads all three readouts after any edit to <paramref name="layout"/>.</summary>
        public void RefreshFromModel(Macro? macro, KeyboardLayout? layout)
        {
            MacroLength = macro is not null && layout is not null ? MacroLengthMetric.Measure(macro, layout) : 0;
            TotalKeystrokes = layout?.TotalKeystrokes ?? 0;
            MacroCount = layout?.MacroCount ?? 0;
        }

        private static string BuildCaption(int value, int? limit)
        {
            var text = value.ToString(CultureInfo.InvariantCulture);

            return limit is int maximum
                ? text + CaptionSeparator + maximum.ToString(CultureInfo.InvariantCulture)
                : text;
        }
    }
}
