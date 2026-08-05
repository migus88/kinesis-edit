using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class MessageBoxPresenterTests
    {
        [Fact]
        public void Constructor_WithoutAnOwnerAccessor_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MessageBoxPresenter(null!));
        }

        [Fact]
        public async Task PresentAsync_WithoutARequest_ThrowsBeforeOpeningAWindow()
        {
            var presenter = new MessageBoxPresenter(() => null);

            await Assert.ThrowsAsync<ArgumentNullException>(() => presenter.PresentAsync(null!));
        }
    }
}
