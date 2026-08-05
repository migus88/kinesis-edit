using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests
{
    public class ViewLocatorTests
    {
        [Fact]
        public void ResolveViewType_ForAViewModelWithAView_ReturnsThatView()
        {
            Assert.Equal(typeof(DashboardView), ViewLocator.ResolveViewType(typeof(DashboardViewModel)));
            Assert.Equal(typeof(DeviceCardView), ViewLocator.ResolveViewType(typeof(DeviceCardViewModel)));
            Assert.Equal(typeof(NoDeviceView), ViewLocator.ResolveViewType(typeof(NoDeviceViewModel)));
            Assert.Equal(typeof(EditorPlaceholderView), ViewLocator.ResolveViewType(typeof(EditorPlaceholderViewModel)));
            Assert.Equal(typeof(SavantElitePedalView), ViewLocator.ResolveViewType(typeof(SavantElitePedalViewModel)));
            Assert.Equal(typeof(ToastView), ViewLocator.ResolveViewType(typeof(ToastViewModel)));
            Assert.Equal(typeof(LoadingView), ViewLocator.ResolveViewType(typeof(LoadingViewModel)));
        }

        [Fact]
        public void ResolveViewType_WhenNoViewIsNamedAfterTheViewModel_ReturnsNull()
        {
            // The shell's window is not resolved by name: it is created by the composition root.
            Assert.Null(ViewLocator.ResolveViewType(typeof(MainWindowViewModel)));
        }

        [Fact]
        public void ResolveViewType_WithNull_ReturnsNull()
        {
            Assert.Null(ViewLocator.ResolveViewType(null));
        }

        [Fact]
        public void Match_ForAnAppViewModel_IsTrue()
        {
            var locator = new ViewLocator();

            Assert.True(locator.Match(new LoadingViewModel()));
        }

        [Fact]
        public void Match_ForAnythingElse_IsFalse()
        {
            var locator = new ViewLocator();

            Assert.False(locator.Match(null));
            Assert.False(locator.Match("Connected"));
        }
    }
}
