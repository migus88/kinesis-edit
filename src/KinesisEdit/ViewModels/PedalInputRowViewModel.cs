using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One of the seven rows of the pedal editor (specs/12-savant-elite.md §5, "Right panel"): the
    /// input's caption, whether it carries a single action or a macro, and the assignment text
    /// <see cref="PedalActionDescriber"/> renders for it — plus the two editor flags §5 shows next
    /// to a row, "being edited" and <c>*Modified</c>.
    /// <para>
    /// The row wraps the live <see cref="PedalInput"/> and re-reads it on demand:
    /// <see cref="PedalInput"/> is a plain model object with no change notification (like
    /// <see cref="KeyboardKeyViewModel"/> over <c>KeyboardKey</c>), so whoever mutates it calls
    /// <see cref="RefreshFromModel"/> afterwards.
    /// </para>
    /// </summary>
    public sealed class PedalInputRowViewModel : ViewModelBase
    {
        /// <summary>Assignment text of an input the file programs nothing onto.</summary>
        public const string UnassignedCaption = "Not assigned";

        /// <summary>Mode caption of an input carrying one key or mouse action (12 §4.2).</summary>
        public const string SingleModeCaption = "Single action";

        /// <summary>Mode caption of an input carrying a macro.</summary>
        public const string MacroModeCaption = "Macro";

        /// <summary>Badge shown on a row edited since the file was loaded (12 §5 <c>*Modified</c>).</summary>
        public const string ModifiedCaption = "Modified";

        /// <summary>Which of the seven inputs this row is.</summary>
        public PedalInputId Id { get; }

        /// <summary>The row caption of 12 §5 — "Left Pedal", "Jack 1", and so on.</summary>
        public string Name { get; }

        /// <summary>The model behind the row; the edit session commits into exactly this object.</summary>
        public PedalInput Input { get; }

        /// <summary>The input's mode; the view maps it to nothing but the caption below.</summary>
        public PedalInputMode Mode
        {
            get => _mode;
            private set => SetProperty(ref _mode, value);
        }

        /// <summary>Human-readable <see cref="Mode"/>; empty while the input is unassigned.</summary>
        public string ModeCaption
        {
            get => _modeCaption;
            private set => SetProperty(ref _modeCaption, value);
        }

        /// <summary>The assignment as 12 §5 renders it, or <see cref="UnassignedCaption"/>.</summary>
        public string AssignmentText
        {
            get => _assignmentText;
            private set => SetProperty(ref _assignmentText, value);
        }

        /// <summary>Whether the input fires anything — the view styles the other rows as muted.</summary>
        public bool IsAssigned
        {
            get => _isAssigned;
            private set => SetProperty(ref _isAssigned, value);
        }

        /// <summary>Whether this row is the one the entry box is currently editing (12 §5 step 1).</summary>
        public bool IsEditing
        {
            get => _isEditing;
            internal set => SetProperty(ref _isEditing, value);
        }

        /// <summary>Whether the row changed since the file was loaded (12 §5's <c>*Modified</c> label).</summary>
        public bool IsModified
        {
            get => _isModified;
            internal set => SetProperty(ref _isModified, value);
        }

        private PedalInputMode _mode;
        private string _modeCaption = string.Empty;
        private string _assignmentText = UnassignedCaption;
        private bool _isAssigned;
        private bool _isEditing;
        private bool _isModified;

        /// <summary>Creates the row for <paramref name="input"/>.</summary>
        public PedalInputRowViewModel(PedalInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            Id = input.Id;
            Name = PedalInputs.GetDisplayName(input.Id);
            Input = input;

            RefreshFromModel();
        }

        /// <summary>
        /// Re-reads the input. Called after a committed edit — <see cref="PedalInput"/> raises no
        /// change notification of its own.
        /// </summary>
        public void RefreshFromModel()
        {
            var assignment = PedalActionDescriber.Describe(Input);

            // An assigned input always describes to something, but the two conditions are asked
            // together anyway: a row that says "Macro" next to an empty string would be a worse
            // answer than one that says the input is unassigned.
            IsAssigned = Input.IsAssigned && assignment.Length > 0;
            Mode = Input.Mode;
            AssignmentText = IsAssigned ? assignment : UnassignedCaption;
            ModeCaption = IsAssigned ? GetModeCaption(Input.Mode) : string.Empty;
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
