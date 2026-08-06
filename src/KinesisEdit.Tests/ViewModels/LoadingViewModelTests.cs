using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class LoadingViewModelTests
    {
        [Fact]
        public void Caption_WithoutCaption_IsTheSpecDefault()
        {
            var viewModel = new LoadingViewModel();

            Assert.Equal("Loading…", viewModel.Caption);
            Assert.False(viewModel.IsVisible);
        }

        [Fact]
        public void Caption_WhenSetToBlank_FallsBackToTheSpecDefault()
        {
            var viewModel = new LoadingViewModel("Loading TKO…")
            {
                Caption = "   "
            };

            Assert.Equal("Loading…", viewModel.Caption);
        }
    }
}
