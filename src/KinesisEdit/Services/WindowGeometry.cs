namespace KinesisEdit.Services
{
    /// <summary>
    /// Where the shell window was and how big it was when the app last closed. A <b>host</b>
    /// preference — the same window serves every keyboard, so this can never live on a v-Drive.
    /// <para>
    /// <see cref="Width"/> and <see cref="Height"/> are DIPs, matching Avalonia's
    /// <c>Window.Width</c>/<c>Window.Height</c>; <see cref="X"/> and <see cref="Y"/> are screen
    /// pixels, matching <c>Window.Position</c>, and are optional because a window that has never
    /// been moved has no position worth restoring. Both may be <b>negative</b>: a second monitor
    /// placed left of or above the primary one has negative coordinates, and rejecting those would
    /// silently drag the window back to the main screen on every launch.
    /// </para>
    /// <para>
    /// <b>The size range is written down here and nowhere else</b> (settings.md invariant 9). A
    /// stored size outside it is not a size — it is a corrupt or hostile file — and
    /// <see cref="TryCreate"/> is the one gate that says so, which is why the tolerant JSON read
    /// asks it rather than range-checking on its own.
    /// </para>
    /// </summary>
    public sealed record WindowGeometry(double Width, double Height, int? X, int? Y, bool IsMaximized)
    {
        /// <summary>
        /// The smallest stored extent that is still a window. Below the shell's own 720×480 floor
        /// on purpose: this rejects garbage (0, -1, NaN), it does not re-state the minimum size,
        /// which is the window's business and would be a second place to change it.
        /// </summary>
        public const double MinimumExtent = 1;

        /// <summary>
        /// The largest stored extent that is still plausible. Far past any real display, so it
        /// only ever catches a corrupt number; a window sized from it would be unreachable.
        /// </summary>
        public const double MaximumExtent = 32000;

        /// <summary>
        /// The geometry for these values, or <c>null</c> when they are not a usable window.
        /// A non-finite or out-of-range <paramref name="width"/>/<paramref name="height"/> yields
        /// null — the caller then has *no stored geometry*, which is the same state as a fresh
        /// install, rather than a window it cannot show. Non-finite coordinates are dropped
        /// individually: a bad position is worth forgetting, not a whole geometry.
        /// </summary>
        public static WindowGeometry? TryCreate(double width, double height, int? x, int? y, bool isMaximized)
        {
            if (!IsUsableExtent(width) || !IsUsableExtent(height))
            {
                return null;
            }

            return new WindowGeometry(width, height, x, y, isMaximized);
        }

        /// <summary>True when <see cref="Width"/> and <see cref="Height"/> are both a real size.</summary>
        public bool IsUsable => IsUsableExtent(Width) && IsUsableExtent(Height);

        /// <summary>True when both coordinates are present, i.e. the window has a position to restore.</summary>
        public bool HasPosition => X.HasValue && Y.HasValue;

        private static bool IsUsableExtent(double extent)
        {
            return double.IsFinite(extent) && extent >= MinimumExtent && extent <= MaximumExtent;
        }
    }
}
