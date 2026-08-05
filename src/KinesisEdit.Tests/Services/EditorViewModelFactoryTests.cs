using KinesisEdit.Core.Devices;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Services
{
    public sealed class EditorViewModelFactoryTests : IDisposable
    {
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly EditorViewModelFactory _factory;

        public EditorViewModelFactoryTests()
        {
            _factory = new EditorViewModelFactory(_profiles, () => _capture, _notifications);
        }

        [Fact]
        public void Create_ForADeviceWithAnAuthoredPicture_ReturnsTheRealEditor()
        {
            var editor = _factory.Create(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            var deviceEditor = Assert.IsType<DeviceEditorViewModel>(editor);

            Assert.Equal("Freestyle Edge RGB", deviceEditor.DeviceName);
            Assert.True(deviceEditor.IsLoading);

            deviceEditor.Dispose();
        }

        [Fact]
        public void Create_ForADeviceWithoutAnAuthoredPicture_ReturnsThePlaceholder()
        {
            // Issues #39-#42 author the remaining board pictures; until then those devices open
            // into the placeholder rather than an empty keyboard.
            Assert.IsType<EditorPlaceholderViewModel>(_factory.Create(TestDevices.CreateSnapshot(DeviceId.Tko)));
            Assert.IsType<EditorPlaceholderViewModel>(_factory.Create(TestDevices.CreateSnapshot(DeviceId.Advantage2)));
            Assert.IsType<EditorPlaceholderViewModel>(_factory.Create(TestDevices.CreateSnapshot(DeviceId.SavantElite2)));
        }

        [Fact]
        public void Create_TheRealEditor_ResolvesTheCaptureServiceLazily()
        {
            var resolutions = 0;

            var factory = new EditorViewModelFactory(
                _profiles,
                () =>
                {
                    resolutions++;

                    return _capture;
                },
                _notifications);

            Assert.Equal(0, resolutions);

            using var editor = (DeviceEditorViewModel)factory.Create(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            Assert.Equal(1, resolutions);
        }

        [Fact]
        public void Constructor_WithoutACollaborator_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(null!, () => _capture, _notifications));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(_profiles, null!, _notifications));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(_profiles, () => _capture, null!));
        }

        public void Dispose()
        {
            _capture.Dispose();
        }
    }
}
