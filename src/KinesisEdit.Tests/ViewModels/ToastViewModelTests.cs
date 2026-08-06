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
        public void IsShown_StartsLowered_SoTheFirstFrameIsTheArrivingOne()
        {
            var viewModel = new ToastViewModel(new ToastRequest
            {
                Message = "Safe To Remove Hardware"
            });

            Assert.False(viewModel.IsShown);
        }

        [Fact]
        public void IsShown_WhenRaised_RaisesPropertyChangedSoTheFadeRuns()
        {
            var viewModel = new ToastViewModel(new ToastRequest
            {
                Message = "Safe To Remove Hardware"
            });

            var changed = new List<string?>();

            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            viewModel.IsShown = true;
            viewModel.IsShown = true;

            Assert.True(viewModel.IsShown);
            Assert.Equal([nameof(ToastViewModel.IsShown)], changed);
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
