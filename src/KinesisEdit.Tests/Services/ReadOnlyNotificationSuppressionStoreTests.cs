using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class ReadOnlyNotificationSuppressionStoreTests
    {
        [Fact]
        public void IsHidden_Always_ReadsThroughToTheWrappedStore()
        {
            var source = new FakeNotificationSuppressionStore();
            source.SetInitiallyHidden(NotificationKeys.Save);
            var store = new ReadOnlyNotificationSuppressionStore(source);

            Assert.True(store.IsHidden(NotificationKeys.Save));
            Assert.False(store.IsHidden(NotificationKeys.Speed));
        }

        [Fact]
        public void SetHidden_Always_WritesNothing()
        {
            var source = new FakeNotificationSuppressionStore();
            source.SetInitiallyHidden(NotificationKeys.Save);
            var store = new ReadOnlyNotificationSuppressionStore(source);

            store.SetHidden(NotificationKeys.Save, false);
            store.SetHidden(NotificationKeys.Speed, true);

            Assert.Empty(source.Writes);
            Assert.True(store.IsHidden(NotificationKeys.Save));
            Assert.False(store.IsHidden(NotificationKeys.Speed));
        }

        [Fact]
        public void Constructor_WithoutASource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ReadOnlyNotificationSuppressionStore(null!));
        }
    }
}
