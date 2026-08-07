using KinesisEdit.Core.Devices;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Builds the per-device instruction list of the dashboard's empty state (docs/design/mockups.md
    /// §1d: "Get an Advantage2 into a detectable state — 3 steps").
    /// <para>
    /// <b>Everything device-specific comes out of the catalog</b>, never out of prose written per
    /// board: the onboard shortcut is <see cref="DeviceDefinition.VDriveShortcutHint"/>, the drive
    /// the user is told to look for is the primary <see cref="DeviceDefinition.VolumeLabels"/>
    /// entry, and whether the thing being plugged in is a keyboard or a pedal is
    /// <see cref="DeviceDefinition.FormFactor"/>. A device added to the catalog therefore gets
    /// correct steps without this file being touched.
    /// </para>
    /// <para>
    /// This replaces the single paragraph of specs/11-feature-dialogs.md §11.8 that the panel used
    /// to render. The paragraph's own wording is gone — §1d draws numbered steps — but the two
    /// deviations that paragraph forced are unchanged and are recorded on
    /// <see cref="AppendMountSteps"/>.
    /// </para>
    /// </summary>
    public static class ConnectionSteps
    {
        /// <summary>Step 1 for a keyboard (docs/design/mockups.md §1d, verbatim).</summary>
        public const string PlugInKeyboardText = "Plug the keyboard into this computer with its USB cable.";

        /// <summary>Step 1 for the Savant Elite 2, which is not a keyboard.</summary>
        public const string PlugInPedalText = "Plug the pedal into this computer with its USB cable.";

        /// <summary>The Power User Mode shortcut of specs/11-feature-dialogs.md §11.8.</summary>
        public const string PowerUserShortcut = "Program + Shift + Esc";

        /// <summary>The Savant Elite 2's mount step — a slide switch, not a key combination.</summary>
        public const string PedalSlideSwitchText =
            "Slide the switch on the pedal from play mode to programming mode. "
            + "If no drive appears, unplug the pedal and plug it back in.";

        private const string PowerUserLead = "If Power User Mode is off, turn it on with ";
        private const string PowerUserTrail = ".";
        private const string MountLead = "On the keyboard, press ";
        private const string MountTrail = " to mount the v-Drive.";
        private const string UnknownShortcutText =
            "On the keyboard, use its onboard v-Drive shortcut to mount the v-Drive.";
        private const string DriveLead = "A drive named ";
        private const string DriveTrail =
            " appears. Press Scan now and your device card takes over this screen.";
        private const string UnknownDriveText =
            "A drive appears. Press Scan now and your device card takes over this screen.";

        /// <summary>
        /// Whether <paramref name="device"/> is a foot pedal rather than a keyboard. One predicate
        /// serves the plug-in noun, the mount mechanism and the panel's own title, so the three can
        /// never disagree about what the user is holding.
        /// </summary>
        public static bool IsPedal(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            return device.FormFactor == DeviceFormFactor.ThreeButtonFootPedal;
        }

        /// <summary>The numbered steps that get <paramref name="device"/> into a detectable state.</summary>
        public static IReadOnlyList<ConnectionStep> Create(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var steps = new List<ConnectionStep>(4);
            var isPedal = IsPedal(device);

            steps.Add(new ConnectionStep(1, isPedal ? PlugInPedalText : PlugInKeyboardText));

            AppendPowerUserStep(steps, device);
            AppendMountSteps(steps, device, isPedal);
            AppendDriveStep(steps, device);

            return steps;
        }

        private static void AppendPowerUserStep(List<ConnectionStep> steps, DeviceDefinition device)
        {
            // Power User Mode reaches the Advantage2 and nothing else. Its only mention anywhere in
            // the specs is specs/11-feature-dialogs.md §11.8's first row, which lumps four devices
            // together — and that row is demonstrably wrong about two of them:
            //
            //   * the Freestyle Edge and Pro have no such mode at all; they open their v-Drive with
            //     "SmartSet + F8".
            //   * the Savant Elite 2 is a three-button foot pedal with no keys, so it can no more
            //     be sent "Program + Shift + Esc" than the "Program + F1" the same row prescribes —
            //     which AppendMountSteps already rejects for exactly this reason. specs/12 never
            //     mentions Power User Mode; §1 gives the pedal's real mechanism, a slide switch.
            //
            // Telling any of the three to press keys they do not have is the one failure this whole
            // panel exists to prevent, so the preamble stays on the Advantage2. A departure from the
            // row's grouping, not from its wording; NoDeviceViewModelTests pins it.
            if (device.Id != DeviceId.Advantage2)
            {
                return;
            }

            steps.Add(new ConnectionStep(steps.Count + 1, PowerUserLead, PowerUserShortcut, PowerUserTrail));
        }

        private static void AppendMountSteps(List<ConnectionStep> steps, DeviceDefinition device, bool isPedal)
        {
            // The Savant Elite 2 has no keyboard, so it has no onboard shortcut either. Its catalog
            // hint and specs/12-savant-elite.md §3 both quote "Program + F1", which is the same
            // legacy copy-paste artifact §11.8 carries; §1 of that spec documents the real
            // mechanism — a physical slide switch between "play mode" and programming mode — and
            // §3's own remedy for a drive that does not appear is to replug the pedal. Both are in
            // the sentence below, and the shortcut is not.
            if (isPedal)
            {
                steps.Add(new ConnectionStep(steps.Count + 1, PedalSlideSwitchText));

                return;
            }

            // The shortcut is templated from the catalog rather than quoted from §11.8, whose
            // second row gives the TKO "+ F8" alongside the RGB. The TKO's real shortcut is
            // "SmartSet + Right Shift + V" (specs/03-vdrive-and-files.md §1), and the catalog is
            // where that lives.
            var shortcut = device.VDriveShortcutHint;

            steps.Add(shortcut is null
                ? new ConnectionStep(steps.Count + 1, UnknownShortcutText)
                : new ConnectionStep(steps.Count + 1, MountLead, shortcut, MountTrail));
        }

        private static void AppendDriveStep(List<ConnectionStep> steps, DeviceDefinition device)
        {
            // The primary candidate: the scanner matches on any of them, but the drive the firmware
            // actually mounts is the first, and naming three would read as a puzzle rather than an
            // instruction.
            var label = device.VolumeLabels.Count > 0 ? device.VolumeLabels[0] : null;

            steps.Add(label is null
                ? new ConnectionStep(steps.Count + 1, UnknownDriveText)
                : new ConnectionStep(steps.Count + 1, DriveLead, label, DriveTrail));
        }
    }
}
