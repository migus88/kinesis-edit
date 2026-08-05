using KinesisEdit.Core.Devices;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Services
{
    public sealed class EditorViewModelFactoryTests : IDisposable
    {
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeVDriveFileService _fileService = new();
        private readonly PedalFileService _pedalFiles;
        private readonly EditorViewModelFactory _factory;

        public EditorViewModelFactoryTests()
        {
            _pedalFiles = new PedalFileService(_fileService);
            _factory = new EditorViewModelFactory(_profiles, () => _capture, _notifications, _pedalFiles);
        }

        [Fact]
        public void Create_ForADeviceWithAnAuthoredPicture_ReturnsTheKeyboardEditor()
        {
            var editor = _factory.Create(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            var keyboardEditor = Assert.IsType<KeyboardEditorViewModel>(editor);

            Assert.Equal("Freestyle Edge RGB", keyboardEditor.DeviceName);
            Assert.True(keyboardEditor.IsLoading);

            keyboardEditor.Dispose();
        }

        [Fact]
        public async Task Create_ForTheSavantElite2_ReturnsThePedalEditor()
        {
            // The pedal has no keyboard picture and never will: its editor is chosen by device id,
            // ahead of the catalogs (specs/12-savant-elite.md).
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);
            _fileService.SetFile(PedalFileService.GetPedalFilePath(snapshot.Location!), "[lpedal]>[lmouse]");

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_factory.Create(snapshot));

            Assert.True(pedal.IsLoading);

            await pedal.LoadAsync();

            Assert.Equal(PedalLoadState.Loaded, pedal.LoadState);
            Assert.Equal("[lmouse]", pedal.Inputs[0].AssignmentText);

            pedal.Dispose();
        }

        [Fact]
        public void Create_ForADeviceWithoutAnAuthoredPicture_ReturnsThePlaceholder()
        {
            // Issues #39-#42 author the remaining board pictures; until then those devices open
            // into the placeholder rather than an empty keyboard.
            Assert.IsType<EditorPlaceholderViewModel>(_factory.Create(TestDevices.CreateSnapshot(DeviceId.Tko)));
            Assert.IsType<EditorPlaceholderViewModel>(_factory.Create(TestDevices.CreateSnapshot(DeviceId.Advantage2)));
        }

        [Fact]
        public void Create_TheKeyboardEditor_ResolvesTheCaptureServiceLazily()
        {
            var resolutions = 0;

            var factory = new EditorViewModelFactory(
                _profiles,
                () =>
                {
                    resolutions++;

                    return _capture;
                },
                _notifications,
                _pedalFiles);

            Assert.Equal(0, resolutions);

            using var editor = (KeyboardEditorViewModel)factory.Create(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            Assert.Equal(1, resolutions);
        }

        [Fact]
        public void Create_ThePedalEditor_ResolvesTheCaptureServiceToo()
        {
            // The pedal records keystrokes exactly like the keyboard editor does
            // (specs/12-savant-elite.md §5 step 3), including in a demo session.
            var resolutions = 0;

            var factory = new EditorViewModelFactory(
                _profiles,
                () =>
                {
                    resolutions++;

                    return _capture;
                },
                _notifications,
                _pedalFiles);

            var pedal = (SavantElitePedalViewModel)factory.Create(
                TestDevices.CreateSnapshot(DeviceId.SavantElite2, VDriveConnectionStatus.CannotAccess));

            Assert.Equal(1, resolutions);
            Assert.True(_capture.HasSubscribers);

            pedal.Dispose();
        }

        [Fact]
        public void Create_AnEditorThatNeedsNoCapture_NeverResolvesTheCaptureService()
        {
            // The placeholder shows a caption and nothing else; every editor that reads the
            // keyboard resolves the shared service instead.
            var resolutions = 0;

            var factory = new EditorViewModelFactory(
                _profiles,
                () =>
                {
                    resolutions++;

                    return _capture;
                },
                _notifications,
                _pedalFiles);

            factory.Create(TestDevices.CreateSnapshot(DeviceId.Tko));
            factory.Create(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.Equal(0, resolutions);
        }

        [Fact]
        public void Create_WithoutADevice_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _factory.Create(null!));
        }

        [Fact]
        public void Constructor_WithoutACollaborator_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(null!, () => _capture, _notifications, _pedalFiles));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(_profiles, null!, _notifications, _pedalFiles));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(_profiles, () => _capture, null!, _pedalFiles));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(_profiles, () => _capture, _notifications, null!));
        }

        public void Dispose()
        {
            _capture.Dispose();
        }
    }
}
