using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The theme preference → <see cref="ThemeVariant"/> mapping, and that applying it moves the
    /// running application. <see cref="ThemeVariant.Default"/> for "follow the system" is the load-
    /// bearing case: it is what <c>App.axaml</c> declares, so it is how the app goes *back* to
    /// following the OS after a forced light or dark.
    /// </summary>
    public class ThemeApplierTests
    {
        [AvaloniaFact]
        public void ToThemeVariant_MapsFollowSystem_ToDefault()
        {
            Assert.Equal(ThemeVariant.Default, ThemeApplier.ToThemeVariant(AppThemePreference.FollowSystem));
        }

        [AvaloniaFact]
        public void ToThemeVariant_MapsLight_ToLight()
        {
            Assert.Equal(ThemeVariant.Light, ThemeApplier.ToThemeVariant(AppThemePreference.Light));
        }

        [AvaloniaFact]
        public void ToThemeVariant_MapsDark_ToDark()
        {
            Assert.Equal(ThemeVariant.Dark, ThemeApplier.ToThemeVariant(AppThemePreference.Dark));
        }

        [AvaloniaFact]
        public void ToThemeVariant_MapsAnUnknownPreference_ToDefault()
        {
            // A value out of a corrupt file or a future version follows the OS, which is what the
            // app did before there was a choice.
            Assert.Equal(ThemeVariant.Default, ThemeApplier.ToThemeVariant((AppThemePreference)99));
        }

        [AvaloniaTheory]
        [InlineData(AppThemePreference.Light)]
        [InlineData(AppThemePreference.Dark)]
        [InlineData(AppThemePreference.FollowSystem)]
        public void Apply_PutsTheVariant_OnTheApplication(AppThemePreference preference)
        {
            var application = Application.Current!;
            var restore = application.RequestedThemeVariant;

            try
            {
                ThemeApplier.Apply(application, preference);

                Assert.Equal(ThemeApplier.ToThemeVariant(preference), application.RequestedThemeVariant);
            }
            finally
            {
                application.RequestedThemeVariant = restore;
            }
        }

        [AvaloniaFact]
        public void Apply_ReturnsToFollowingTheSystem_AfterAForcedVariant()
        {
            var application = Application.Current!;
            var restore = application.RequestedThemeVariant;

            try
            {
                ThemeApplier.Apply(application, AppThemePreference.Dark);
                ThemeApplier.Apply(application, AppThemePreference.FollowSystem);

                Assert.Equal(ThemeVariant.Default, application.RequestedThemeVariant);
            }
            finally
            {
                application.RequestedThemeVariant = restore;
            }
        }

        [AvaloniaFact]
        public void Apply_Rejects_ANullApplication()
        {
            Assert.Throws<ArgumentNullException>(() => ThemeApplier.Apply(null!, AppThemePreference.Dark));
        }
    }
}
