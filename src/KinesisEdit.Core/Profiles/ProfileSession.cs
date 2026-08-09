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
        /// re-serialize to lines different from <b>the baseline</b> — the lines captured right after
        /// <see cref="Load"/>, replaced by the lines a successful <see cref="Save"/> wrote. Reuses
        /// <see cref="LayoutFileSerializer"/>/<see cref="ProfileLightingCodec"/> rather than a
        /// bespoke model comparison, so dirty tracking can never drift from what a save would
        /// actually write.
        /// <para>
        /// <b>The baseline is always what is on the drive for this profile</b>, which is what makes
        /// "a profile with no changes is never rewritten" answerable at all: a caller that saves and
        /// then asks again gets false, and a second press of Save writes nothing. It moves only for
        /// a save that targeted <see cref="ProfileNumber"/> itself — a <see cref="SaveAs"/> to
        /// another slot leaves this profile's file untouched, so its baseline must not move — and
        /// never for an <see cref="Import"/>, whose whole point is that the imported content is
        /// unsaved edit state.
        /// </para>
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
        private readonly IReadOnlyList<LayoutInvalidLine> _originalInvalidLines;
        private readonly IVDriveFileService _fileService;
        private readonly SettingsService _settingsService;

        // The baseline: what is on the drive for this profile. Set at Load and moved by a save that
        // wrote this profile's own files (see IsDirty and ExecuteSave). Not readonly for that one
        // reason, and written from nowhere else — IsDirty stays a pull, and no caller can reach in.
        private IReadOnlyList<string> _originalLayoutLines;
        private IReadOnlyList<string>? _originalLightingLines;

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
            _originalInvalidLines = invalidLines;
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
        /// rather than dropped (04 §5), and nothing written anywhere. An import <b>never moves the
        /// baseline</b> <see cref="IsDirty"/> compares against — only a save does — so imported
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

        /// <summary>
        /// Throws this session's <b>layout</b> edits away and rebuilds <see cref="Layout"/> from the
        /// baseline — the lines read at <see cref="Load"/>, or the ones the last successful
        /// <see cref="Save"/> wrote. <see cref="Lighting"/> is not touched —
        /// that is the whole point of the pair: the editor's <c>Discard changes</c> is scoped to the
        /// page the user is on, and re-reading the drive would be all-or-nothing and take the other
        /// page's edits with it (docs/app/keyboard-editor.md).
        /// <para>
        /// It is the same shape as <see cref="Import"/> — a re-parse that replaces
        /// <see cref="Layout"/> and <see cref="InvalidLines"/> in place, so a caller must hold the
        /// <i>session</i> and re-read both afterwards — from a different source: the baseline
        /// <see cref="IsDirty"/> already compares against. Two things follow. It <b>cannot diverge</b>
        /// from that baseline, because it *is* it: after this call the layout half of
        /// <see cref="IsDirty"/> is false by construction. And it needs <b>no drive</b>: the lines
        /// are in memory, so a revert works over a volume that has since been unmounted.
        /// </para>
        /// <para>
        /// <b>It reverts to what is on the drive</b>, which is the load for a profile that has not
        /// been saved and the last <see cref="Save"/> for one that has — the baseline moves with the
        /// write (see <see cref="IsDirty"/>), so a discard can never take back work the user already
        /// committed.
        /// </para>
        /// <para>
        /// <see cref="InvalidLines"/> goes back to the list captured at load rather than to whatever
        /// re-parsing the baseline produces. Every baseline — the load's and every save's — is
        /// serialized with that same list, so the kept lines are in it and the dropped ones are not;
        /// restoring the captured list is what makes the round trip exact and keeps the "some lines
        /// could not be applied" report saying what it said when the profile was opened. It is the
        /// one part of the load that a save does not move, and it does not need to.
        /// </para>
        /// </summary>
        public void RevertLayout()
        {
            var parseResult = new LayoutFileParser(Device).Parse(_originalLayoutLines);

            Layout = parseResult.Layout;
            InvalidLines = _originalInvalidLines;
        }

        /// <summary>
        /// The lighting twin of <see cref="RevertLayout"/>: rebuilds <see cref="Lighting"/> from the
        /// baseline's led lines — what is on the drive for this profile — and leaves
        /// <see cref="Layout"/> exactly as it is.
        /// <para>
        /// A device with no profile-orchestrated led file (FS Edge/Pro — see
        /// <see cref="ProfileLightingCodec"/>) has nothing to revert and nothing that could be
        /// dirty, so this is a no-op there rather than a throw: unlike an import, a revert's
        /// postcondition — "the lighting is what is on the drive" — already holds.
        /// </para>
        /// </summary>
        public void RevertLighting()
        {
            if (_originalLightingLines is null)
            {
                return;
            }

            Lighting = ProfileLightingCodec.Parse(Device, _originalLightingLines);
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

            IReadOnlyList<string>? lightingLines = null;

            if (Lighting is not null)
            {
                lightingLines = ProfileLightingCodec.Serialize(Device, Lighting);
                _fileService.WriteAllLines(GetLightingFilePath(_location, targetProfileNumber), lightingLines, allowCreate: true);
            }

            // The lines just written ARE what is on the drive for this profile, so they become the
            // baseline IsDirty compares against. This module still writes whatever it is asked to;
            // what moves is the ANSWER a caller gets afterwards, and it is the answer a caller that
            // decides which profiles to write needs — with the baseline frozen at Load, every
            // session ever saved reports itself dirty for ever and the editor rewrites the lot on
            // the next press of Save (docs/app/keyboard-editor.md, "Save").
            //
            // Only when the write went to this session's own slot. SaveAs(other, ...) copies the
            // model somewhere else and leaves layout<ProfileNumber>.txt exactly as it was, so this
            // session's unsaved work is still unsaved and its baseline must not move.
            if (targetProfileNumber == ProfileNumber)
            {
                _originalLayoutLines = layoutLines;

                if (lightingLines is not null)
                {
                    _originalLightingLines = lightingLines;
                }
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
