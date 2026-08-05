using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class ToastViewModelTests
    {
        [Fact]
        public void Timeout_WithoutExplicitTimeout_IsTheSpecDefaultOfFiveSeconds()
        {
            var viewModel = new ToastViewModel(new ToastRequest
            {
                Message = "Safe To Remove Hardware"
            });

            Assert.Equal(TimeSpan.FromSeconds(5), viewModel.Timeout);
            Assert.Equal("Safe To Remove Hardware", viewModel.Message);
            Assert.False(viewModel.HasTitle);
            Assert.Equal(ToastPosition.BottomRight, viewModel.Position);
        }

        [Fact]
        public void Position_WhenRequestedCentered_IsReported()
        {
            var viewModel = new ToastViewModel(new ToastRequest
            {
                Message = "Disconnecting v-Drive",
                Position = ToastPosition.Center
            });

            Assert.Equal(ToastPosition.Center, viewModel.Position);
        }

        [Fact]
        public void Title_WhenProvided_IsReported()
        {
            var viewModel = new ToastViewModel(new ToastRequest
            {
                Title = "Eject",
                Message = "Disconnecting v-Drive",
                Timeout = TimeSpan.FromSeconds(2)
            });

            Assert.True(viewModel.HasTitle);
            Assert.Equal("Eject", viewModel.Title);
            Assert.Equal(TimeSpan.FromSeconds(2), viewModel.Timeout);
        }
    }
}
