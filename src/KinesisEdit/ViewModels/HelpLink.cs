namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the Help screen: a caption, an optional line of explanation, and the page it
    /// opens.
    /// <para>
    /// A plain record, not a <see cref="ViewModelBase"/> — it never changes, and the shell's
    /// <c>ViewLocator</c> must not try to resolve a view for it. It carries <b>no command</b>
    /// either: opening a link is <see cref="HelpScreenViewModel.OpenLinkCommand"/>'s job, so the
    /// screen keeps the one <c>IUrlLauncher</c> and a row stays data.
    /// </para>
    /// </summary>
    public sealed record HelpLink
    {
        /// <summary>What the link reads as. Drawn beside the external-link mark, never with an arrow character.</summary>
        public required string Caption { get; init; }

        /// <summary>The page it opens. Every row has a real one — a link with nothing behind it is not rendered.</summary>
        public required string Url { get; init; }

        /// <summary>One line saying what is on the other end, or null where the caption is the whole story.</summary>
        public string? Description { get; init; }

        /// <summary>Whether <see cref="Description"/> is worth a line of its own.</summary>
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    }
}
