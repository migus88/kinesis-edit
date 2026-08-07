namespace KinesisEdit.Services
{
    /// <summary>
    /// <b>The one in-memory copy of this user's host preferences</b> — theme, motion and the shell
    /// window's geometry — loaded once, mutated in memory, written through to a per-user file.
    /// <para>
    /// It has the same shape as <see cref="IAppPreferencesStore"/> because it solves the same
    /// problem — more than one consumer (the Settings screen, the window's close handler, the two
    /// appliers) reading and writing one file — and a second reader shows the others stale state.
    /// <b>It is a different store over a different file, and the two must never be confused:</b>
    /// <see cref="IAppPreferencesStore"/> is <c>app_settings.txt</c> on a keyboard's v-Drive and is
    /// <i>per device</i>; this is a JSON file in the per-user configuration directory and is
    /// <i>per user, per machine</i>. Do not extend either one to carry the other's values.
    /// </para>
    /// <para>
    /// Every consumer reads <see cref="Current"/> and re-reads it when <see cref="Changed"/> fires.
    /// </para>
    /// </summary>
    public interface IHostPreferencesStore
    {
        /// <summary>
        /// The user's current preferences. Never null; an absent or unreadable file yields
        /// <see cref="HostPreferences.Default"/>.
        /// </summary>
        HostPreferences Current { get; }

        /// <summary>
        /// Raised after <see cref="Current"/> actually changed, so every other consumer can re-read
        /// it. <b>A mutation that returns an equal value raises nothing</b> — the Settings screen
        /// binds to this, and a no-op write that re-entered every binding would be a loop waiting
        /// to happen.
        /// </summary>
        event Action? Changed;

        /// <summary>
        /// Applies <paramref name="mutate"/> to <see cref="Current"/>, persists the result and
        /// raises <see cref="Changed"/>.
        /// <para>
        /// <b>It takes a function, not a value, on purpose:</b> two callers write different fields
        /// of the same record — the Settings screen writes the theme while the window's close
        /// handler writes the geometry — and a set-the-whole-record API lets the second one clobber
        /// the first with the copy it read a moment earlier. A mutation always sees the newest
        /// state.
        /// </para>
        /// <b>Never throws for I/O problems</b>: a preference that cannot be written is not worth
        /// taking the app down for. The in-memory value still moves, so the session stays
        /// self-consistent even when the disk refused.
        /// </summary>
        void Update(Func<HostPreferences, HostPreferences> mutate);
    }
}
