namespace KinesisEdit.Services
{
    /// <summary>
    /// Everything needed to show one message box (specs/11-feature-dialogs.md §11.9). A
    /// <see cref="SuppressionKey"/> turns on the "Don't ask this again" checkbox and makes the
    /// request subject to the suppression policy of <see cref="NotificationService"/>.
    /// <para>
    /// The four <c>…Caption</c> overrides exist because mockup 1k labels the buttons by
    /// <b>outcome</b> — "Cancel · Key data only · Include macros" — rather than Yes/No/Cancel.
    /// They rename the buttons only: the <see cref="MessageBoxResult"/> a click produces is
    /// unchanged, so every caller's <c>switch</c> and every <see cref="SuppressedResult"/> keep
    /// their meaning. Yes stays the primary affirmative and No the secondary one whatever they
    /// are called.
    /// </para>
    /// <para>
    /// Four more knobs dress the leave-with-unsaved modal of docs/design/handoff.md §2 without
    /// giving it a view of its own: <see cref="IsWide"/> takes its 420 px card,
    /// <see cref="DestructiveResult"/> paints the answer that loses data red,
    /// <see cref="SuppressionCaption"/> renames the opt-out ("always save on leaving" is a
    /// promise, not "don't ask"), and <see cref="SuppressionResult"/> ties that promise to the
    /// one answer that can keep it.
    /// </para>
    /// </summary>
    public sealed record MessageBoxRequest
    {
        /// <summary>Dialog title.</summary>
        public required string Title { get; init; }

        /// <summary>Dialog message.</summary>
        public required string Message { get; init; }

        /// <summary>Dialog type, selecting the icon.</summary>
        public MessageBoxIcon Icon { get; init; } = MessageBoxIcon.Information;

        /// <summary>Standard button set.</summary>
        public MessageBoxButtons Buttons { get; init; } = MessageBoxButtons.Ok;

        /// <summary>Additional custom buttons, shown after the standard ones.</summary>
        public IReadOnlyList<MessageBoxButton> CustomButtons { get; init; } = [];

        /// <summary>Outcome-named label for the Yes button; null keeps the standard "Yes".</summary>
        public string? YesCaption { get; init; }

        /// <summary>Outcome-named label for the No button; null keeps the standard "No".</summary>
        public string? NoCaption { get; init; }

        /// <summary>Outcome-named label for the OK button; null keeps the standard "OK".</summary>
        public string? OkCaption { get; init; }

        /// <summary>Outcome-named label for the Cancel button; null keeps the standard "Cancel".</summary>
        public string? CancelCaption { get; init; }

        /// <summary>
        /// The <c>*_msg</c> key of specs/08-settings.md §3 this dialog can be suppressed with;
        /// null when the dialog is always shown (see <see cref="NotificationKeys"/>).
        /// </summary>
        public string? SuppressionKey { get; init; }

        /// <summary>Result reported when the dialog is suppressed and therefore never shown.</summary>
        public MessageBoxResult SuppressedResult { get; init; } = MessageBoxResult.Ok;

        /// <summary>Per-request caption for the suppression checkbox; null keeps the default.</summary>
        public string? SuppressionCaption { get; init; }

        /// <summary>
        /// Whether this box takes the design's wide (420 px) card instead of the default 330 px one.
        /// </summary>
        public bool IsWide { get; init; }

        /// <summary>
        /// The answer that loses data, if any — that button is drawn on the red <c>discard</c> theme.
        /// </summary>
        public MessageBoxResult? DestructiveResult { get; init; }

        /// <summary>
        /// Record a suppression answer only when the result is this one; null records it on any
        /// answer.
        /// </summary>
        public MessageBoxResult? SuppressionResult { get; init; }

        /// <summary>Whether the "Don't ask this again" checkbox belongs on this dialog.</summary>
        public bool HasSuppressionOption => !string.IsNullOrWhiteSpace(SuppressionKey);
    }
}
