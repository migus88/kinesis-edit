using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class NullNotificationSuppressionStoreTests
    {
        [Fact]
        public void IsHidden_ForEverySpecKey_ReturnsFalse()
        {
            var store = NullNotificationSuppressionStore.Instance;

            Assert.All(NotificationKeys.All, key => Assert.False(store.IsHidden(key)));
        }

        [Fact]
        public void SetHidden_WhenCalled_PersistsNothing()
        {
            var store = new NullNotificationSuppressionStore();

            store.SetHidden(NotificationKeys.Save, true);

            Assert.False(store.IsHidden(NotificationKeys.Save));
        }
    }
}
