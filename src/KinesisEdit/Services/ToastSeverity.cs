namespace KinesisEdit.Services
{
    /// <summary>
    /// What a toast is reporting (docs/design/mockups.md, mockup 1k, which draws exactly two:
    /// "Saved" and "Saved with 3 advisories"). It selects the dot and the card's border from the
    /// four-status vocabulary; an advisory is amber and never red, and it never blocks — a toast
    /// dismisses itself either way.
    /// </summary>
    public enum ToastSeverity
    {
        /// <summary>Something finished and worked. The green dot.</summary>
        Success = 0,

        /// <summary>Something finished, with advisories worth reading. The amber dot.</summary>
        Advisory = 1
    }
}
