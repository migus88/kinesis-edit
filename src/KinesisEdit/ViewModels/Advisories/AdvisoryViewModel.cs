namespace KinesisEdit.ViewModels.Advisories
{
    /// <summary>
    /// One anchored advisory sentence — a thing the app noticed about the open profile and is
    /// telling the user about, having changed nothing.
    /// <para>
    /// <b>Severity is fixed.</b> There is deliberately no error level here: "advisories never
    /// block" is a design law (docs/design/README.md), and amber is the only warning colour the app
    /// has. A genuine failure is not an advisory — it is a message box from the path that failed.
    /// </para>
    /// <para>
    /// Immutable, and not a <see cref="ViewModelBase"/>: an advisory is never edited, it is
    /// replaced. <see cref="EditorAdvisories"/> is rebuilt whole after every mutation, so nothing
    /// here would ever raise a change notification.
    /// </para>
    /// </summary>
    public sealed class AdvisoryViewModel
    {
        /// <summary>Where it belongs on screen, and what <c>Review</c> selects.</summary>
        public AdvisoryAnchor Anchor { get; }

        /// <summary>The whole sentence, as the anchor's own surface shows it.</summary>
        public string Message { get; }

        /// <summary>
        /// The same fact as a mid-sentence fragment ("tap-and-hold count is 11 of 10"), so the
        /// summary strip can name what it is summarising without re-deriving it. Never empty.
        /// </summary>
        public string Detail { get; }

        /// <summary>Always <see cref="StatusSeverity.Warning"/> — see the type's remarks.</summary>
        public StatusSeverity Severity => StatusSeverity.Warning;

        /// <summary>The section this is shown in.</summary>
        public EditorTab Tab => Anchor.Tab;

        /// <summary>The surface within that section that <c>Review</c> opens.</summary>
        public AdvisorySurface Surface => Anchor.Surface;

        /// <summary>The layer it sits on, or null when it is layout-wide.</summary>
        public int? LayerIndex => Anchor.LayerIndex;

        /// <summary>The key position it sits on, or null.</summary>
        public int? KeyIndex => Anchor.KeyIndex;

        /// <summary>The macro slot it sits in, or null.</summary>
        public int? MacroIndex => Anchor.MacroIndex;

        /// <summary>Creates one advisory. <paramref name="detail"/> defaults to the whole message.</summary>
        public AdvisoryViewModel(AdvisoryAnchor anchor, string message, string? detail = null)
        {
            Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Detail = string.IsNullOrEmpty(detail) ? message : detail;
        }
    }
}
