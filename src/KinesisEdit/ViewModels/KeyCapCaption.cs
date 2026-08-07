namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The four captions that do not fit a 1U key cap, and what the cap prints instead.
    /// <para>
    /// This is a <b>cap-only</b> rule and deliberately not a change to
    /// <see cref="KinesisEdit.Core.Keys.KeyRegistry"/>: the words in the key table are
    /// specs/05-key-model.md §3.7's own, they are what the key is *called*, and every surface with
    /// room for them still prints them — the key inspector's prose, its mono token field, the
    /// macro step rows, the co-trigger captions and the macro library's header all resolve through
    /// <see cref="KeyCaption"/> and are untouched. Only the legend on the cap is abbreviated, and
    /// only because the cap is 28 px wide.
    /// </para>
    /// <para>
    /// The measurement behind the table: at the keycap type step (9/400) roughly five characters
    /// fit a 1U cap. <c>Views/KeyCapView.axaml</c> records that <c>Pause</c> — five — measured
    /// exactly the old 24 px box and wrapped to <c>Paus</c> before the negative-margin fix widened
    /// it to 28. Dropping to the stacked 8 px step does not rescue these: <c>Forward</c> needs
    /// about 35 px even there. Confirmed on a rendered frame in both variants, which is the only
    /// thing that could have shown it — every assertion about the cap was already passing while it
    /// drew <c>Forwa</c>.
    /// </para>
    /// <para>
    /// The <see cref="KinesisEdit.ViewModels.KeyInspectorViewModel.CapCaption"/> tile follows the
    /// cap rather than the prose, because that tile is a picture of the cap.
    /// </para>
    /// </summary>
    public static class KeyCapCaption
    {
        /// <summary>
        /// Long caption → what the cap prints. Keyed on the caption rather than on the key code
        /// because two codes resolve to <c>Play\nPause</c> and the rule is about the string's
        /// width, not about the key's identity.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> _tooWideForACap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Forward"] = "Fwd",
                ["Rewind"] = "Rew",
                ["Record"] = "Rec",
                ["Play\nPause"] = "Play"
            };

        /// <summary>Every caption this rule shortens, for the test that keeps the table honest.</summary>
        public static IReadOnlyCollection<string> Shortened => (IReadOnlyCollection<string>)_tooWideForACap.Keys;

        /// <summary>
        /// <paramref name="caption"/> as the cap should print it: the abbreviation when the word
        /// is one of the four that overflow, otherwise the caption untouched.
        /// </summary>
        public static string For(string caption)
        {
            ArgumentNullException.ThrowIfNull(caption);

            return _tooWideForACap.TryGetValue(caption, out var shortened) ? shortened : caption;
        }
    }
}
