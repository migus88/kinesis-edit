using KinesisEdit.Core.Settings;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The no-drive store: every preference sits at its default, every notification is shown and
    /// nothing is ever persisted, because there is no <c>app_settings.txt</c> to persist it to
    /// (specs/08-settings.md §3, specs/03-vdrive-and-files.md §3.5). Also the fallback when no
    /// device session is active.
    /// </summary>
    public sealed class NullAppPreferencesStore : IAppPreferencesStore
    {
        /// <summary>Shared instance; the type is stateless.</summary>
        public static NullAppPreferencesStore Instance { get; } = new();

        /// <summary>Always <see cref="AppSettings.Empty"/> — every key absent, every default in force.</summary>
        public AppSettings Current => AppSettings.Empty;

        /// <summary>Always false; there is no file to write.</summary>
        public bool IsWritable => false;

        /// <summary>Never raised — <see cref="Current"/> cannot change.</summary>
        public event Action? Changed
        {
            add
            {
            }

            remove
            {
            }
        }

        /// <summary>Always false — nothing is hidden without a drive to have hidden it on.</summary>
        public bool IsHidden(string key)
        {
            return false;
        }

        /// <summary>Discards the preference; there is nowhere to write it.</summary>
        public void SetHidden(string key, bool hidden)
        {
        }

        /// <summary>Discards the change; there is nowhere to write it.</summary>
        public void Update(Func<AppSettings, AppSettings> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);
        }

        /// <summary>
        /// Discards the names; there is nowhere to write them. A macro renamed in this session
        /// keeps its name in memory (it lives on <c>Macro.Name</c>) and is simply not persisted —
        /// which is what "demo mode must not throw" means here.
        /// </summary>
        public void UpdateMacroNames(int profileNumber, Func<AppSettings, AppSettings> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);
        }
    }
}
