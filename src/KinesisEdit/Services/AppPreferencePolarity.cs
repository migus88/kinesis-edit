namespace KinesisEdit.Services
{
    /// <summary>
    /// What the stored value <c>on</c> means for a preference in <c>app_settings.txt</c>. The two
    /// members are <b>opposites</b>, and conflating them inverts a checkbox: this enum exists so
    /// the difference is a compile-time fact rather than a comment.
    /// </summary>
    public enum AppPreferencePolarity
    {
        /// <summary>
        /// A <c>*_msg</c> hide flag of specs/08-settings.md §3: <c>on</c> = <b>hide</b> the prompt,
        /// absent = <c>off</c> = show it. The user-facing option is therefore the <i>negation</i>
        /// of what is stored, and it defaults to enabled ("ask me").
        /// </summary>
        Suppression = 0,

        /// <summary>
        /// A display preference, of which <c>advisory_detail</c> is the only one today: <c>on</c> =
        /// the option is <b>enabled</b>, absent = <c>off</c> = disabled. Stored value and
        /// user-facing option agree, and it defaults to disabled.
        /// </summary>
        Display = 1
    }
}
