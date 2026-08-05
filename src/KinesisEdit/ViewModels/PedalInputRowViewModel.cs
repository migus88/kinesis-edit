using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One of the seven rows of the pedal view (specs/12-savant-elite.md §5, "Right panel"): the
    /// input's caption, whether it carries a single action or a macro, and the assignment text
    /// <see cref="PedalActionDescriber"/> renders for it. Immutable — the view is read-only, so
    /// nothing here ever changes after the file is loaded.
    /// </summary>
    public sealed class PedalInputRowViewModel : ViewModelBase
    {
        /// <summary>Assignment text of an input the file programs nothing onto.</summary>
        public const string UnassignedCaption = "Not assigned";

        /// <summary>Mode caption of an input carrying one key or mouse action (12 §4.2).</summary>
        public const string SingleModeCaption = "Single action";

        /// <summary>Mode caption of an input carrying a macro.</summary>
        public const string MacroModeCaption = "Macro";

        /// <summary>Which of the seven inputs this row is.</summary>
        public PedalInputId Id { get; }

        /// <summary>The row caption of 12 §5 — "Left Pedal", "Jack 1", and so on.</summary>
        public string Name { get; }

        /// <summary>The input's mode; the view maps it to nothing but the caption below.</summary>
        public PedalInputMode Mode { get; }

        /// <summary>Human-readable <see cref="Mode"/>; empty while the input is unassigned.</summary>
        public string ModeCaption { get; }

        /// <summary>The assignment as 12 §5 renders it, or <see cref="UnassignedCaption"/>.</summary>
        public string AssignmentText { get; }

        /// <summary>Whether the input fires anything — the view styles the other rows as muted.</summary>
        public bool IsAssigned { get; }

        /// <summary>Creates the row for <paramref name="input"/>.</summary>
        public PedalInputRowViewModel(PedalInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var assignment = PedalActionDescriber.Describe(input);

            Id = input.Id;
            Name = PedalInputs.GetDisplayName(input.Id);
            Mode = input.Mode;

            // An assigned input always describes to something, but the two conditions are asked
            // together anyway: a row that says "Macro" next to an empty string would be a worse
            // answer than one that says the input is unassigned.
            IsAssigned = input.IsAssigned && assignment.Length > 0;
            AssignmentText = IsAssigned ? assignment : UnassignedCaption;
            ModeCaption = IsAssigned ? GetModeCaption(input.Mode) : string.Empty;
        }

        private static string GetModeCaption(PedalInputMode mode)
        {
            return mode switch
            {
                PedalInputMode.Single => SingleModeCaption,
                PedalInputMode.Macro => MacroModeCaption,
                _ => string.Empty
            };
        }
    }
}
