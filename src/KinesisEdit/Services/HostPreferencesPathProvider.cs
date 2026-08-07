namespace KinesisEdit.Services
{
    /// <summary>
    /// The per-user configuration location of each platform, as
    /// <c>&lt;root&gt;/KinesisEdit/preferences.json</c>.
    /// <para>
    /// The roots are written out rather than taken from
    /// <see cref="Environment.SpecialFolder.ApplicationData"/> everywhere, because .NET maps that
    /// folder to <c>~/.config</c> on macOS — correct for Linux, wrong for the app's primary
    /// platform, where a user-visible preferences file belongs in
    /// <c>~/Library/Application Support</c>. Linux honours <c>XDG_CONFIG_HOME</c> first, as the
    /// base-directory spec requires; Windows uses <c>%APPDATA%</c>.
    /// </para>
    /// <para>
    /// The root is a constructor parameter so this class itself is testable, and so nothing has to
    /// mock a filesystem to check the layout under it.
    /// <see cref="CreateForCurrentPlatform"/> is the only thing in the app that names the real
    /// location.
    /// </para>
    /// </summary>
    public sealed class HostPreferencesPathProvider : IHostPreferencesPathProvider
    {
        /// <summary>The folder this app owns inside the platform's configuration root.</summary>
        public const string DirectoryName = "KinesisEdit";

        /// <summary>The preferences file's name inside <see cref="DirectoryName"/>.</summary>
        public const string FileName = "preferences.json";

        /// <summary>The Linux/XDG override for the configuration root.</summary>
        private const string XdgConfigHomeVariable = "XDG_CONFIG_HOME";

        /// <summary>
        /// A provider over the real per-user configuration directory of the running platform.
        /// <b>The one place in the app that resolves it</b>; the composition root calls this and
        /// nothing else does — a test that wants a store builds its own provider over a temporary
        /// root.
        /// </summary>
        public static IHostPreferencesPathProvider CreateForCurrentPlatform()
        {
            return new HostPreferencesPathProvider(ResolveConfigurationRoot());
        }

        private readonly string _configurationRoot;

        /// <summary>Creates a provider whose files live under <paramref name="configurationRoot"/>.</summary>
        public HostPreferencesPathProvider(string configurationRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationRoot);

            _configurationRoot = configurationRoot;
        }

        /// <inheritdoc />
        public string GetFilePath()
        {
            return Path.Combine(_configurationRoot, DirectoryName, FileName);
        }

        private static string ResolveConfigurationRoot()
        {
            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(GetHomeDirectory(), "Library", "Application Support");
            }

            if (OperatingSystem.IsWindows())
            {
                var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                return string.IsNullOrWhiteSpace(applicationData) ? AppContext.BaseDirectory : applicationData;
            }

            var xdgConfigHome = Environment.GetEnvironmentVariable(XdgConfigHomeVariable);

            if (!string.IsNullOrWhiteSpace(xdgConfigHome))
            {
                return xdgConfigHome;
            }

            return Path.Combine(GetHomeDirectory(), ".config");
        }

        private static string GetHomeDirectory()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // With no home directory — a stripped container, a service account — a relative path
            // would land wherever the process happens to be running from, which moves between
            // launches. Next to the executable is at least stable, and mirrors the fallback
            // specs/03-vdrive-and-files.md §6 describes for the legacy app's settings file.
            return string.IsNullOrWhiteSpace(home) ? AppContext.BaseDirectory : home;
        }
    }
}
