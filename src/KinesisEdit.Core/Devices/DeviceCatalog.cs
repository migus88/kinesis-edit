namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// The immutable catalog of every device known to the SmartSet ecosystem, encoding the
    /// master device table of specs/02-devices.md and the detection/path data of
    /// specs/03-vdrive-and-files.md §1-§4. Pure data — no I/O or filesystem probing.
    /// </summary>
    public static partial class DeviceCatalog
    {
        private const string DefaultVersionFolder = "firmware";
        private const string DefaultVersionFile = "version.txt";
        private const string DefaultSettingsFolder = "settings";
        private const string DefaultSettingsFile = "kbd_settings.txt";
        private const string ActiveFolder = "active";
        private const string Adv360SettingsFile = "settings.txt";

        /// <summary>All devices in master-table order, legacy app id 0 through 8.</summary>
        public static IReadOnlyList<DeviceDefinition> All { get; }

        private static readonly IReadOnlyDictionary<DeviceId, DeviceDefinition> _byId;
        private static readonly IReadOnlyDictionary<string, DeviceDefinition> _byVolumeLabel;

        static DeviceCatalog()
        {
            All = new[]
            {
                CreateSavantElite2(),
                CreateAdvantage2(),
                CreateFreestyleEdge(),
                CreateFreestylePro(),
                CreateFreestyleEdgeRgb(),
                CreateCrossfireKeypad(),
                CreateTko(),
                CreateAdvantage360(),
                CreateAdvantage360Professional()
            };

            var byId = new Dictionary<DeviceId, DeviceDefinition>();
            var byVolumeLabel = new Dictionary<string, DeviceDefinition>(StringComparer.Ordinal);

            foreach (var device in All)
            {
                byId.Add(device.Id, device);

                foreach (var volumeLabel in device.VolumeLabels)
                {
                    byVolumeLabel.Add(volumeLabel.ToUpperInvariant(), device);
                }
            }

            _byId = byId;
            _byVolumeLabel = byVolumeLabel;
        }

        /// <summary>Returns the definition for <paramref name="deviceId"/>; throws for ids not in the catalog.</summary>
        public static DeviceDefinition GetById(DeviceId deviceId)
        {
            if (_byId.TryGetValue(deviceId, out var device))
            {
                return device;
            }

            throw new ArgumentOutOfRangeException(nameof(deviceId), deviceId, "No device definition exists for this id.");
        }

        /// <summary>
        /// Resolves a v-Drive volume label to its device using the exact-match rule of
        /// specs/03-vdrive-and-files.md §2: the label is uppercased and trimmed, then compared
        /// against each device's candidates. Returns null when no device matches.
        /// </summary>
        public static DeviceDefinition? FindByVolumeLabel(string? volumeLabel)
        {
            if (string.IsNullOrWhiteSpace(volumeLabel))
            {
                return null;
            }

            var normalizedLabel = volumeLabel.Trim().ToUpperInvariant();

            return _byVolumeLabel.TryGetValue(normalizedLabel, out var device) ? device : null;
        }

        private static DeviceDefinition CreateSavantElite2()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.SavantElite2,
                DisplayName = "Savant Elite 2",
                LegacyAppId = 0,
                ServingApp = ServingApp.SmartSetMasterOffice,
                VolumeLabels = ["SE2", "KINESIS FP"],
                MarkerFolder = ActiveFolder,
                MarkerFile = DefaultVersionFile,
                VersionFolder = ActiveFolder,
                VersionFile = DefaultVersionFile,
                SettingsFolder = DefaultSettingsFolder,
                SettingsFile = DefaultSettingsFile,
                LayerCount = null,
                LayoutScheme = new LayoutFileScheme
                {
                    Kind = LayoutSchemeKind.PedalFile,
                    LayoutFolder = ActiveFolder,
                    FixedLayoutFileName = "pedals.txt"
                },
                Macros = CreateSavantElite2Macros(),
                TapAndHold = TapAndHoldCapability.None,
                Lighting = LightingCapability.None,
                Settings = SettingsCapability.None,
                HardwareNotes = "3 pedals + jack",
                IsProgrammable = true
            };
        }

        private static DeviceDefinition CreateAdvantage2()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.Advantage2,
                DisplayName = "Advantage2",
                LegacyAppId = 1,
                ServingApp = ServingApp.SmartSetMasterOffice,
                VolumeLabels = ["ADVANTAGE2", "KINESIS KB", "ADV2"],
                MarkerFolder = ActiveFolder,
                MarkerFile = DefaultVersionFile,
                VersionFolder = ActiveFolder,
                VersionFile = DefaultVersionFile,
                SettingsFolder = ActiveFolder,
                SettingsFile = "state.txt",
                LayerCount = 2,
                LayoutScheme = new LayoutFileScheme
                {
                    Kind = LayoutSchemeKind.QwertyDvorakPositions,
                    LayoutFolder = ActiveFolder
                },
                Macros = CreateAdvantage2Macros(),
                TapAndHold = CreateTapAndHold(new FirmwareVersion(1, 0, 516)),
                Lighting = LightingCapability.None,
                Settings = new SettingsCapability
                {
                    WritesMacroSpeed = true,
                    MacroSpeedMinimum = 0,
                    StatusSetting = StatusSettingKind.StatusPlaySpeedKey,
                    VDriveSetting = VDriveSettingKind.OpenOnStartup,
                    WritesKeyClickTone = true,
                    WritesToggleTone = true
                },
                HardwareNotes = "full keyset incl. thumb clusters; 3 foot-pedal inputs (left/middle/right pedal)",
                VDriveShortcutHint = "Program + F1",
                IsProgrammable = true
            };
        }

        private static DeviceDefinition CreateFreestyleEdge()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.FreestyleEdge,
                DisplayName = "Freestyle Edge",
                LegacyAppId = 2,
                ServingApp = ServingApp.StandaloneOnly,
                VolumeLabels = ["FS EDGE"],
                MarkerFolder = DefaultVersionFolder,
                MarkerFile = DefaultVersionFile,
                VersionFolder = DefaultVersionFolder,
                VersionFile = DefaultVersionFile,
                SettingsFolder = DefaultSettingsFolder,
                SettingsFile = DefaultSettingsFile,
                LayerCount = 2,
                LayoutScheme = CreateNumberedProfileScheme(hasLighting: true),
                Macros = CreateFreestyleMacros(),
                TapAndHold = CreateTapAndHold(new FirmwareVersion(1, 0, 480)),
                Lighting = new LightingCapability
                {
                    Kind = LightingKind.BlueBacklight
                },
                Settings = new SettingsCapability
                {
                    LedMode = LedModeKind.ModeString,
                    WritesMacroSpeed = true,
                    MacroSpeedMinimum = 0,
                    StatusSetting = StatusSettingKind.StatusPlaySpeedKey,
                    VDriveSetting = VDriveSettingKind.AutoManual,
                    WritesGameMode = true
                },
                HardwareNotes = "split keyboard, LED backlight, Game Mode switch",
                VDriveShortcutHint = "SmartSet + F8",
                IsProgrammable = true
            };
        }

        private static DeviceDefinition CreateFreestylePro()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.FreestylePro,
                DisplayName = "Freestyle Pro",
                LegacyAppId = 3,
                ServingApp = ServingApp.StandaloneOnly,
                VolumeLabels = ["FS PRO"],
                MarkerFolder = DefaultVersionFolder,
                MarkerFile = DefaultVersionFile,
                VersionFolder = DefaultVersionFolder,
                VersionFile = DefaultVersionFile,
                SettingsFolder = DefaultSettingsFolder,
                SettingsFile = DefaultSettingsFile,
                LayerCount = 2,
                LayoutScheme = CreateNumberedProfileScheme(hasLighting: false),
                Macros = CreateFreestyleMacros(),
                TapAndHold = CreateTapAndHold(new FirmwareVersion(1, 0, 480)),
                Lighting = LightingCapability.None,
                Settings = new SettingsCapability
                {
                    WritesMacroSpeed = true,
                    MacroSpeedMinimum = 0,
                    StatusSetting = StatusSettingKind.StatusPlaySpeedKey,
                    VDriveSetting = VDriveSettingKind.AutoManual
                },
                HardwareNotes = "split keyboard; no game mode",
                VDriveShortcutHint = "SmartSet + F8",
                IsProgrammable = true
            };
        }

        private static DeviceDefinition CreateFreestyleEdgeRgb()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.FreestyleEdgeRgb,
                DisplayName = "Freestyle Edge RGB",
                LegacyAppId = 4,
                ServingApp = ServingApp.SmartSetMasterGaming,
                VolumeLabels = ["FS EDGE RGB"],
                MarkerFolder = DefaultVersionFolder,
                MarkerFile = DefaultVersionFile,
                VersionFolder = DefaultVersionFolder,
                VersionFile = DefaultVersionFile,
                SettingsFolder = DefaultSettingsFolder,
                SettingsFile = DefaultSettingsFile,
                LayerCount = 2,
                LayoutScheme = CreateNumberedProfileScheme(hasLighting: true),
                Macros = CreateRgbFamilyMacros(),
                TapAndHold = CreateTapAndHold(new FirmwareVersion(1, 0, 1)),
                Lighting = new LightingCapability
                {
                    Kind = LightingKind.PerKeyRgb
                },
                Settings = CreatePerKeyRgbSettingsCapability(),
                HardwareNotes = "split gaming keyboard",
                VDriveShortcutHint = "SmartSet + F8",
                IsProgrammable = true
            };
        }

        private static DeviceDefinition CreateCrossfireKeypad()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.CrossfireKeypad,
                DisplayName = "CROSSFIRE Keypad",
                LegacyAppId = 5,
                ServingApp = ServingApp.None,
                VolumeLabels = ["CROSSFIRE KEYPAD"],
                Macros = MacroCapability.None,
                TapAndHold = TapAndHoldCapability.None,
                Settings = SettingsCapability.None,
                HardwareNotes = "never shipped",
                IsProgrammable = false,
                IsFutureDevice = true
            };
        }

        private static DeviceDefinition CreateTko()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.Tko,
                DisplayName = "TKO",
                LegacyAppId = 6,
                ServingApp = ServingApp.SmartSetMasterGaming,
                VolumeLabels = ["TKO"],
                MarkerFolder = DefaultVersionFolder,
                MarkerFile = DefaultVersionFile,
                VersionFolder = DefaultVersionFolder,
                VersionFile = DefaultVersionFile,
                SettingsFolder = DefaultSettingsFolder,
                SettingsFile = DefaultSettingsFile,
                LayerCount = 2,
                LayoutScheme = CreateNumberedProfileScheme(hasLighting: true),
                Macros = CreateRgbFamilyMacros(),
                TapAndHold = CreateTapAndHold(),
                Lighting = new LightingCapability
                {
                    Kind = LightingKind.PerKeyRgb,
                    EdgeLedLeftCount = 9,
                    EdgeLedBottomCount = 15,
                    EdgeLedRightCount = 9
                },
                Settings = CreatePerKeyRgbSettingsCapability(),
                HardwareNotes = "60% tenkeyless gaming board with tripartite space bar (left/middle/right space)",
                VDriveShortcutHint = "SmartSet + Right Shift + V",
                IsProgrammable = true
            };
        }

        private static DeviceDefinition CreateAdvantage360()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.Advantage360,
                DisplayName = "Advantage 360",
                LegacyAppId = 7,
                ServingApp = ServingApp.SmartSetMasterOffice,
                VolumeLabels = ["ADV360"],
                MarkerFolder = DefaultSettingsFolder,
                MarkerFile = Adv360SettingsFile,
                VersionFolder = DefaultSettingsFolder,
                VersionFile = Adv360SettingsFile,
                SettingsFolder = DefaultSettingsFolder,
                SettingsFile = Adv360SettingsFile,
                LayerCount = 5,
                LayoutScheme = CreateNumberedProfileScheme(hasLighting: true, hasReadOnlyFactoryProfile: true),
                Macros = CreateAdvantage360Macros(),
                TapAndHold = CreateTapAndHold(defaultDelayMilliseconds: Advantage360TapAndHoldDefaultDelayMilliseconds),
                SupportsMultiModifiers = true,
                Lighting = new LightingCapability
                {
                    Kind = LightingKind.IndicatorLeds,
                    IndicatorLedCount = 6
                },
                Settings = new SettingsCapability
                {
                    StartupSetting = StartupSettingKind.ProfileNumber,
                    StatusSetting = StatusSettingKind.StatusKey,
                    WritesProgramLock = true
                },
                HardwareNotes = "split ergonomic contoured keyboard; optional foot pedal display; Bluetooth",
                VDriveShortcutHint = "SmartSet + v-Drive",
                IsProgrammable = true
            };
        }

        private static DeviceDefinition CreateAdvantage360Professional()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.Advantage360Professional,
                DisplayName = "Advantage 360 Professional",
                LegacyAppId = 8,
                ServingApp = ServingApp.SmartSetMasterOffice,
                Macros = MacroCapability.None,
                TapAndHold = TapAndHoldCapability.None,
                Settings = SettingsCapability.None,
                HardwareNotes = "configured via ZMK web GUI",
                ConfigurationUrl = "https://kinesiscorporation.github.io/Adv360-Pro-GUI/",
                SupportUrl = "https://kinesis-ergo.com/support/advantage360-pro",
                IsProgrammable = false
            };
        }

        private static LayoutFileScheme CreateNumberedProfileScheme(bool hasLighting, bool hasReadOnlyFactoryProfile = false)
        {
            return new LayoutFileScheme
            {
                Kind = LayoutSchemeKind.NumberedProfiles,
                LayoutFolder = "layouts",
                LightingFolder = hasLighting ? "lighting" : null,
                FirstProfileNumber = 1,
                LastProfileNumber = 9,
                HasReadOnlyFactoryProfile = hasReadOnlyFactoryProfile
            };
        }
    }
}
