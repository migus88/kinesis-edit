using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class NullAppPreferencesStoreTests
    {
        [Fact]
        public void IsHidden_ForEverySuppressionKey_ReturnsFalse()
        {
            var store = NullAppPreferencesStore.Instance;

            Assert.All(NotificationKeys.All, key => Assert.False(store.IsHidden(key)));
        }

        [Fact]
        public void SetHidden_WhenCalled_PersistsNothing()
        {
            var store = new NullAppPreferencesStore();

            store.SetHidden(NotificationKeys.Save, true);

            Assert.False(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void Current_Always_IsEmptySoEveryPreferenceSitsAtItsDefault()
        {
            var store = new NullAppPreferencesStore();

            Assert.Same(AppSettings.Empty, store.Current);
            Assert.All(
                AppPreferenceCatalog.All,
                descriptor => Assert.Equal(descriptor.DefaultValue, descriptor.GetValue(store.Current)));
        }

        [Fact]
        public void Update_WhenCalled_ChangesNothingAndRaisesNothing()
        {
            var store = new NullAppPreferencesStore();
            var changes = 0;
            store.Changed += () => changes++;

            store.Update(settings => settings with { IsAdvisoryDetailExpanded = true });

            Assert.False(store.IsWritable);
            Assert.Null(store.Current.IsAdvisoryDetailExpanded);
            Assert.Equal(0, changes);
        }

        [Fact]
        public void Update_WithoutAMutation_Throws()
        {
            var store = new NullAppPreferencesStore();

            Assert.Throws<ArgumentNullException>(() => store.Update(null!));
        }
    }
}
