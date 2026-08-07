using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Settings tab's "App &amp; notifications" section (docs/design/mockups.md §1j).
    /// <para>
    /// Everything runs over a <b>real</b> <see cref="VDriveAppPreferencesStore"/> on a temp
    /// directory, through Core's own parser and serializer. A fake store would prove the view model
    /// calls something; only the real one proves which line lands in <c>app_settings.txt</c> — and
    /// that is the whole risk on this screen, because the file carries <b>two opposite
    /// polarities</b>: <c>on</c> hides a <c>*_msg</c> prompt, and <c>on</c> expands
    /// <c>advisory_detail</c>. A section that applied one rule to both would still tick and untick
    /// perfectly.
    /// </para>
    /// </summary>
    public sealed class AppPreferencesViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _filePath;
        private readonly VDriveLocation _location;
        private readonly VDriveFileService _fileService = new();
        private readonly ISettingsService _settings;

        public AppPreferencesViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);

            _location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb) with { RootPath = _tempDirectory };
            _filePath = VDriveAppPreferencesStore.GetFilePath(_location);
            _settings = TestDevices.CreateSettingsService(_fileService);

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        }

        [Fact]
        public void Featured_IsMockup1jsFivePreferencesInItsOrder()
        {
            var section = CreateSection();

            Assert.Equal(
                new[]
                {
                    SettingsKeys.UnsavedChangesMessage,
                    SettingsKeys.ResetLayerMessage,
                    SettingsKeys.CaptureSummaryMessage,
                    SettingsKeys.SwitchVariantMessage,
                    SettingsKeys.AdvisoryDetail
                },
                section.Featured.Select(row => row.Key));

            // The order and the wording come from the catalog, never from this screen.
            Assert.Equal(
                AppPreferenceCatalog.Featured.Select(descriptor => descriptor.Caption),
                section.Featured.Select(row => row.Caption));
        }

        [Fact]
        public void All_CoversEveryPreferenceTheCatalogDeclares()
        {
            var section = CreateSection();

            Assert.Equal(AppPreferenceCatalog.All.Count, section.All.Count);
            Assert.Equal(
                AppPreferenceCatalog.All.Select(descriptor => descriptor.Key),
                section.All.Select(row => row.Key));
        }

        [Fact]
        public void DisclosureCaption_CountsTheCatalogRatherThanRepeatingTheMockupsNumber()
        {
            // Mockup 1j writes "+7 more", which assumed the twelve legacy keys. There are
            // seventeen preferences, so the literal would have shipped wrong.
            var section = CreateSection();

            Assert.Equal(AppPreferenceCatalog.Additional.Count, section.Additional.Count);
            Assert.Equal(
                AppPreferencesViewModel.FormatMoreCaption(AppPreferenceCatalog.Additional.Count),
                section.DisclosureCaption);
            Assert.DoesNotContain("7", section.DisclosureCaption, StringComparison.Ordinal);

            section.ToggleAdditionalCommand.Execute(null);

            Assert.True(section.AreAdditionalShown);
            Assert.Equal(AppPreferencesViewModel.ShowFewerCaption, section.DisclosureCaption);
        }

        [Fact]
        public async Task LoadAsync_WithAnEmptyFile_RestsEveryPreferenceAtItsPolaritysDefault()
        {
            var section = CreateSection();

            await section.LoadAsync();

            // Absent always means `off` on disk, and what `off` means is the polarity's business:
            // "ask me" for a suppression flag, "collapsed" for the display preference.
            Assert.True(Row(section, SettingsKeys.UnsavedChangesMessage).IsChecked);
            Assert.True(Row(section, SettingsKeys.CaptureSummaryMessage).IsChecked);
            Assert.False(Row(section, SettingsKeys.AdvisoryDetail).IsChecked);
        }

        [Fact]
        public async Task LoadAsync_ForASuppressionPreference_ShowsTheTickAsTheAbsenceOfTheFlag()
        {
            // capture_summary_msg=on means "hide the summary", so the option reads unticked. This
            // is exactly the row mockup 1j draws unticked, and it is that board's stored answer
            // rather than the default.
            CreateFile(SettingsKeys.CaptureSummaryMessage + "=on");

            var section = CreateSection();

            await section.LoadAsync();

            Assert.False(Row(section, SettingsKeys.CaptureSummaryMessage).IsChecked);
            Assert.True(Row(section, SettingsKeys.UnsavedChangesMessage).IsChecked);
        }

        [Fact]
        public async Task LoadAsync_ForTheDisplayPreference_ShowsTheTickAsTheFlagItself()
        {
            // The half that breaks if one rule is applied to both: advisory_detail=on is the
            // option *enabled*, not suppressed. A section that inverted it would show this
            // unticked, and the suppression case above would still pass.
            CreateFile(SettingsKeys.AdvisoryDetail + "=on");

            var section = CreateSection();

            await section.LoadAsync();

            Assert.True(Row(section, SettingsKeys.AdvisoryDetail).IsChecked);
        }

        [Fact]
        public async Task Unticking_ASuppressionPreference_WritesTheHideFlagOn()
        {
            var section = CreateSection();

            await section.LoadAsync();

            Row(section, SettingsKeys.ResetLayerMessage).IsChecked = false;

            Assert.Contains(
                SettingsKeys.ResetLayerMessage + "=on",
                File.ReadAllLines(_filePath),
                StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Ticking_TheDisplayPreference_WritesItOnWithoutInvertingIt()
        {
            var section = CreateSection();

            await section.LoadAsync();

            Row(section, SettingsKeys.AdvisoryDetail).IsChecked = true;

            var lines = File.ReadAllLines(_filePath);

            Assert.Contains(SettingsKeys.AdvisoryDetail + "=on", lines, StringComparer.OrdinalIgnoreCase);

            // And the other way round, which is where an inverted implementation lands.
            Assert.DoesNotContain(SettingsKeys.AdvisoryDetail + "=off", lines, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TickingASuppressionPreferenceBack_WritesTheFlagOffRatherThanRemovingIt()
        {
            // The answer has to be durable: "yes, keep asking" must survive as a written `off`,
            // not as the absence that merely happens to mean the same thing today.
            CreateFile(SettingsKeys.ResetLayerMessage + "=on");

            var section = CreateSection();

            await section.LoadAsync();

            Assert.False(Row(section, SettingsKeys.ResetLayerMessage).IsChecked);

            Row(section, SettingsKeys.ResetLayerMessage).IsChecked = true;

            Assert.Contains(
                SettingsKeys.ResetLayerMessage + "=off",
                File.ReadAllLines(_filePath),
                StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Preferences_SurviveARestart_WithBothPolaritiesIntact()
        {
            // The acceptance criterion, run end to end: write through one session, then read the
            // file back through a brand new store, exactly as the next launch would.
            var first = CreateSection();

            await first.LoadAsync();

            Row(first, SettingsKeys.UnsavedChangesMessage).IsChecked = false;
            Row(first, SettingsKeys.AdvisoryDetail).IsChecked = true;

            var second = CreateSection();

            await second.LoadAsync();

            Assert.False(Row(second, SettingsKeys.UnsavedChangesMessage).IsChecked);
            Assert.True(Row(second, SettingsKeys.AdvisoryDetail).IsChecked);
            Assert.True(Row(second, SettingsKeys.SwitchVariantMessage).IsChecked);
        }

        [Fact]
        public async Task Writing_OnePreference_LeavesTheOthersAndTheForeignLinesAlone()
        {
            CreateFile("cust_color_1=[255][0][128]", "some_unmanaged_line=whatever");

            var section = CreateSection();

            await section.LoadAsync();

            Row(section, SettingsKeys.SaveMessage).IsChecked = false;

            var lines = File.ReadAllLines(_filePath);

            Assert.Contains("cust_color_1=[255][0][128]", lines, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("some_unmanaged_line=whatever", lines, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ASecondConsumersWrite_ReachesTheSectionWithNoReload()
        {
            // One store per session, and every consumer re-reads it on Changed: a "don't ask this
            // again" answered in a message box has to show up here immediately.
            var store = CreateStore();
            var section = new AppPreferencesViewModel(store);

            await section.LoadAsync();

            Assert.True(Row(section, SettingsKeys.SaveMessage).IsChecked);

            store.SetHidden(SettingsKeys.SaveMessage, hidden: true);

            Assert.False(Row(section, SettingsKeys.SaveMessage).IsChecked);
        }

        [Fact]
        public async Task InDemoMode_TheBoxesStillMoveAndNothingIsWritten()
        {
            // Spec 08 §3 bans saving app settings in demo mode, not loading them — so the stored
            // answers are shown, the boxes are explorable, and the caveat is what says the
            // difference. The file is not touched at all.
            CreateFile(SettingsKeys.CaptureSummaryMessage + "=on");

            var section = new AppPreferencesViewModel(new ReadOnlyAppPreferencesStore(CreateStore()));

            await section.LoadAsync();

            Assert.True(section.IsReadOnly);
            Assert.False(Row(section, SettingsKeys.CaptureSummaryMessage).IsChecked);

            Row(section, SettingsKeys.CaptureSummaryMessage).IsChecked = true;

            Assert.True(Row(section, SettingsKeys.CaptureSummaryMessage).IsChecked);
            Assert.Equal(
                new[] { SettingsKeys.CaptureSummaryMessage + "=on" },
                File.ReadAllLines(_filePath));
        }

        [Fact]
        public void DemoModeCaveat_IsCoresWordingVerbatim()
        {
            Assert.Equal(
                SettingsMessageCatalog.DemoModePreferencesCaveat,
                CreateSection().DemoModeCaveat);
        }

        [Fact]
        public void WithADriveThatCanBeWritten_TheSectionIsNotReadOnly()
        {
            Assert.False(CreateSection().IsReadOnly);
            Assert.True(new AppPreferencesViewModel(NullAppPreferencesStore.Instance).IsReadOnly);
        }

        [Fact]
        public async Task LoadAsync_CalledTwice_ReadsOnce()
        {
            var section = CreateSection();

            await section.LoadAsync();
            await section.LoadAsync();

            Assert.True(Row(section, SettingsKeys.UnsavedChangesMessage).IsChecked);
        }

        [Fact]
        public void Constructor_WithoutAStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AppPreferencesViewModel(null!));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private static AppPreferenceRowViewModel Row(AppPreferencesViewModel section, string key)
        {
            return section.All.Single(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        private AppPreferencesViewModel CreateSection()
        {
            return new AppPreferencesViewModel(CreateStore());
        }

        private VDriveAppPreferencesStore CreateStore()
        {
            return new VDriveAppPreferencesStore(_settings, _location);
        }

        private void CreateFile(params string[] lines)
        {
            File.WriteAllLines(_filePath, lines);
        }
    }
}
