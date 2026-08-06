namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One numbered instruction of the empty state's "get this device into a detectable state"
    /// panel (docs/design/mockups.md §1d).
    /// <para>
    /// A step is split into three parts rather than being one string because the middle one is a
    /// <b>literal that exists on the hardware or on the drive</b> — a keycap legend, an onboard
    /// shortcut, a volume label — and the mono law reserves mono type for exactly those. The view
    /// draws the three as inline runs of one sentence, so the type change happens mid-line without
    /// the sentence being broken up into separate controls.
    /// </para>
    /// <para>
    /// Immutable, and deliberately not a <see cref="ViewModelBase"/>: a step never changes: picking
    /// another device builds a new list.
    /// </para>
    /// </summary>
    public sealed class ConnectionStep
    {
        /// <summary>The 1-based position shown in the step's own marker.</summary>
        public int Number { get; }

        /// <summary>The sentence up to the literal; the whole sentence when there is none.</summary>
        public string LeadText { get; }

        /// <summary>The hardware/drive literal, in mono; empty when the step names none.</summary>
        public string ValueText { get; }

        /// <summary>The rest of the sentence after the literal; empty when there is none.</summary>
        public string TrailText { get; }

        /// <summary>Whether this step names a literal at all.</summary>
        public bool HasValue => ValueText.Length > 0;

        /// <summary>The whole step as one sentence, for tests and for accessibility.</summary>
        public string Text => LeadText + ValueText + TrailText;

        /// <summary>Creates a step. <paramref name="valueText"/> is the mono half and may be null.</summary>
        public ConnectionStep(int number, string leadText, string? valueText = null, string? trailText = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
            ArgumentNullException.ThrowIfNull(leadText);

            Number = number;
            LeadText = leadText;
            ValueText = valueText ?? string.Empty;
            TrailText = trailText ?? string.Empty;
        }
    }
}
