using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's Settings tab: the device-side keyboard settings of specs/08-settings.md §5,
    /// rendered as the rows the device's <c>SettingsCapability</c> designates
    /// (<see cref="KeyboardSettingsRows"/>) and saved back through
    /// <see cref="ISettingsService"/>.
    /// <para>
    /// It owns no settings rules: which keys exist and in what form is the capability's, the
    /// ranges are <see cref="SettingsValueRanges"/>, the startup-profile ↔ <c>led_mode</c>
    /// pairing is <c>StartupProfileSettings</c>, the Advantage2 4MB lock is
    /// <c>KeyboardSettingsGate</c>, and the post-save wording is
    /// <see cref="SettingsMessageCatalog"/>.
    /// </para>
    /// </summary>
    public sealed class KeyboardSettingsViewModel : ViewModelBase
    {
        /// <summary>Title of everything a settings save raises — its failure dialog and its toast.</summary>
        public const string SaveTitle = "Save Settings";

        /// <summary>Caption of the loading indicator during a settings save. Not a spec string.</summary>
        public const string SavingCaption = "Saving settings...";

        /// <summary>Message prefix when the save threw; the exception's message follows it.</summary>
        public const string SaveErrorMessagePrefix = "The keyboard settings could not be saved: ";

        /// <summary>Message prefix shown inline when the settings file could not be read.</summary>
        public const string LoadErrorMessagePrefix = "The keyboard settings could not be read from the v-Drive: ";

        /// <summary>The note shown instead of a Save button in demo mode (03 §3.5, 08 §3).</summary>
        public const string DemoModeHint = "This device is open in demo mode, so settings are shown but cannot be saved.";

        /// <summary>
        /// The note shown in demo mode when there is no drive at all: unlike <see cref="DemoModeHint"/>
        /// it does not claim the settings are shown, because nothing was read.
        /// </summary>
        public const string NoDriveHint =
            "This device has no v-Drive attached, so its settings could not be read and cannot be saved.";

        /// <summary>Whether <paramref name="device"/> has an app-managed keyboard-settings file at all.</summary>
        public static bool HasSettings(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            // SettingsCapability is a record, so this is value equality: a device designating no
            // writable key at all is "no settings", however its capability was built. It is the
            // same question KeyboardSettingsRows.Create answers with an empty list.
            return device.Settings != SettingsCapability.None;
        }

        /// <summary>The controls this device shows, built once from its capability.</summary>
        public IReadOnlyList<SettingsRowViewModel> Rows { get; }

        /// <summary>
        /// Whether a read of the settings file is in flight (or has not been started yet). It
        /// gates nothing — <see cref="HasLoadedSettings"/> does that — because a read can also
        /// finish by failing.
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Whether a read actually <b>succeeded</b> — deliberately not the negation of
        /// <see cref="IsLoading"/>: a read that threw, or a device with no drive to read, also
        /// ends the loading state, and in both cases the rows still hold their constructor
        /// defaults. Saving them would write this panel's invented values (a Freestyle Edge RGB's
        /// <c>v_drive=manual</c> among them) over a file nobody has looked at, so it gates both
        /// <see cref="SaveCommand"/> and the rows' editability.
        /// </summary>
        public bool HasLoadedSettings
        {
            get => _hasLoadedSettings;
            private set
            {
                if (SetProperty(ref _hasLoadedSettings, value))
                {
                    RefreshState();
                }
            }
        }

        /// <summary>Whether a save is in flight.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RefreshState();
                }
            }
        }

        /// <summary>
        /// False only on an Advantage2 whose version file carries no <c>4MB</c> marker: spec 08
        /// §5.4 disables the whole panel there, and <see cref="LockHint"/> says why.
        /// </summary>
        public bool CanEditSettings { get; }

        /// <summary>Whether the panel is locked — the negation of <see cref="CanEditSettings"/>.</summary>
        public bool IsLocked => !CanEditSettings;

        /// <summary>Core's explanation of the lock; empty while the panel is editable.</summary>
        public string LockHint => IsLocked ? SettingsMessageCatalog.Advantage2SettingsDisabledHint : string.Empty;

        /// <summary>An inline note about this panel's state — the demo-mode hint or a read failure.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetProperty(ref _statusMessage, value))
                {
                    OnPropertyChanged(nameof(HasStatusMessage));
                }
            }
        }

        /// <summary>Whether there is a <see cref="StatusMessage"/> to show.</summary>
        public bool HasStatusMessage => StatusMessage.Length > 0;

        /// <summary>Writes the rows back to the device's settings file; never available in demo mode (03 §3.5).</summary>
        public IAsyncRelayCommand SaveCommand { get; }

        private readonly DeviceSnapshot _device;
        private readonly ISettingsService _settings;
        private readonly INotificationService _notifications;
        private KeyboardSettings _loaded = KeyboardSettings.Empty;
        private string _statusMessage = string.Empty;
        private string? _loadErrorMessage;
        private bool _isLoading = true;
        private bool _hasLoadedSettings;
        private bool _isBusy;
        private bool _hasLoadStarted;

        /// <summary>
        /// Creates the panel for <paramref name="device"/>. Construction touches no file — the
        /// rows come from the catalog — so the editor can build it eagerly and let
        /// <see cref="LoadAsync"/> do the reading.
        /// </summary>
        public KeyboardSettingsViewModel(
            DeviceSnapshot device,
            ISettingsService settings,
            INotificationService notifications)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

            Rows = KeyboardSettingsRows.Create(device.Device.Settings);

            // Demo mode passes the gate, as every firmware gate does (FirmwareGateService, spec 09
            // §2): the version file of a keyboard that is not attached carries no 4MB marker, and
            // explaining the Advantage2's 2MB lock about a board nobody plugged in would be a
            // fabricated diagnosis. Nothing is written in demo mode anyway.
            CanEditSettings = device.IsDemoMode
                || KeyboardSettingsGate.CanEditKeyboardSettings(device.DeviceId, device.VersionFile);

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave());

            RefreshStatusMessage();
            RefreshState();
        }

        /// <summary>
        /// Reads the device's settings file off the UI thread and fills the rows in. Total: a
        /// failure leaves the rows at their defaults, is reported inline rather than as a second
        /// modal on top of the editor's own load dialog, and — because nothing was read — leaves
        /// <see cref="HasLoadedSettings"/> false, so nothing can be written back. A second call
        /// is a no-op.
        /// <para>
        /// Demo mode <b>does</b> read: it is entered for a drive that is merely not writable
        /// (03 §3.5), and spec 08 §3 bans saving there, not loading — the same rule the colour
        /// picker's custom slots follow. Only a device with no <c>Location</c> at all reads
        /// nothing, because there is no file to read.
        /// </para>
        /// </summary>
        public async Task LoadAsync()
        {
            if (_hasLoadStarted)
            {
                return;
            }

            _hasLoadStarted = true;

            try
            {
                if (Rows.Count == 0 || _device.Location is null)
                {
                    return;
                }

                var location = _device.Location;

                KeyboardSettings? loaded = null;
                Exception? error = null;

                try
                {
                    loaded = await Task.Run(() => _settings.LoadKeyboardSettings(location)).ConfigureAwait(true);
                }
                catch (Exception exception)
                {
                    error = exception;
                }

                if (loaded is not null)
                {
                    Apply(loaded);
                }
                else
                {
                    _loadErrorMessage = LoadErrorMessagePrefix + error!.Message;

                    RefreshStatusMessage();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Apply(KeyboardSettings settings)
        {
            _loaded = settings;

            foreach (var row in Rows)
            {
                row.ReadFrom(settings);
            }

            HasLoadedSettings = true;
        }

        private bool CanSave()
        {
            // Demo mode never writes (03 §3.5, 08 §3), a 2MB Advantage2 is locked outright
            // (08 §5.4), a device with no settings file has nothing to write, and a panel that
            // never read the file would write its own defaults over the device's values.
            return Rows.Count > 0
                && CanEditSettings
                && !_device.IsDemoMode
                && _device.Location is not null
                && HasLoadedSettings
                && !IsBusy;
        }

        /// <summary>
        /// Collects the rows into a model and writes it, between the loading indicator's show and
        /// hide. Every value is already clamped into <see cref="SettingsValueRanges"/> by its row,
        /// so the serializer's range check cannot fire; a throw is still caught and reported,
        /// because an exception escaping an <c>AsyncRelayCommand</c> is an unhandled crash.
        /// </summary>
        private async Task SaveAsync()
        {
            if (!CanSave())
            {
                return;
            }

            var location = _device.Location!;
            var versionFile = _device.VersionFile;
            var updated = BuildSettings();

            Exception? error = null;

            IsBusy = true;
            _notifications.ShowLoading(SavingCaption);

            try
            {
                await Task.Run(() => _settings.SaveKeyboardSettings(location, versionFile, updated)).ConfigureAwait(true);

                _loaded = updated;
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                _notifications.HideLoading();

                IsBusy = false;
            }

            if (error is not null)
            {
                await TryShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = SaveTitle,
                    Message = SaveErrorMessagePrefix + error.Message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return;
            }

            // Spec 08 §5.1's post-save notice, verbatim from Core. It is a toast for the same
            // reason the profile save's is: the editor already carries the outcome, and a modal
            // between Save and the eject is the legacy app's habit, not a requirement.
            _notifications.ShowToast(new ToastRequest
            {
                Title = SettingsMessageCatalog.SettingsSavedTitle,
                Message = SettingsMessageCatalog.SettingsSavedMessage
            });
        }

        private KeyboardSettings BuildSettings()
        {
            // Starting from the loaded model keeps the reserved keys the parser read (spec 08 §2)
            // in state; they have no serializer path, so they are still never written.
            var settings = _loaded;

            foreach (var row in Rows)
            {
                settings = row.ApplyTo(settings);
            }

            return settings;
        }

        private async Task TryShowMessageBoxAsync(MessageBoxRequest request)
        {
            try
            {
                await _notifications.ShowMessageBoxAsync(request).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // A box that cannot be put on screen (the window is already gone) must not bring
                // the app down; the panel state already carries the outcome.
            }
        }

        private void RefreshState()
        {
            // A row is editable only once it holds what the device actually has: rows showing
            // their constructor defaults after a failed (or impossible) read are not the
            // keyboard's settings, and offering them for editing would be inviting the write
            // CanSave refuses.
            var isRowEnabled = CanEditSettings && HasLoadedSettings && !IsBusy;

            foreach (var row in Rows)
            {
                row.IsEnabled = isRowEnabled;
            }

            SaveCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Rebuilds the inline note out of its two independent parts: the demo-mode / no-drive
        /// hint the panel opens with, and a read failure. A failed read in demo mode has to say
        /// both — the earlier "last writer wins" hid the demo hint behind the error.
        /// </summary>
        private void RefreshStatusMessage()
        {
            var lines = new List<string>(2);

            if (_device.Location is null)
            {
                lines.Add(NoDriveHint);
            }
            else if (_device.IsDemoMode)
            {
                lines.Add(DemoModeHint);
            }

            if (_loadErrorMessage is not null)
            {
                lines.Add(_loadErrorMessage);
            }

            StatusMessage = string.Join(Environment.NewLine, lines);
        }
    }
}
