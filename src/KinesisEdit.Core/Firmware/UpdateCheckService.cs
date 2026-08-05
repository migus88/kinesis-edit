using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The comparison engine of the "Check for Updates" dialog (specs/09-firmware.md §3): turns
    /// local versions plus a fetched <see cref="VersionManifest"/> into the ordered rows the
    /// dialog renders. Pure and stateless — no HTTP, no filesystem, no captions. The app layer
    /// fetches (see <see cref="IVersionManifestClient"/>), calls one of these builders, and maps
    /// <see cref="UpdateRowState"/> to wording.
    /// </summary>
    public static class UpdateCheckService
    {
        /// <summary>
        /// The rows of the update dialog for a device, in display order: keyboard, lighting
        /// (omitted for the Advantage 360, §3 step 4), app. Empty for devices that offer no
        /// update dialog. Follows §3: a keyboard firmware that "comes back empty" short-circuits
        /// to <see cref="BuildUnreadableRows"/> (step 1, see
        /// <see cref="HasReadableKeyboardVersion"/>); otherwise each row compares its local
        /// version against the manifest value of its key (steps 4-5) — local smaller than remote
        /// is <see cref="UpdateRowState.UpdateAvailable"/>, equal or newer is
        /// <see cref="UpdateRowState.UpToDate"/>, an empty remote value is
        /// <see cref="UpdateRowState.RemoteMissing"/>. Only emptiness means "no version": on both
        /// sides a present but unparseable value compares as 0.0.0, because §1.1 parses a
        /// non-numeric token as 0.
        /// </summary>
        public static IReadOnlyList<UpdateCheckRow> BuildRows(UpdateCheckRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.LocalVersions);
            ArgumentNullException.ThrowIfNull(request.Manifest);

            if (!UpdateCheckEligibility.IsSupported(request.Device))
            {
                return [];
            }

            if (!HasReadableKeyboardVersion(request.LocalVersions))
            {
                return BuildUnreadableRows(request.Device);
            }

            var rows = new List<UpdateCheckRow>();

            foreach (var kind in GetRowKinds(request.Device))
            {
                rows.Add(BuildRow(request, kind));
            }

            return rows;
        }

        /// <summary>
        /// Whether the device reported a keyboard firmware version at all — the condition of the
        /// §3 step 1 short circuit, exposed so callers can skip the endpoint request without
        /// restating the rule. False only when the version file carried no keyboard value (absent
        /// line, or a line with nothing after the separator); a present but unparseable value is
        /// readable and compares as 0.0.0 per §1.1.
        /// </summary>
        public static bool HasReadableKeyboardVersion(VersionFileInfo localVersions)
        {
            ArgumentNullException.ThrowIfNull(localVersions);

            return !string.IsNullOrWhiteSpace(localVersions.KeyboardFirmwareText);
        }

        /// <summary>The dialog's initial rows, before the check produces any result (§3: every button starts as "checking").</summary>
        public static IReadOnlyList<UpdateCheckRow> BuildCheckingRows(DeviceId deviceId)
        {
            return BuildUniformRows(deviceId, UpdateRowState.Checking);
        }

        /// <summary>
        /// The rows for a device whose local keyboard firmware came back empty
        /// (§3 step 1): every row — the app row included — reports
        /// <see cref="UpdateRowState.LocalUnreadable"/> and nothing is compared.
        /// </summary>
        public static IReadOnlyList<UpdateCheckRow> BuildUnreadableRows(DeviceId deviceId)
        {
            return BuildUniformRows(deviceId, UpdateRowState.LocalUnreadable);
        }

        /// <summary>
        /// The rows for a failed endpoint request (§3 step 6): any exception from
        /// <see cref="IVersionManifestClient.FetchAsync"/> — no internet, HTTP error, bad JSON —
        /// puts every row into <see cref="UpdateRowState.ConnectionError"/>.
        /// </summary>
        public static IReadOnlyList<UpdateCheckRow> BuildConnectionErrorRows(DeviceId deviceId)
        {
            return BuildUniformRows(deviceId, UpdateRowState.ConnectionError);
        }

        private static IReadOnlyList<UpdateCheckRow> BuildUniformRows(DeviceId deviceId, UpdateRowState state)
        {
            var rows = new List<UpdateCheckRow>();

            foreach (var kind in GetRowKinds(deviceId))
            {
                rows.Add(new UpdateCheckRow
                {
                    Kind = kind,
                    State = state,
                    TargetUrl = FindTargetUrl(deviceId, kind)
                });
            }

            return rows;
        }

        private static IReadOnlyList<UpdateRowKind> GetRowKinds(DeviceId deviceId)
        {
            if (!UpdateCheckEligibility.IsSupported(deviceId))
            {
                return [];
            }

            if (!UpdateCheckEligibility.HasLightingRow(deviceId))
            {
                return [UpdateRowKind.Keyboard, UpdateRowKind.App];
            }

            return [UpdateRowKind.Keyboard, UpdateRowKind.Lighting, UpdateRowKind.App];
        }

        private static UpdateCheckRow BuildRow(UpdateCheckRequest request, UpdateRowKind kind)
        {
            var localVersionText = ResolveLocalVersionText(request, kind);
            var manifestKey = FindManifestKey(request.Device, kind, request.Platform);
            var remoteVersionText = request.Manifest.GetValue(manifestKey);

            var hasLocalVersion = TryResolveVersion(localVersionText, out var localVersion);
            var hasRemoteVersion = TryResolveVersion(remoteVersionText, out var remoteVersion);

            return new UpdateCheckRow
            {
                Kind = kind,
                State = ResolveState(hasLocalVersion, localVersion, hasRemoteVersion, remoteVersion),
                LocalVersion = hasLocalVersion ? localVersion : null,
                LocalVersionText = localVersionText,
                RemoteVersion = hasRemoteVersion ? remoteVersion : null,
                RemoteVersionText = remoteVersionText ?? string.Empty,
                TargetUrl = FindTargetUrl(request.Device, kind)
            };
        }

        private static UpdateRowState ResolveState(
            bool hasLocalVersion,
            FirmwareVersion localVersion,
            bool hasRemoteVersion,
            FirmwareVersion remoteVersion)
        {
            if (!hasLocalVersion)
            {
                return UpdateRowState.LocalUnreadable;
            }

            if (!hasRemoteVersion)
            {
                return UpdateRowState.RemoteMissing;
            }

            return localVersion < remoteVersion ? UpdateRowState.UpdateAvailable : UpdateRowState.UpToDate;
        }

        /// <summary>
        /// The local value a row compares, as the device or the build resource worded it: the
        /// keyboard/LED firmware text of the version file (§3 step 1) or the app's own version
        /// text (§3 step 2). The raw text — not the pre-parsed version — decides whether the row
        /// has a local value at all, because §3 only recognises an empty value as "not read".
        /// </summary>
        private static string ResolveLocalVersionText(UpdateCheckRequest request, UpdateRowKind kind)
        {
            var text = kind switch
            {
                UpdateRowKind.Keyboard => request.LocalVersions.KeyboardFirmwareText,
                UpdateRowKind.Lighting => request.LocalVersions.LedFirmwareText,
                UpdateRowKind.App => request.AppVersionText,
                _ => null
            };

            return text?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// The version a value compares as. Returns false only for a value that is absent, empty,
        /// or whitespace — the sole "no version" case §3 recognises, on the local side (step 1)
        /// as on the remote one (step 5). A present but unparseable value is usable and yields
        /// 0.0.0, because §1.1 parses a non-numeric token as 0.
        /// </summary>
        private static bool TryResolveVersion(string? text, out FirmwareVersion version)
        {
            version = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (FirmwareVersion.TryParse(text, out var parsedVersion))
            {
                version = parsedVersion;
            }

            return true;
        }

        private static string? FindManifestKey(DeviceId deviceId, UpdateRowKind kind, UpdateCheckPlatform platform)
        {
            return kind switch
            {
                UpdateRowKind.Keyboard => UpdateCheckKeys.FindKeyboardKey(deviceId),
                UpdateRowKind.Lighting => UpdateCheckKeys.FindLightingKey(deviceId),
                UpdateRowKind.App => UpdateCheckKeys.FindAppKey(deviceId, platform),
                _ => null
            };
        }

        private static string? FindTargetUrl(DeviceId deviceId, UpdateRowKind kind)
        {
            return kind switch
            {
                UpdateRowKind.Keyboard => UpdateCheckUrls.FindFirmwareUrl(deviceId),
                UpdateRowKind.Lighting => UpdateCheckUrls.FindFirmwareUrl(deviceId),
                UpdateRowKind.App => UpdateCheckUrls.FindAppUrl(deviceId),
                _ => null
            };
        }
    }
}
