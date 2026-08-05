using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class NotificationKeysTests
    {
        [Fact]
        public void All_Always_ListsTheTwelveSpecKeysInSpecOrder()
        {
            var expected = new[]
            {
                "app_intro_msg",
                "saveas_msg",
                "save_msg",
                "multiplay_msg",
                "speed_msg",
                "copy_macro_msg",
                "reset_key_msg",
                "app_checkfirm_msg",
                "savelighting_msg",
                "savesettings_msg",
                "windowscombo_msg",
                "updownkeystroke_msg"
            };

            Assert.Equal(expected, NotificationKeys.All);
        }
    }
}
