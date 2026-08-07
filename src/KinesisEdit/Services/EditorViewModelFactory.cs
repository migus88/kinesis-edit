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
    /// The one place that decides which editor a device opens into: the Savant Elite2's pedal
    /// editor, the keyboard editor when the board can be drawn, and the placeholder otherwise.
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
        private readonly ISettingsService _settings;
        private readonly Func<IKeystrokeCaptureService> _captureServiceResolver;
        private readonly INotificationService _notifications;
        private readonly PedalFileService _pedalFiles;
        private readonly IFolderPickerService _folderPicker;
        private readonly IFilePickerService _filePicker;
        private readonly IVDriveFileService _files;
        private readonly IUrlLauncher _urlLauncher;
        private readonly IDeviceSessionAccessor? _sessions;
        private readonly IMotionSettings? _motionSettings;
        private readonly IHostPreferencesStore? _hostPreferences;

        /// <summary>
        /// Creates the factory. The capture service is resolved through a delegate rather than
        /// held directly, for the same reason the message-box presenter resolves its owner window
        /// through one: it is built over the shell window, which does not exist while the
        /// composition root wires the view models (docs/app/app-shell.md). The settings seam feeds
        /// the editor's Settings and Lighting tabs — it is the app's single reader/writer of the
        /// settings files (docs/app/settings.md) — and the two pickers, the file service and the
        /// URL launcher are its feature-panel dependencies (Export, Import, the firmware gates of
        /// spec 11). All of them are held here for the same reason: so the shell never accumulates
        /// a device's dependencies.
        /// <para>
        /// <paramref name="sessions"/> is the seam through which an editor reaches the open
        /// device's <c>app_settings.txt</c> (<see cref="IAppPreferencesStore"/>). It is optional so
        /// that a factory built for a test or a design scene needs no session at all, and every
        /// preference then sits at its default.
        /// </para>
        /// <para>
        /// <paramref name="motionSettings"/> reaches exactly one tab: the Lighting tab's live
        /// preview is the app's only animated surface, and it freezes at frame zero when motion is
        /// reduced. It is optional for the same reason as <paramref name="sessions"/> — a test or a
        /// design scene that passes none animates, which is the state the app was designed in.
        /// </para>
        /// <para>
        /// <paramref name="hostPreferences"/> is the per-user store
        /// (docs/app/host-preferences.md) and reaches exactly one thing: the keyboard editor's key
        /// inspector rail, whose dragged width is remembered for this person on this machine. It is
        /// threaded through here rather than reached for as a singleton, for the same reason
        /// everything else on this list is — the composition root owns the one instance, and a
        /// second reader would show a stale copy. Optional like the two above: an editor built
        /// without one opens the rail at its authored width and forgets a drag.
        /// </para>
        /// </summary>
        public EditorViewModelFactory(
            IProfileSessionFactory profileSessions,
            ISettingsService settings,
            Func<IKeystrokeCaptureService> captureServiceResolver,
            INotificationService notifications,
            PedalFileService pedalFiles,
            IFolderPickerService folderPicker,
            IFilePickerService filePicker,
            IVDriveFileService files,
            IUrlLauncher urlLauncher,
            IDeviceSessionAccessor? sessions = null,
            IMotionSettings? motionSettings = null,
            IHostPreferencesStore? hostPreferences = null)
        {
            _profileSessions = profileSessions ?? throw new ArgumentNullException(nameof(profileSessions));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _captureServiceResolver = captureServiceResolver ?? throw new ArgumentNullException(nameof(captureServiceResolver));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _pedalFiles = pedalFiles ?? throw new ArgumentNullException(nameof(pedalFiles));
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _sessions = sessions;
            _motionSettings = motionSettings;
            _hostPreferences = hostPreferences;
        }

        /// <inheritdoc />
        public DeviceEditorViewModel Create(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            // The pedal reads its file itself (specs/12-savant-elite.md §4.6: pedals.txt is the
            // whole model, so there is nothing for the detection loop to carry) and has no
            // keyboard picture at all, so it is answered before the catalogs are asked. It records
            // keystrokes exactly like the keyboard editor does (12 §5 step 3), so it resolves the
            // same shared capture service.
            if (device.DeviceId == DeviceId.SavantElite2)
            {
                return new SavantElitePedalViewModel(device, _pedalFiles, _captureServiceResolver(), _notifications);
            }

            if (!CanRender(device))
            {
                return new EditorPlaceholderViewModel(device);
            }

            return new KeyboardEditorViewModel(
                device,
                _profileSessions,
                _settings,
                _captureServiceResolver(),
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher,
                _sessions,
                _motionSettings,
                _hostPreferences);
        }

        private static bool CanRender(DeviceSnapshot device)
        {
            return VisualCatalog.TryGet(device.DeviceId, out _) && GeometryCatalog.TryGet(device.DeviceId, out _);
        }
    }
}
