using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One footer meter of the key inspector's Macro panel (mockup <c>2i</c>): a label, a value, a
    /// limit, and whether the value is past it — <c>Playback speed 3 / 5</c>,
    /// <c>this macro 128 / 500</c>, <c>layout keystrokes 5 140 / 7 200</c>.
    /// <para>
    /// <b>The thousands separator is a space, and it is not restated here.</b> It is
    /// <see cref="AdvisoryText.Number"/>'s, which is the app's one grouped-number format (mockup
    /// <c>1i</c>) — a second formatter would be a second thing to disagree with the advisory
    /// sentences that quote the same budgets.
    /// </para>
    /// <para>
    /// <b>Over budget is amber and still saves.</b> A meter reports; it refuses nothing. Core's
    /// model keeps the same rule (docs/app/keyboard-model.md, invariant 1) and
    /// <c>KeyboardLayout.Validate</c> at save time is the backstop. The view paints
    /// <see cref="IsOverBudget"/> with <c>statusWarning</c> and never with the error ramp
    /// (docs/design/README.md — "advisories never block").
    /// </para>
    /// <para>
    /// <b>A null limit is "no limit", never zero.</b> The Advantage2 states no macro count and the
    /// Advantage2/Advantage360 state no per-macro speed range; the caption is then the bare number
    /// and nothing can be over budget.
    /// </para>
    /// </summary>
    public sealed class MacroMeterViewModel : ViewModelBase
    {
        /// <summary>Separates the value from its limit — <c>128 / 500</c>.</summary>
        public const string CaptionSeparator = " / ";

        /// <summary>What the meter is about, in the app's own voice (mockup <c>2i</c>).</summary>
        public string Label { get; }

        /// <summary>The current figure.</summary>
        public int Value
        {
            get => _value;
            private set
            {
                if (SetProperty(ref _value, value))
                {
                    OnPropertyChanged(nameof(Caption));
                    OnPropertyChanged(nameof(IsOverBudget));
                }
            }
        }

        /// <summary>The device's own limit, or null where it states none.</summary>
        public int? Limit
        {
            get => _limit;
            private set
            {
                if (SetProperty(ref _limit, value))
                {
                    OnPropertyChanged(nameof(Caption));
                    OnPropertyChanged(nameof(IsOverBudget));
                }
            }
        }

        /// <summary>The reading — <c>5 140 / 7 200</c>, or a bare number where there is no limit.</summary>
        public string Caption => Build(_value, _limit);

        /// <summary>Whether the value is past the limit. Amber, non-blocking; never red.</summary>
        public bool IsOverBudget => _limit is int limit && _value > limit;

        private int _value;
        private int? _limit;

        /// <summary>Creates a meter labelled <paramref name="label"/>, reading zero of no limit.</summary>
        public MacroMeterViewModel(string label)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        /// <summary>Builds one reading; the numbers are grouped the way every count in the app is.</summary>
        public static string Build(int value, int? limit)
        {
            var text = AdvisoryText.Number(value);

            return limit is int maximum ? text + CaptionSeparator + AdvisoryText.Number(maximum) : text;
        }

        /// <summary>Moves the reading. Both halves move together, so a caption is never half-stale.</summary>
        public void Set(int value, int? limit)
        {
            Limit = limit;
            Value = value;
        }
    }
}
