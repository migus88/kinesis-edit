using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Input;
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
        private readonly FakeSettingsService _settings = new();
        private readonly FakeFolderPickerService _folderPicker = new();
        private readonly FakeFilePickerService _filePicker = new();
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly PedalFileService _pedalFiles;
        private readonly EditorViewModelFactory _factory;

        public EditorViewModelFactoryTests()
        {
            _pedalFiles = new PedalFileService(_fileService);
            _factory = CreateFactory(() => _capture);
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
        public void Create_ForTheSavantElite2_ReturnsThePedalEditor()
        {
            // The pedal has no keyboard picture and never will: its editor is chosen by device id,
            // ahead of the catalogs (specs/12-savant-elite.md).
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);
            _fileService.SetFile(PedalFileService.GetPedalFilePath(snapshot.Location!), "[lpedal]>[lmouse]");

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_factory.Create(snapshot));

            Assert.Equal(PedalLoadState.Loaded, pedal.LoadState);
            Assert.Equal("[lmouse]", pedal.Inputs[0].AssignmentText);
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

            var factory = CreateFactory(() =>
            {
                resolutions++;

                return _capture;
            });

            Assert.Equal(0, resolutions);

            using var editor = (KeyboardEditorViewModel)factory.Create(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            Assert.Equal(1, resolutions);
        }

        [Fact]
        public void Create_AnEditorThatNeedsNoCapture_NeverResolvesTheCaptureService()
        {
            var resolutions = 0;

            var factory = CreateFactory(() =>
            {
                resolutions++;

                return _capture;
            });

            factory.Create(TestDevices.CreateSnapshot(DeviceId.SavantElite2, VDriveConnectionStatus.CannotAccess));
            factory.Create(TestDevices.CreateSnapshot(DeviceId.Tko));

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
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                null!, _settings, () => _capture, _notifications, _pedalFiles, _folderPicker, _filePicker, _fileService, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, null!, () => _capture, _notifications, _pedalFiles, _folderPicker, _filePicker, _fileService, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, _settings, null!, _notifications, _pedalFiles, _folderPicker, _filePicker, _fileService, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, _settings, () => _capture, null!, _pedalFiles, _folderPicker, _filePicker, _fileService, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, _settings, () => _capture, _notifications, null!, _folderPicker, _filePicker, _fileService, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, _settings, () => _capture, _notifications, _pedalFiles, null!, _filePicker, _fileService, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, _settings, () => _capture, _notifications, _pedalFiles, _folderPicker, null!, _fileService, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, _settings, () => _capture, _notifications, _pedalFiles, _folderPicker, _filePicker, null!, _urlLauncher));
            Assert.Throws<ArgumentNullException>(() => new EditorViewModelFactory(
                _profiles, _settings, () => _capture, _notifications, _pedalFiles, _folderPicker, _filePicker, _fileService, null!));
        }

        private EditorViewModelFactory CreateFactory(Func<IKeystrokeCaptureService> captureResolver)
        {
            return new EditorViewModelFactory(
                _profiles,
                _settings,
                captureResolver,
                _notifications,
                _pedalFiles,
                _folderPicker,
                _filePicker,
                _fileService,
                _urlLauncher);
        }

        public void Dispose()
        {
            _capture.Dispose();
        }
    }
}
