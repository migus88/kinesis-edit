namespace KinesisEdit.Services
{
    /// <summary>
    /// The leave-with-unsaved-changes question of docs/design/handoff.md §2 and mockup <c>1f</c>,
    /// as data: the title, the three answers, the opt-out and the two bodies, plus the one reading
    /// of an answer every caller must share.
    /// <para>
    /// <b>It exists so that neither editor owns the strings.</b> The keyboard editor and the pedal
    /// editor ask the same question about the same stakes, and the design draws it once; two
    /// hand-built <see cref="MessageBoxRequest"/>s drifted apart the moment one of them was
    /// touched. What differs between the two boards is a single sentence — which file the save
    /// writes and what makes the device reload it — so that sentence is the parameter and
    /// everything else is fixed here.
    /// </para>
    /// <para>
    /// Nothing in here decides anything: the suppression policy is
    /// <see cref="NotificationService"/>'s, and what to do with an answer is the editor's.
    /// </para>
    /// </summary>
    public static class UnsavedChangesPrompt
    {
        /// <summary>The modal's title, verbatim from mockup <c>1f</c>.</summary>
        public const string Title = "Save changes before leaving?";

        /// <summary>The affirmative: write the files, then go. Answers <c>Yes</c>.</summary>
        public const string SaveCaption = "Save";

        /// <summary>The answer that loses the work — drawn red, never the default. Answers <c>No</c> (or <c>Yes</c> when saving is impossible).</summary>
        public const string DiscardCaption = "Discard";

        /// <summary>The way out: stay in the editor and change nothing.</summary>
        public const string CancelCaption = "Cancel";

        /// <summary>
        /// The opt-out's wording, verbatim from mockup <c>1f</c>. It is a <b>promise</b>, not a
        /// "don't ask this again": ticking it arms auto-save on every future leave, which is why
        /// it is offered beside <see cref="SaveCaption"/> alone (see
        /// <see cref="MessageBoxRequest.SuppressionResult"/>) and never on a box that cannot save.
        /// </summary>
        public const string SuppressionCaption = "Don't ask again — always save on leaving";

        /// <summary>
        /// The keyboard editor's body. Mockup <c>1f</c> opens it with a mono count of the edited
        /// keys and layers; this app does not have a trustworthy count of *unsaved* keys — the
        /// session's dirty flag compares serialized files, not positions — so the sentence states
        /// the fact it can stand behind. The rest is the mockup's, and the eject clause is the law
        /// of docs/design/README.md: nothing ejects implicitly.
        /// </summary>
        public const string KeyboardMessage =
            "You have unsaved changes. Saving writes the layout files to the v-Drive. Eject when "
            + "you're done — the keyboard reloads on eject, and only you decide when that happens.";

        /// <summary>
        /// The pedal editor's body. Same shape, different last clause: the Savant Elite2 has no
        /// eject at all (specs/12-savant-elite2.md §5 step 7) — the firmware reloads when the user
        /// flips the board back to play mode.
        /// </summary>
        public const string PedalMessage =
            "You have unsaved changes. Saving writes the pedal configuration file to the v-Drive. "
            + "Switch your Savant Elite2 back to play mode to apply them.";

        /// <summary>
        /// The title when saving is impossible. It cannot be <see cref="Title"/>: that sentence
        /// offers a save, and this card has no Save button on it.
        /// </summary>
        public const string CannotSaveTitle = "Leave without saving?";

        /// <summary>
        /// The body when saving is impossible — a demo session, a read-only profile, a file that
        /// could not be read.
        /// <para>
        /// It <b>replaces</b> the board-specific body rather than joining it, and that is the point:
        /// every clause of <see cref="KeyboardMessage"/> and <see cref="PedalMessage"/> describes a
        /// save. "Saving writes the layout files to the v-Drive" over a card offering only Cancel
        /// and Discard is a promise the app cannot keep — it read as a bug the moment the box was
        /// rendered, which is why this constant exists.
        /// </para>
        /// </summary>
        public const string CannotSaveMessage =
            "You have unsaved changes and they cannot be written to this device. Leave the editor "
            + "and discard them?";

        /// <summary>Title of the toast raised when a navigation is refused because a save is running.</summary>
        public const string SaveInProgressTitle = "Saving";

        /// <summary>
        /// Message of that toast — the fourth outcome this prompt does not model, and the reason it
        /// lives here rather than on one editor: both boards refuse the same way. Leaving mid-save
        /// would dispose the editor while the write is still running, and the question would be the
        /// wrong one anyway (the changes are being saved, not unsaveable). The loading card is
        /// blocking, so the pointer can no longer reach Home — this is the invariant behind that
        /// scrim, and it speaks so that a path which is not a click does not appear to do nothing.
        /// </summary>
        public const string SaveInProgressMessage = "Please wait for the save to finish.";

        /// <summary>
        /// The box to raise, dressed for what the session can actually do.
        /// </summary>
        /// <param name="message">
        /// The body — <see cref="KeyboardMessage"/> or <see cref="PedalMessage"/>; the only part
        /// that differs between the two editors. Used on the savable path only: the other one has
        /// its own wording, for the reason <see cref="CannotSaveMessage"/> gives.
        /// </param>
        /// <param name="canSave">
        /// Whether saving is possible <b>at all</b> — a writable drive and a session that may be
        /// written. False degrades the three answers to a Yes/No <i>discard</i> question: offering
        /// a Save that cannot succeed would trap the user in the editor with no way out.
        /// </param>
        /// <param name="canSuppress">
        /// Whether the opt-out is offered. It must be false wherever the user could not <b>undo</b>
        /// it: the checkbox is only re-tickable from the "App &amp; notifications" section of the
        /// Settings tab, and a board that renders no Settings tab (the Savant Elite2 is
        /// <c>SettingsCapability.None</c>) would leave a user who ticked it with auto-save armed
        /// for good. It is also ignored when <paramref name="canSave"/> is false — there is nothing
        /// to "always save".
        /// </param>
        public static MessageBoxRequest Build(string message, bool canSave, bool canSuppress)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            if (!canSave)
            {
                return new MessageBoxRequest
                {
                    Title = CannotSaveTitle,
                    Message = CannotSaveMessage,
                    Icon = MessageBoxIcon.Warning,
                    Buttons = MessageBoxButtons.YesNo,
                    YesCaption = DiscardCaption,
                    NoCaption = UnsavedChangesPrompt.CancelCaption,
                    // The affirmative is the destructive one here, because it is the only one that
                    // moves: there is nowhere to put the work, so leaving always loses it.
                    DestructiveResult = MessageBoxResult.Yes,
                    IsWide = true
                };
            }

            return new MessageBoxRequest
            {
                Title = UnsavedChangesPrompt.Title,
                Message = message,
                Icon = MessageBoxIcon.Confirmation,
                Buttons = MessageBoxButtons.YesNoCancel,
                YesCaption = SaveCaption,
                NoCaption = DiscardCaption,
                CancelCaption = UnsavedChangesPrompt.CancelCaption,
                DestructiveResult = MessageBoxResult.No,
                IsWide = true,
                SuppressionKey = canSuppress ? NotificationKeys.UnsavedChanges : null,
                SuppressionCaption = canSuppress ? UnsavedChangesPrompt.SuppressionCaption : null,
                // A suppressed prompt means "save and go", which is the promise the checkbox made;
                // and the promise is only recorded when it was made beside the answer that keeps
                // it, so ticking the box and then pressing Discard arms nothing.
                SuppressedResult = MessageBoxResult.Yes,
                SuppressionResult = canSuppress ? MessageBoxResult.Yes : null
            };
        }

        /// <summary>
        /// Reads one answer. <paramref name="canSave"/> has to be passed because <c>Yes</c> means
        /// Save in the three-button box and Discard in the two-button one — the same
        /// <see cref="MessageBoxResult"/>, two different intents.
        /// <para>
        /// <b>Everything that is not an explicit answer is <see cref="UnsavedChangesAnswer.Cancel"/></b>:
        /// a null outcome (the box could not be put on screen), an Escape, a window close. Losing
        /// work because the question failed is the exact outcome this guard exists to prevent, so
        /// the fallback keeps the editor open rather than letting the navigation through.
        /// </para>
        /// </summary>
        public static UnsavedChangesAnswer Interpret(MessageBoxOutcome? outcome, bool canSave)
        {
            return outcome?.Result switch
            {
                MessageBoxResult.Yes => canSave ? UnsavedChangesAnswer.Save : UnsavedChangesAnswer.Discard,
                MessageBoxResult.No when canSave => UnsavedChangesAnswer.Discard,
                _ => UnsavedChangesAnswer.Cancel
            };
        }
    }

    /// <summary>
    /// What the user answered the unsaved-changes prompt. <see cref="Cancel"/> is 0 on purpose:
    /// it is the safe answer, so a value nobody set means "stay in the editor".
    /// </summary>
    public enum UnsavedChangesAnswer
    {
        /// <summary>Stay in the editor and change nothing. Also every non-answer.</summary>
        Cancel = 0,

        /// <summary>Leave, losing the unsaved work.</summary>
        Discard = 1,

        /// <summary>Write the files, then leave — but only if the write succeeds.</summary>
        Save = 2
    }
}
