using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// The Advantage2 settings-editing gate of specs/09-firmware.md §1.1 and
    /// specs/08-settings.md §2/§5.4: keyboard settings may only be written to an Advantage2
    /// whose version file carries the <c>4MB</c> marker — 2MB boards keep the settings UI
    /// disabled. Every other device is ungated. Consumes an already-parsed
    /// <see cref="VersionFileInfo"/>; version files are never re-parsed here.
    /// </summary>
    public static class KeyboardSettingsGate
    {
        /// <summary>
        /// Whether keyboard settings may be written for <paramref name="deviceId"/>: false only
        /// for the Advantage2 without <see cref="VersionFileInfo.HasFourMegabyteMarker"/>. The
        /// future settings UI disables its controls on false (spec 08 §5.4).
        /// </summary>
        public static bool CanEditKeyboardSettings(DeviceId deviceId, VersionFileInfo versionFileInfo)
        {
            ArgumentNullException.ThrowIfNull(versionFileInfo);

            return deviceId != DeviceId.Advantage2 || versionFileInfo.HasFourMegabyteMarker;
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> when
        /// <see cref="CanEditKeyboardSettings"/> is false — the write-refusal side of the gate.
        /// </summary>
        public static void EnsureCanEditKeyboardSettings(DeviceId deviceId, VersionFileInfo versionFileInfo)
        {
            if (!CanEditKeyboardSettings(deviceId, versionFileInfo))
            {
                throw new InvalidOperationException(
                    "Advantage2 keyboard settings cannot be written: the version file carries no 4MB marker, "
                    + "so the board is treated as the 2MB variant and settings editing is disabled "
                    + "(specs/09-firmware.md §1.1, specs/08-settings.md §2).");
            }
        }
    }
}
