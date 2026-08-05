namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// One limit or rule breach found by <see cref="KeyboardLayout.Validate"/> — a report, never a
    /// refusal (limits: specs/04-layout-file-format.md §5.3, specs/06-macros.md §6). The UI decides
    /// what to show and the serializer decides what it can still write.
    /// </summary>
    public sealed record ModelViolation
    {
        /// <summary>What is out of bounds.</summary>
        public required ModelViolationKind Kind { get; init; }

        /// <summary>Human-readable description naming the limit and the actual value.</summary>
        public required string Message { get; init; }

        /// <summary>Layer the breach was found on, or null for layout-wide breaches.</summary>
        public int? LayerIndex { get; init; }

        /// <summary>Key position the breach was found on, or null when it is not key-specific.</summary>
        public int? KeyIndex { get; init; }

        /// <summary>Macro slot 1..5 involved, or null for flat-list macros and non-macro breaches.</summary>
        public int? MacroIndex { get; init; }

        /// <summary>The limit from the device catalog, or null when the breach is not a numeric limit.</summary>
        public int? Limit { get; init; }

        /// <summary>The value the model actually holds, or null when the breach is not a numeric limit.</summary>
        public int? ActualValue { get; init; }
    }
}
