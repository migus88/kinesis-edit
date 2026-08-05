using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The one place that decides which editor a device opens into: the Savant Elite2's read-only
    /// pedal view, the keyboard editor when the board can be drawn, and the placeholder otherwise.
    /// "Can be drawn" is both catalogs answering: <see cref="GeometryCatalog"/> for the key
    /// positions and <see cref="VisualCatalog"/> for where they sit. Only the Freestyle Edge RGB
    /// has an authored picture today — issues #39-#42 add the rest, and each one becomes editable
    /// by adding data, never by changing this class.
    /// <para>
    /// The branch lives here rather than in <see cref="ViewModels.MainWindowViewModel"/> so that
    /// the shell does not accumulate every device's dependencies as editors are added: this class
    /// holds them, the shell holds this class.
    /// </para>
    /// </summary>
    public sealed class EditorViewModelFactory : IEditorViewModelFactory
    {
        private readonly IProfileSessionFactory _profileSessions;
        private readonly Func<IKeystrokeCaptureService> _captureServiceResolver;
        private readonly INotificationService _notifications;
        private readonly PedalFileService _pedalFiles;
        private readonly IFolderPickerService _folderPicker;
        private readonly IFilePickerService _filePicker;
        private readonly IVDriveFileService _files;
        private readonly IUrlLauncher _urlLauncher;

        /// <summary>
        /// Creates the factory. The capture service is resolved through a delegate rather than
        /// held directly, for the same reason the message-box presenter resolves its owner window
        /// through one: it is built over the shell window, which does not exist while the
        /// composition root wires the view models (docs/app/app-shell.md). The two pickers, the
        /// file service and the URL launcher are the keyboard editor's feature-panel dependencies
        /// (Export, Import, the firmware gates of spec 11) and are held here for the same reason
        /// everything else is: so the shell never accumulates a device's dependencies.
        /// </summary>
        public EditorViewModelFactory(
            IProfileSessionFactory profileSessions,
            Func<IKeystrokeCaptureService> captureServiceResolver,
            INotificationService notifications,
            PedalFileService pedalFiles,
            IFolderPickerService folderPicker,
            IFilePickerService filePicker,
            IVDriveFileService files,
            IUrlLauncher urlLauncher)
        {
            _profileSessions = profileSessions ?? throw new ArgumentNullException(nameof(profileSessions));
            _captureServiceResolver = captureServiceResolver ?? throw new ArgumentNullException(nameof(captureServiceResolver));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _pedalFiles = pedalFiles ?? throw new ArgumentNullException(nameof(pedalFiles));
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
        }

        /// <inheritdoc />
        public DeviceEditorViewModel Create(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            // The pedal reads its file itself (specs/12-savant-elite.md §4.6: pedals.txt is the
            // whole model, so there is nothing for the detection loop to carry) and has no
            // keyboard picture at all, so it is answered before the catalogs are asked.
            if (device.DeviceId == DeviceId.SavantElite2)
            {
                return new SavantElitePedalViewModel(device, _pedalFiles);
            }

            if (!CanRender(device))
            {
                return new EditorPlaceholderViewModel(device);
            }

            return new KeyboardEditorViewModel(
                device,
                _profileSessions,
                _captureServiceResolver(),
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);
        }

        private static bool CanRender(DeviceSnapshot device)
        {
            return VisualCatalog.TryGet(device.DeviceId, out _) && GeometryCatalog.TryGet(device.DeviceId, out _);
        }
    }
}
