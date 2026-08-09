using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.Transfer;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Profiles
{
    /// <summary>
    /// The load/edit/save unit for one numbered profile — Freestyle Edge/Pro, Freestyle Edge
    /// RGB, TKO, and Advantage 360 (Advantage2's position-based naming is out of scope, issue
    /// #37). Ties together the layout parser/serializer, the lighting engine, the settings
    /// engine, and v-Drive file I/O for one <c>layout&lt;n&gt;.txt</c>/<c>led&lt;n&gt;.txt</c>
    /// pair (specs/03-vdrive-and-files.md §4.1/§4.3, §5.3).
    /// <para>
    /// <b>A save writes files and nothing else — it never ejects.</b> Releasing the volume is the
    /// user's own deliberate action on the dashboard card
    /// (<c>DeviceEjectService</c>/<c>VDriveEjectNotifier</c>), so this session holds no
    /// <c>IVDriveEjector</c> and reaches no platform command. Do not re-add one: a drive
    /// unmounted behind the user's back is exactly what docs/design/README.md's "Nothing ejects
    /// implicitly" forbids.
    /// </para>
    /// </summary>
    public sealed class ProfileSession
    {
        // The defaults are shared by every session that does not name its own — a session is not
        // worth a file service of its own.
        private static readonly IVDriveFileService _defaultFileService = new VDriveFileService();
        private static readonly SettingsService _defaultSettingsService = new(_defaultFileService);

        /// <summary>
        /// The profile's key/layer/macro model, freshly built by <see cref="LayoutFileParser"/>
        /// (04 §4.2). Replaced wholesale by a layout <see cref="Import"/>, which is the same
        /// operation from a different source — hold the session, not this reference.
        /// </summary>
        public KeyboardLayout Layout { get; private set; }

        /// <summary>
        /// The device-appropriate lighting model (<see cref="Lighting.LightingModel"/>,
        /// <see cref="Lighting.TkoLightingModel"/>, or <see cref="Lighting.Advantage360LightingModel"/>);
        /// null when the device has no profile-orchestrated lighting file (FS Edge/Pro, see
        /// <see cref="ProfileLightingCodec"/>). Replaced wholesale by a lighting <see cref="Import"/>.
        /// </summary>
        public object? Lighting { get; private set; }

        /// <summary>Layout lines that could not be applied on load (04 §5); toggle <see cref="LayoutInvalidLine.Keep"/> before saving.</summary>
        public IReadOnlyList<LayoutInvalidLine> InvalidLines { get; private set; }

        /// <summary>The numbered profile position this session was loaded from.</summary>
        public int ProfileNumber { get; }

        /// <summary>The device this session belongs to.</summary>
        public DeviceId Device { get; }

        /// <summary>
        /// False only for the Advantage 360's profile 0 (<see cref="LayoutFileScheme.HasReadOnlyFactoryProfile"/>) —
        /// the factory profile has no on-disk file and cannot be saved to (specs/02-devices.md "Profiles 0-9").
        /// </summary>
        public bool CanSave => !IsReadOnlyProfile(_location.Device.LayoutScheme, ProfileNumber);

        /// <summary>
        /// Whether <see cref="Layout"/> (and <see cref="Lighting"/>, if present) currently
        /// re-serialize to lines different from the ones captured right after <see cref="Load"/>.
        /// Reuses <see cref="LayoutFileSerializer"/>/<see cref="ProfileLightingCodec"/> rather than
        /// a bespoke model comparison, so dirty tracking can never drift from what a save would
        /// actually write.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                var currentLayoutLines = LayoutFileSerializer.Serialize(Layout, InvalidLines);

                if (!currentLayoutLines.SequenceEqual(_originalLayoutLines))
                {
                    return true;
                }

                if (Lighting is null)
                {
                    return false;
                }

                var currentLightingLines = ProfileLightingCodec.Serialize(Device, Lighting);

                return !currentLightingLines.SequenceEqual(_originalLightingLines!);
            }
        }

        private readonly VDriveLocation _location;
        private readonly KeyboardSettings _settings;
        private readonly IReadOnlyList<string> _originalLayoutLines;
        private readonly IReadOnlyList<string>? _originalLightingLines;
        private readonly IVDriveFileService _fileService;
        private readonly SettingsService _settingsService;

        private ProfileSession(
            VDriveLocation location,
            DeviceId device,
            int profileNumber,
            KeyboardLayout layout,
            IReadOnlyList<LayoutInvalidLine> invalidLines,
            object? lighting,
            KeyboardSettings settings,
            IReadOnlyList<string> originalLayoutLines,
            IReadOnlyList<string>? originalLightingLines,
            IVDriveFileService fileService,
            SettingsService settingsService)
        {
            _location = location;
            Device = device;
            ProfileNumber = profileNumber;
            Layout = layout;
            InvalidLines = invalidLines;
            Lighting = lighting;
            _settings = settings;
            _originalLayoutLines = originalLayoutLines;
            _originalLightingLines = originalLightingLines;
            _fileService = fileService;
            _settingsService = settingsService;
        }

        /// <summary>
        /// Loads profile <paramref name="profileNumber"/> of <paramref name="device"/> from
        /// <paramref name="location"/>: the layout file, the paired led file where the device has
        /// one, and the keyboard-settings snapshot. Every call builds a brand-new instance —
        /// never reload an existing one — which is what gives the spec 04 §4.2
        /// "full-model-wipe-on-load" guarantee its orchestration-level meaning, on top of
        /// <see cref="LayoutFileParser.Parse"/> already building a fresh <see cref="KeyboardLayout"/>
        /// every call. Throws <see cref="ProfileReadOnlyException"/> for the Advantage 360's
        /// profile 0, which has no on-disk file to read (specs/02-devices.md "Profiles 0-9").
        /// <para>
        /// <paramref name="fileService"/> is the module's whole substitution seam: omit it and the
        /// session reads and writes exactly as it always has (the shared real service); name one
        /// and <b>every</b> read and write of the session goes through it — layout, led <i>and</i>
        /// the settings file, whose <see cref="SettingsService"/> is built over the same instance,
        /// so a session can never straddle two sources. Core stays unaware of why a caller would
        /// want that — there is no demo-mode concept here. There is no ejector to substitute,
        /// because a session never ejects.
        /// </para>
        /// </summary>
        public static ProfileSession Load(
            VDriveLocation location,
            DeviceId device,
            int profileNumber,
            IVDriveFileService? fileService = null)
        {
            ArgumentNullException.ThrowIfNull(location);

            if (location.Device.Id != device)
            {
                throw new ArgumentException(
                    $"The v-Drive location belongs to {location.Device.Id}, not {device}.", nameof(device));
            }

            var scheme = location.Device.LayoutScheme;

            if (scheme.Kind != LayoutSchemeKind.NumberedProfiles)
            {
                throw new NotSupportedException(
                    $"{device} does not use the numbered-profile layout scheme; profile orchestration covers "
                    + "Freestyle Edge/Pro, Freestyle Edge RGB, TKO, and Advantage 360 only.");
            }

            EnsureNotReadOnlyProfile(scheme, profileNumber);
            EnsureValidProfileNumber(scheme, profileNumber);

            var effectiveFileService = fileService ?? _defaultFileService;
            var effectiveSettingsService = ResolveSettingsService(fileService);

            var layoutLines = effectiveFileService.ReadAllLines(GetLayoutFilePath(location, profileNumber));
            var parseResult = new LayoutFileParser(device).Parse(layoutLines);
            var originalLayoutLines = LayoutFileSerializer.Serialize(parseResult.Layout, parseResult.InvalidLines);

            object? lighting = null;
            IReadOnlyList<string>? originalLightingLines = null;

            if (ProfileLightingCodec.HasSupportedLighting(device))
            {
                var lightingLines = effectiveFileService.ReadAllLines(GetLightingFilePath(location, profileNumber));
                lighting = ProfileLightingCodec.Parse(device, lightingLines);
                originalLightingLines = ProfileLightingCodec.Serialize(device, lighting);
            }

            var settings = effectiveSettingsService.LoadKeyboardSettings(location);

            return new ProfileSession(
                location,
                device,
                profileNumber,
                parseResult.Layout,
                parseResult.InvalidLines,
                lighting,
                settings,
                originalLayoutLines,
                originalLightingLines,
                effectiveFileService,
                effectiveSettingsService);
        }

        /// <summary>
        /// Replaces this session's layout or lighting with the content of an imported file
        /// (specs/10-apps-and-ui.md: "Import loads an external <c>.txt</c> (maximum 50 KB) and
        /// auto-detects whether it is layout or LED content"). Which of the two
        /// <paramref name="kind"/> names is decided by
        /// <see cref="Transfer.ImportClassifier"/> before the call; reading the file and
        /// enforcing its size are the caller's job.
        /// <para>
        /// It is deliberately the same operation as <see cref="Load"/> from a different source:
        /// the very same <see cref="LayoutFileParser"/>/<see cref="ProfileLightingCodec"/> path
        /// runs, so an imported file behaves exactly as it would have off the drive — a brand-new
        /// model built from scratch (04 §4.2's full-model-wipe-on-load), invalid lines tracked
        /// rather than dropped (04 §5), and nothing written anywhere. The baseline
        /// <see cref="IsDirty"/> compares against is the one captured at load, so imported
        /// content is <b>unsaved edit state</b> until the user saves.
        /// </para>
        /// Throws <see cref="ProfileReadOnlyException"/> for the Advantage 360's profile 0, whose
        /// import is disabled with its save (specs/02-devices.md, "Profiles 0-9").
        /// </summary>
        public ProfileImportResult Import(ImportedFileKind kind, IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown imported file kind.");
            }

            EnsureNotReadOnlyProfile(_location.Device.LayoutScheme, ProfileNumber);

            return kind == ImportedFileKind.Lighting ? ImportLighting(lines) : ImportLayout(lines);
        }

        /// <summary>Saves back to <see cref="ProfileNumber"/> (the save sequence of specs/03-vdrive-and-files.md §5.3).</summary>
        public ProfileSaveResult Save()
        {
            return ExecuteSave(ProfileNumber, setAsStartup: false);
        }

        /// <summary>
        /// Saves to a different numbered slot. When <paramref name="setAsStartup"/> is true, also
        /// points the device's startup settings (<c>startup_file</c>/<c>led_mode</c>, or the
        /// Advantage 360's <c>profile</c> key) at <paramref name="targetProfileNumber"/> in the
        /// same call (specs/07-lighting.md §1.2 "Save As to a profile number switches both the
        /// current layout file and the current led file at once").
        /// </summary>
        public ProfileSaveResult SaveAs(int targetProfileNumber, bool setAsStartup)
        {
            return ExecuteSave(targetProfileNumber, setAsStartup);
        }

        private ProfileImportResult ImportLayout(IReadOnlyList<string> lines)
        {
            var parseResult = new LayoutFileParser(Device).Parse(lines);

            Layout = parseResult.Layout;
            InvalidLines = parseResult.InvalidLines;

            return new ProfileImportResult
            {
                Kind = ImportedFileKind.Layout,
                InvalidLines = InvalidLines
            };
        }

        /// <summary>
        /// Replaces the led model only; the layout and its invalid lines belong to the other file
        /// and survive untouched. Defensive rather than reachable: the classifier answers
        /// <see cref="ImportedFileKind.Lighting"/> only for the three devices that pair a led file
        /// with each profile, which are exactly the ones this codec knows.
        /// </summary>
        private ProfileImportResult ImportLighting(IReadOnlyList<string> lines)
        {
            if (!ProfileLightingCodec.HasSupportedLighting(Device))
            {
                throw new NotSupportedException($"{Device} has no profile-orchestrated lighting file to import into.");
            }

            Lighting = ProfileLightingCodec.Parse(Device, lines);

            return new ProfileImportResult
            {
                Kind = ImportedFileKind.Lighting,
                InvalidLines = InvalidLines
            };
        }

        private ProfileSaveResult ExecuteSave(int targetProfileNumber, bool setAsStartup)
        {
            var scheme = _location.Device.LayoutScheme;

            EnsureNotReadOnlyProfile(scheme, targetProfileNumber);
            EnsureValidProfileNumber(scheme, targetProfileNumber);

            var violations = Layout.Validate();

            if (violations.Count > 0)
            {
                return new ProfileSaveResult
                {
                    Success = false,
                    Violations = violations,
                    PostSaveMessage = null
                };
            }

            var layoutLines = LayoutFileSerializer.Serialize(Layout, InvalidLines);
            _fileService.WriteAllLines(GetLayoutFilePath(_location, targetProfileNumber), layoutLines, allowCreate: true);

            if (Lighting is not null)
            {
                var lightingLines = ProfileLightingCodec.Serialize(Device, Lighting);
                _fileService.WriteAllLines(GetLightingFilePath(_location, targetProfileNumber), lightingLines, allowCreate: true);
            }

            if (setAsStartup)
            {
                SaveStartupSettings(targetProfileNumber);
            }

            // No eject: the write is the whole save. The volume is released only when the user
            // asks for it on the device card (docs/design/README.md, "Nothing ejects implicitly").
            var isStartupProfile = setAsStartup || _settings.StartupProfileNumber == targetProfileNumber;

            return new ProfileSaveResult
            {
                Success = true,
                Violations = violations,
                PostSaveMessage = ProfileSaveMessageCatalog.GetMessage(Device, targetProfileNumber, isStartupProfile)
            };
        }

        private void SaveStartupSettings(int targetProfileNumber)
        {
            var updatedSettings = StartupProfileSettings.ApplyStartupProfile(
                _location.Device.Settings,
                _settings,
                targetProfileNumber);

            _settingsService.SaveKeyboardSettings(_location, VersionFileInfo.Empty, updatedSettings);
        }

        /// <summary>
        /// The settings service a session reads and writes settings through. Sharing the *given*
        /// file service is the point: a session handed a substitute must not read its layout from
        /// that substitute and its settings from the real disk, so the default service — the only
        /// one built over the default file service — is used only when nothing was given.
        /// </summary>
        private static SettingsService ResolveSettingsService(IVDriveFileService? fileService)
        {
            return fileService is null ? _defaultSettingsService : new SettingsService(fileService);
        }

        private static void EnsureNotReadOnlyProfile(LayoutFileScheme scheme, int profileNumber)
        {
            if (IsReadOnlyProfile(scheme, profileNumber))
            {
                throw new ProfileReadOnlyException();
            }
        }

        private static bool IsReadOnlyProfile(LayoutFileScheme scheme, int profileNumber)
        {
            return profileNumber == 0 && scheme.HasReadOnlyFactoryProfile;
        }

        private static void EnsureValidProfileNumber(LayoutFileScheme scheme, int profileNumber)
        {
            if (profileNumber < scheme.FirstProfileNumber || profileNumber > scheme.LastProfileNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(profileNumber),
                    profileNumber,
                    $"Profile number must be between {scheme.FirstProfileNumber} and {scheme.LastProfileNumber}.");
            }
        }

        private static string GetLayoutFilePath(VDriveLocation location, int profileNumber)
        {
            return Path.Combine(location.LayoutsFolderPath!, ProfileFileNames.Layout(profileNumber));
        }

        private static string GetLightingFilePath(VDriveLocation location, int profileNumber)
        {
            // ProfileFileNames.Lighting is StartupProfileSettings.GetLedFileName — the same helper
            // that writes the paired led_mode value (spec 07 §1.2), so the file this session reads
            // and the file name the settings point at cannot drift apart.
            return Path.Combine(location.LightingFolderPath!, ProfileFileNames.Lighting(profileNumber));
        }
    }
}
