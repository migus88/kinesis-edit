using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The shell's Settings screen: the host preferences, the two segmented switches over them, and
    /// the two halves of every pick — it is persisted through the store <b>and</b> applied to the
    /// running app.
    /// </summary>
    public class SettingsScreenViewModelTests
    {
        [Fact]
        public void ThemeOptions_Always_OfferTheThreePreferencesInTheEnumsOrder()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(
                new[] { AppThemePreference.FollowSystem, AppThemePreference.Light, AppThemePreference.Dark },
                viewModel.ThemeOptions.Select(option => option.Value));
        }

        [Fact]
        public void MotionOptions_Always_OfferTheThreePreferencesInTheEnumsOrder()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(
                new[] { MotionPreference.FollowSystem, MotionPreference.AlwaysReduce, MotionPreference.NeverReduce },
                viewModel.MotionOptions.Select(option => option.Value));
        }

        [Fact]
        public void Options_Always_CoverEveryMemberOfBothEnums()
        {
            // The order above is hand-written; the membership is not. A preference added to either
            // enum has to reach its switch even before anybody thinks to place it.
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(
                Enum.GetValues<AppThemePreference>().Order(),
                viewModel.ThemeOptions.Select(option => option.Value).Order());
            Assert.Equal(
                Enum.GetValues<MotionPreference>().Order(),
                viewModel.MotionOptions.Select(option => option.Value).Order());
        }

        [Fact]
        public void EveryOption_Always_CarriesACaption()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.All(viewModel.ThemeOptions, option => Assert.False(string.IsNullOrWhiteSpace(option.Caption)));
            Assert.All(viewModel.MotionOptions, option => Assert.False(string.IsNullOrWhiteSpace(option.Caption)));
        }

        [Fact]
        public void Selection_OnConstruction_ReadsWhateverTheStoreAlreadyHolds()
        {
            var store = new FakeHostPreferencesStore(new HostPreferences
            {
                Theme = AppThemePreference.Light,
                Motion = MotionPreference.NeverReduce
            });

            using var viewModel = new SettingsScreenViewModel(store, _ => { }, _ => { });

            Assert.Equal(AppThemePreference.Light, viewModel.SelectedThemeOption.Value);
            Assert.Equal(MotionPreference.NeverReduce, viewModel.SelectedMotionOption.Value);
        }

        [Fact]
        public void Construction_Always_AppliesNothing()
        {
            // The composition root already put the stored preferences on the application before this
            // screen existed. Re-applying them on every visit would repaint the window for nothing —
            // and would hide a screen that never applies anything at all.
            var applied = new List<AppThemePreference>();
            var moved = new List<MotionPreference>();
            var store = new FakeHostPreferencesStore(new HostPreferences { Theme = AppThemePreference.Dark });

            using var viewModel = new SettingsScreenViewModel(store, applied.Add, moved.Add);

            Assert.Empty(applied);
            Assert.Empty(moved);
        }

        [Fact]
        public void SelectTheme_WithANewTheme_PersistsItAndAppliesItLive()
        {
            var viewModel = CreateViewModel(out var store, out var themes, out _);

            viewModel.SelectThemeCommand.Execute(FindTheme(viewModel, AppThemePreference.Dark));

            Assert.Equal(AppThemePreference.Dark, store.Current.Theme);
            Assert.Equal([AppThemePreference.Dark], themes);
            Assert.Equal(AppThemePreference.Dark, viewModel.SelectedThemeOption.Value);
        }

        [Fact]
        public void SelectMotion_WithANewPreference_PersistsItAndAppliesItLive()
        {
            var viewModel = CreateViewModel(out var store, out _, out var motions);

            viewModel.SelectMotionCommand.Execute(FindMotion(viewModel, MotionPreference.AlwaysReduce));

            Assert.Equal(MotionPreference.AlwaysReduce, store.Current.Motion);
            Assert.Equal([MotionPreference.AlwaysReduce], motions);
            Assert.Equal(MotionPreference.AlwaysReduce, viewModel.SelectedMotionOption.Value);
        }

        [Fact]
        public void SelectTheme_Always_LeavesTheOtherPreferencesAlone()
        {
            // Update takes a function precisely so two writers cannot clobber each other; a screen
            // that rebuilt the whole record would undo the window geometry the shell wrote on close.
            var geometry = WindowGeometry.TryCreate(900, 640, 12, 34, isMaximized: false);
            var store = new FakeHostPreferencesStore(new HostPreferences
            {
                Motion = MotionPreference.NeverReduce,
                Window = geometry
            });

            using var viewModel = new SettingsScreenViewModel(store, _ => { }, _ => { });

            viewModel.SelectThemeCommand.Execute(FindTheme(viewModel, AppThemePreference.Light));

            Assert.Equal(MotionPreference.NeverReduce, store.Current.Motion);
            Assert.Equal(geometry, store.Current.Window);
        }

        [Fact]
        public void SelectTheme_WithTheThemeAlreadyChosen_WritesNothingAndAppliesNothing()
        {
            // An equal record is a no-op in the store, and re-applying a theme nobody changed would
            // repaint the app every time a segment is re-clicked.
            var viewModel = CreateViewModel(out var store, out var themes, out _);

            viewModel.SelectThemeCommand.Execute(FindTheme(viewModel, AppThemePreference.FollowSystem));

            Assert.Equal(0, store.UpdateCount);
            Assert.Empty(themes);
        }

        [Fact]
        public void SelectTheme_Always_LeavesTheMotionApplierUntouched()
        {
            // Both appliers hang off one Changed event, so the guard that keeps them apart is the
            // comparison against what was last applied — not the call site.
            var viewModel = CreateViewModel(out _, out _, out var motions);

            viewModel.SelectThemeCommand.Execute(FindTheme(viewModel, AppThemePreference.Light));

            Assert.Empty(motions);
        }

        [Fact]
        public void SelectTheme_WithNull_DoesNothing()
        {
            // A ListBox in single-selection mode clears its selection on a Ctrl/Cmd-click, and the
            // handler forwards whatever it holds.
            var viewModel = CreateViewModel(out var store, out var themes, out _);

            viewModel.SelectThemeCommand.Execute(null);

            Assert.Equal(0, store.UpdateCount);
            Assert.Empty(themes);
            Assert.Equal(AppThemePreference.FollowSystem, viewModel.SelectedThemeOption.Value);
        }

        [Fact]
        public void SelectMotion_WithNull_DoesNothing()
        {
            var viewModel = CreateViewModel(out var store, out _, out var motions);

            viewModel.SelectMotionCommand.Execute(null);

            Assert.Equal(0, store.UpdateCount);
            Assert.Empty(motions);
        }

        [Fact]
        public void AStoreChangedElsewhere_Always_MovesTheSelectionAndApplies()
        {
            // The store is the truth and the screen binds to Changed, so a preference changed by
            // anything else lands on the switch and on the running app.
            var viewModel = CreateViewModel(out var store, out var themes, out var motions);

            store.Update(current => current with
            {
                Theme = AppThemePreference.Light,
                Motion = MotionPreference.AlwaysReduce
            });

            Assert.Equal(AppThemePreference.Light, viewModel.SelectedThemeOption.Value);
            Assert.Equal(MotionPreference.AlwaysReduce, viewModel.SelectedMotionOption.Value);
            Assert.Equal([AppThemePreference.Light], themes);
            Assert.Equal([MotionPreference.AlwaysReduce], motions);
        }

        [Fact]
        public void AGeometryOnlyWrite_Always_RepaintsNothing()
        {
            // The shell writes the window's size and position into this very record on close. That
            // must not repaint the app or re-run the motion binder on the way out.
            var viewModel = CreateViewModel(out var store, out var themes, out var motions);

            store.Update(current => current with
            {
                Window = WindowGeometry.TryCreate(1000, 680, 0, 0, isMaximized: false)
            });

            Assert.Empty(themes);
            Assert.Empty(motions);
        }

        [Fact]
        public void Dispose_Always_StopsListeningToTheStore()
        {
            // The store outlives the screen; a screen left subscribed would keep applying after the
            // user navigated away from it.
            var viewModel = CreateViewModel(out var store, out var themes, out _);

            viewModel.Dispose();

            store.Update(current => current with { Theme = AppThemePreference.Dark });

            Assert.Empty(themes);
            Assert.Equal(AppThemePreference.FollowSystem, viewModel.SelectedThemeOption.Value);
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.Dispose();
            viewModel.Dispose();
        }

        [Fact]
        public void Constructor_WithAMissingCollaborator_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new SettingsScreenViewModel(null!, _ => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(
                () => new SettingsScreenViewModel(new FakeHostPreferencesStore(), null!, _ => { }));
            Assert.Throws<ArgumentNullException>(
                () => new SettingsScreenViewModel(new FakeHostPreferencesStore(), _ => { }, null!));
        }

        [Fact]
        public void TheCopy_Always_SaysTheseArePerUserRatherThanPerDevice()
        {
            // The one thing a reader arriving from the editor's Settings tab has to be told: that
            // tab writes to the board, this screen does not.
            Assert.Contains("this computer", SettingsScreenViewModel.Subtitle, StringComparison.Ordinal);
            Assert.Contains("Settings tab", SettingsScreenViewModel.Subtitle, StringComparison.Ordinal);
        }

        [Fact]
        public void TheWindowNote_Always_SaysThereIsNothingToSwitchOn()
        {
            // The geometry is restored silently and renders no control, so the copy is the only
            // place the behaviour is stated at all.
            Assert.Contains("nothing to switch on", SettingsScreenViewModel.WindowNote, StringComparison.Ordinal);
        }

        private static SettingsScreenViewModel CreateViewModel(
            out FakeHostPreferencesStore store,
            out List<AppThemePreference> appliedThemes,
            out List<MotionPreference> appliedMotions)
        {
            var themes = new List<AppThemePreference>();
            var motions = new List<MotionPreference>();

            store = new FakeHostPreferencesStore();
            appliedThemes = themes;
            appliedMotions = motions;

            return new SettingsScreenViewModel(store, themes.Add, motions.Add);
        }

        private static AppThemeOption FindTheme(SettingsScreenViewModel viewModel, AppThemePreference preference)
        {
            return viewModel.ThemeOptions.Single(option => option.Value == preference);
        }

        private static MotionOption FindMotion(SettingsScreenViewModel viewModel, MotionPreference preference)
        {
            return viewModel.MotionOptions.Single(option => option.Value == preference);
        }
    }
}
