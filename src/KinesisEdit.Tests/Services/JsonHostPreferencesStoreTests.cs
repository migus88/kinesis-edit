using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The file-backed host store: that it round-trips, that it stays quiet when nothing changed,
    /// and that neither a read nor a write failure can reach the caller. Every store here is built
    /// through <see cref="TemporaryHostPreferences"/>, which refuses to point at the real per-user
    /// configuration directory.
    /// </summary>
    public class JsonHostPreferencesStoreTests
    {
        [Fact]
        public void Current_IsDefault_WhenNoFileExists()
        {
            using var temporary = new TemporaryHostPreferences();

            Assert.Equal(HostPreferences.Default, temporary.CreateStore().Current);
        }

        [Fact]
        public void Update_RoundTripsEveryField_ThroughTheFile()
        {
            using var temporary = new TemporaryHostPreferences();
            var geometry = new WindowGeometry(1234.5, 678.25, -900, 42, true);

            temporary.CreateStore().Update(preferences => preferences with
            {
                Theme = AppThemePreference.Dark,
                Motion = MotionPreference.AlwaysReduce,
                Window = geometry
            });

            // A second store over the same file is the only honest read-back: it shares no state
            // with the one that wrote.
            var reloaded = temporary.CreateStore().Current;

            Assert.Equal(AppThemePreference.Dark, reloaded.Theme);
            Assert.Equal(MotionPreference.AlwaysReduce, reloaded.Motion);
            Assert.Equal(geometry, reloaded.Window);
        }

        [Fact]
        public void Update_CreatesTheDirectory_WhenItDoesNotExist()
        {
            using var temporary = new TemporaryHostPreferences();

            Assert.False(Directory.Exists(temporary.Root));

            temporary.CreateStore().Update(preferences => preferences with { Theme = AppThemePreference.Light });

            Assert.True(File.Exists(temporary.FilePath));
        }

        [Fact]
        public void Update_MovesTheInMemoryValue_Immediately()
        {
            using var temporary = new TemporaryHostPreferences();
            var store = temporary.CreateStore();

            store.Update(preferences => preferences with { Theme = AppThemePreference.Light });

            Assert.Equal(AppThemePreference.Light, store.Current.Theme);
        }

        [Fact]
        public void Update_SeesTheNewestState_SoTwoWritersDoNotClobberEachOther()
        {
            // This is why Update takes a function: the Settings screen writes the theme while the
            // window's close handler writes the geometry, and neither may undo the other.
            using var temporary = new TemporaryHostPreferences();
            var store = temporary.CreateStore();

            store.Update(preferences => preferences with { Theme = AppThemePreference.Dark });
            store.Update(preferences => preferences with { Window = new WindowGeometry(900, 600, 5, 6, false) });

            Assert.Equal(AppThemePreference.Dark, store.Current.Theme);
            Assert.NotNull(store.Current.Window);
        }

        [Fact]
        public void Update_RaisesChanged_WhenSomethingMoved()
        {
            using var temporary = new TemporaryHostPreferences();
            var store = temporary.CreateStore();
            var raised = 0;

            store.Changed += () => raised++;
            store.Update(preferences => preferences with { Motion = MotionPreference.NeverReduce });

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Update_RaisesNothing_ForANoOpWrite()
        {
            // The Settings screen binds to Changed; a write that changed nothing re-entering every
            // binding is a loop waiting to happen.
            using var temporary = new TemporaryHostPreferences();
            var store = temporary.CreateStore();
            var raised = 0;

            store.Changed += () => raised++;
            store.Update(preferences => preferences);
            store.Update(preferences => preferences with { Theme = store.Current.Theme });

            Assert.Equal(0, raised);
        }

        [Fact]
        public void Update_WritesNothing_ForANoOpWrite()
        {
            using var temporary = new TemporaryHostPreferences();
            var store = temporary.CreateStore();

            store.Update(preferences => preferences);

            Assert.False(File.Exists(temporary.FilePath));
        }

        [Fact]
        public void Update_RaisesChanged_AfterCurrentHasMoved()
        {
            // A handler that reads Current must not see the value it is being told changed.
            using var temporary = new TemporaryHostPreferences();
            var store = temporary.CreateStore();
            AppThemePreference? observed = null;

            store.Changed += () => observed = store.Current.Theme;
            store.Update(preferences => preferences with { Theme = AppThemePreference.Dark });

            Assert.Equal(AppThemePreference.Dark, observed);
        }

        [Fact]
        public void Update_Rejects_ANullMutation()
        {
            using var temporary = new TemporaryHostPreferences();

            Assert.Throws<ArgumentNullException>(() => temporary.CreateStore().Update(null!));
        }

        [Fact]
        public void Constructor_Rejects_ANullPathProvider()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonHostPreferencesStore(null!));
        }

        [Fact]
        public void Current_IsDefault_WhenTheFileIsCorrupt()
        {
            using var temporary = new TemporaryHostPreferences();

            temporary.WriteFile("{ this was hand-edited badly");

            Assert.Equal(HostPreferences.Default, temporary.CreateStore().Current);
        }

        [Fact]
        public void Current_IsDefault_WhenThePathIsADirectory()
        {
            using var temporary = new TemporaryHostPreferences();

            Directory.CreateDirectory(temporary.FilePath);

            var store = temporary.CreateStore();
            var exception = Record.Exception(() => store.Current);

            Assert.Null(exception);
            Assert.Equal(HostPreferences.Default, store.Current);
        }

        [Fact]
        public void Current_IsDefault_WhenThePathProviderThrows()
        {
            var store = new JsonHostPreferencesStore(
                FakeHostPreferencesPathProvider.Failing(new IOException("no home directory")));

            Assert.Equal(HostPreferences.Default, store.Current);
        }

        [Fact]
        public void ReadingCurrent_ReadsTheFileOnce_AndDoesNotHoldItOpen()
        {
            using var temporary = new TemporaryHostPreferences();

            temporary.WriteFile("""{ "theme": "Dark" }""");

            var provider = new FakeHostPreferencesPathProvider(temporary.FilePath);
            var store = new JsonHostPreferencesStore(provider);

            _ = store.Current;
            _ = store.Current;
            _ = store.Current;

            Assert.Equal(1, provider.GetFilePathCallCount);

            // Not held open: the file is deletable straight after the read.
            File.Delete(temporary.FilePath);
        }

        [Fact]
        public void Update_DoesNotPropagate_AWriteFailure()
        {
            using var temporary = new TemporaryHostPreferences();

            // A file where the store wants a directory: Directory.CreateDirectory fails on every
            // platform, which is the closest reproducible stand-in for a read-only home directory.
            var blocked = Path.Combine(temporary.Root, "blocked");

            Directory.CreateDirectory(temporary.Root);
            File.WriteAllText(blocked, "not a directory");

            var store = new JsonHostPreferencesStore(
                new FakeHostPreferencesPathProvider(Path.Combine(blocked, "preferences.json")));

            var exception = Record.Exception(
                () => store.Update(preferences => preferences with { Theme = AppThemePreference.Dark }));

            Assert.Null(exception);
        }

        [Fact]
        public void Update_KeepsTheInMemoryValue_WhenTheWriteFailed()
        {
            // The session must agree with what the user just did even when the disk refused.
            using var temporary = new TemporaryHostPreferences();
            var blocked = Path.Combine(temporary.Root, "blocked");

            Directory.CreateDirectory(temporary.Root);
            File.WriteAllText(blocked, "not a directory");

            var store = new JsonHostPreferencesStore(
                new FakeHostPreferencesPathProvider(Path.Combine(blocked, "preferences.json")));
            var raised = 0;

            store.Changed += () => raised++;
            store.Update(preferences => preferences with { Theme = AppThemePreference.Dark });

            Assert.Equal(AppThemePreference.Dark, store.Current.Theme);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void Update_DoesNotPropagate_APathProviderFailure()
        {
            var store = new JsonHostPreferencesStore(
                FakeHostPreferencesPathProvider.Failing(new UnauthorizedAccessException()));

            var exception = Record.Exception(
                () => store.Update(preferences => preferences with { Motion = MotionPreference.AlwaysReduce }));

            Assert.Null(exception);
            Assert.Equal(MotionPreference.AlwaysReduce, store.Current.Motion);
        }
    }
}
