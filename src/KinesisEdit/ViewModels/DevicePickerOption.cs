using KinesisEdit.Core.Devices;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the empty state's device picker (docs/design/mockups.md §1d): a silhouette, the
    /// board's name, and — on one row — the "default" tag.
    /// <para>
    /// Immutable, and deliberately not a <see cref="ViewModelBase"/>. The picker is a
    /// <c>ListBox</c>, so "which row is selected" is the list's own <c>:selected</c> state and not
    /// a flag every row has to be told about.
    /// </para>
    /// </summary>
    public sealed class DevicePickerOption
    {
        private const string PedalQualifier = " (pedal)";

        /// <summary>The catalog entry this row stands for.</summary>
        public DeviceDefinition Device { get; }

        /// <summary>The device's id — what the silhouette converter reads.</summary>
        public DeviceId Id => Device.Id;

        /// <summary>
        /// The row's caption. Mockup §1d writes the pedal as "Savant Elite 2 (pedal)"; the
        /// qualifier is derived from the form factor rather than spelled per device, so the row is
        /// right for any pedal the catalog ever gains.
        /// </summary>
        public string Caption { get; }

        /// <summary>Whether this is the board the screen starts on, tagged "default" in the mockup.</summary>
        public bool IsDefault { get; }

        /// <summary>Creates a row over <paramref name="device"/>.</summary>
        public DevicePickerOption(DeviceDefinition device, bool isDefault)
        {
            ArgumentNullException.ThrowIfNull(device);

            Device = device;
            IsDefault = isDefault;
            Caption = ConnectionSteps.IsPedal(device)
                ? device.DisplayName + PedalQualifier
                : device.DisplayName;
        }
    }
}
