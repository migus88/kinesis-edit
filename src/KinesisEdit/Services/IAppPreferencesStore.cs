using KinesisEdit.Core.Settings;

namespace KinesisEdit.Services
{
    /// <summary>
    /// <b>The one in-memory <c>app_settings.txt</c> of a device session</b> — the notification hide
    /// flags, this app's five added preferences and the twelve <c>cust_color_N</c> picker slots
    /// (specs/08-settings.md §3), loaded once, mutated in memory, written through.
    /// <para>
    /// It exists because that file has more than one consumer: the colour picker writes swatches,
    /// the settings screen writes preferences, and message boxes write suppression answers. Two
    /// independent readers show each other stale state — a swatch stored in the picker would not
    /// appear in the settings screen until a reload. Hence one store per session and
    /// <see cref="Changed"/>: <b>every consumer reads <see cref="Current"/> and re-reads it when
    /// the event fires.</b> docs/app/settings.md's rule that <c>app_settings.txt</c> has exactly
    /// one reader and one writer is this interface.
    /// </para>
    /// <para>
    /// It extends <see cref="INotificationSuppressionStore"/> rather than replacing it:
    /// <see cref="NotificationService"/> needs only the narrow pair and must not learn about the
    /// rest.
    /// </para>
    /// </summary>
    public interface IAppPreferencesStore : INotificationSuppressionStore
    {
        /// <summary>The session's current preferences. Never null; an unreadable file yields <see cref="AppSettings.Empty"/>.</summary>
        AppSettings Current { get; }

        /// <summary>
        /// Whether <see cref="Update"/> reaches the drive. False in demo mode and with no drive at
        /// all — spec 08 §3 bans <b>saving</b> app settings in demo mode, not loading them, so a
        /// read-only store still serves real values from the file.
        /// </summary>
        bool IsWritable { get; }

        /// <summary>Raised after <see cref="Current"/> changed, so every other consumer can re-read it.</summary>
        event Action? Changed;

        /// <summary>
        /// Applies <paramref name="mutate"/> to <see cref="Current"/>, persists the result and
        /// raises <see cref="Changed"/>. A store that is not writable does nothing at all.
        /// <b>Never throws for I/O problems</b> — a preference that cannot be written must not
        /// break a dialog or a screen — but the in-memory value still moves, so the session stays
        /// self-consistent.
        /// </summary>
        void Update(Func<AppSettings, AppSettings> mutate);
    }
}
