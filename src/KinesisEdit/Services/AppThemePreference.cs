namespace KinesisEdit.Services
{
    /// <summary>
    /// Which light/dark face the app wears. A <b>host</b> preference: it belongs to the person
    /// using this machine, not to any keyboard, so it lives in <see cref="HostPreferences"/> and
    /// never in a v-Drive's <c>app_settings.txt</c>.
    /// <para>
    /// <see cref="FollowSystem"/> is what the app did before there was a choice — <c>App.axaml</c>
    /// declares <c>RequestedThemeVariant="Default"</c> and the OS variant wins — so it is the
    /// default and the zero value. <see cref="ThemeApplier"/> is what turns one of these into
    /// Avalonia's <c>ThemeVariant</c>.
    /// </para>
    /// </summary>
    public enum AppThemePreference
    {
        /// <summary>Wear whatever the operating system is wearing. The default.</summary>
        FollowSystem = 0,

        /// <summary>Always light, whatever the OS says.</summary>
        Light = 1,

        /// <summary>Always dark, whatever the OS says.</summary>
        Dark = 2,
    }
}
