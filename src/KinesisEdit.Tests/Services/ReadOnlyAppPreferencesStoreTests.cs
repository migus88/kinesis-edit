using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class ReadOnlyAppPreferencesStoreTests
    {
        [Fact]
        public void IsHidden_Always_ReadsThroughToTheWrappedStore()
        {
            var source = new FakeAppPreferencesStore();
            source.SetInitiallyHidden(NotificationKeys.Save);
            var store = new ReadOnlyAppPreferencesStore(source);

            Assert.True(store.IsHidden(NotificationKeys.Save));
            Assert.False(store.IsHidden(NotificationKeys.Speed));
        }

        [Fact]
        public void SetHidden_Always_WritesNothing()
        {
            var source = new FakeAppPreferencesStore();
            source.SetInitiallyHidden(NotificationKeys.Save);
            var store = new ReadOnlyAppPreferencesStore(source);

            store.SetHidden(NotificationKeys.Save, false);
            store.SetHidden(NotificationKeys.Speed, true);

            Assert.Empty(source.Writes);
            Assert.True(store.IsHidden(NotificationKeys.Save));
            Assert.False(store.IsHidden(NotificationKeys.Speed));
        }

        [Fact]
        public void Current_Always_ServesTheDrivesRealPreferences()
        {
            // specs/08-settings.md §3 bans saving in demo mode, not loading: the screen shows what
            // the board actually carries, swatches and all.
            var source = new FakeAppPreferencesStore();
            source.SetInitial(AppSettings.Empty with
            {
                IsAdvisoryDetailExpanded = true,
                IsResetLayerConfirmationHidden = true
            });
            var store = new ReadOnlyAppPreferencesStore(source);

            Assert.False(store.IsWritable);
            Assert.True(store.Current.IsAdvisoryDetailExpanded);
            Assert.True(store.Current.IsResetLayerConfirmationHidden);
        }

        [Fact]
        public void Update_Always_DiscardsTheChangeWithoutRaisingChanged()
        {
            var source = new FakeAppPreferencesStore();
            var store = new ReadOnlyAppPreferencesStore(source);
            var changes = 0;
            store.Changed += () => changes++;

            store.Update(settings => settings with { IsAdvisoryDetailExpanded = true });

            Assert.Equal(0, source.UpdateCount);
            Assert.Null(store.Current.IsAdvisoryDetailExpanded);
            Assert.Equal(0, changes);
        }

        [Fact]
        public void Changed_Always_ForwardsToTheWrappedStore()
        {
            var source = new FakeAppPreferencesStore();
            var store = new ReadOnlyAppPreferencesStore(source);
            var changes = 0;
            void Handler()
            {
                changes++;
            }

            store.Changed += Handler;
            source.Update(settings => settings with { IsAdvisoryDetailExpanded = true });
            store.Changed -= Handler;
            source.Update(settings => settings with { IsAdvisoryDetailExpanded = false });

            Assert.Equal(1, changes);
        }

        [Fact]
        public void Constructor_WithoutASource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ReadOnlyAppPreferencesStore(null!));
        }
    }
}
