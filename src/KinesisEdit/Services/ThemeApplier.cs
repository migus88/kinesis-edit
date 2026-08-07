using Avalonia;
using Avalonia.Styling;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Turns an <see cref="AppThemePreference"/> into Avalonia's <see cref="ThemeVariant"/> and
    /// puts it on the <see cref="Application"/>.
    /// <para>
    /// <c>App.axaml</c> declares <c>RequestedThemeVariant="Default"</c>, which is the OS-follows
    /// behaviour the app shipped with, so <see cref="AppThemePreference.FollowSystem"/> maps to
    /// <see cref="ThemeVariant.Default"/> and setting it is how the app goes *back* to following
    /// the OS after a forced light or dark.
    /// </para>
    /// <para>
    /// Every colour in the app is a role token resolved with <c>DynamicResource</c> and the theme
    /// dictionaries are declared for both variants (design-system.md), so assigning this at runtime
    /// re-resolves the whole palette live; nothing has to be rebuilt or reopened.
    /// </para>
    /// <para>
    /// A bridging service in the sense of app-shell.md invariant 8, and the same shape as
    /// <see cref="MotionResourceBinder"/> beside it: the mapping — <see cref="ToThemeVariant"/> — is
    /// separable and testable, and only <see cref="Apply"/> touches application state.
    /// </para>
    /// </summary>
    public static class ThemeApplier
    {
        /// <summary>
        /// The variant <paramref name="preference"/> asks for. An unrecognised preference follows
        /// the system, which is what the app did before there was a choice.
        /// </summary>
        public static ThemeVariant ToThemeVariant(AppThemePreference preference)
        {
            return preference switch
            {
                AppThemePreference.Light => ThemeVariant.Light,
                AppThemePreference.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }

        /// <summary>
        /// Puts <paramref name="preference"/>'s variant on <paramref name="application"/>. Safe to
        /// call repeatedly and safe to call live.
        /// </summary>
        public static void Apply(Application application, AppThemePreference preference)
        {
            ArgumentNullException.ThrowIfNull(application);

            application.RequestedThemeVariant = ToThemeVariant(preference);
        }
    }
}
