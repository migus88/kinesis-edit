namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// What a board's own configuration key does <b>on the board</b> — the "What it does on the
    /// board" section of the key inspector's locked-key panel (mockup <c>2h</c>).
    ///
    /// <para><b>Why this is data and not three strings in a view.</b> The facts differ per device:
    /// the Freestyle family mounts its v-Drive with <c>SmartSet + F8</c>, the TKO with
    /// <c>SmartSet + Right Shift + V</c>, the Advantage2 with <c>Program + F1</c>. A panel that
    /// hard-coded one board's answer would teach the wrong shortcut on the other four, and the
    /// panel is shown on whichever board the user opened.</para>
    ///
    /// <para><b>There is no single spec section to cite, and this type does not pretend there
    /// is.</b> Each fact carries its own source, and the three sources are different in kind:
    /// <list type="bullet">
    /// <item>The <b>v-Drive</b> shortcut is spec text — specs/03-vdrive-and-files.md §1's per-device
    /// table, verbatim.</item>
    /// <item>The <b>profile/layout</b> shortcut is spec text — specs/02-devices.md's per-device
    /// bullets (<c>SmartSet + &lt;n&gt;</c>, <c>Program + &lt;n&gt;</c>).</item>
    /// <item>The <b>F-row hotkey legends</b> are <b>authored board silkscreen</b>, not spec text.
    /// They live in <c>Geometry/Visual/FreestyleEdgeRgbVisual.cs</c> — <c>status, vdrv, speed,
    /// nkro, game, reset</c> — and were sourced from mockup <c>1e</c>'s drawing of the physical
    /// board, because specs/ documents the <em>files</em> and not what is printed on the keys.
    /// That is why the fact says "device hotkeys" rather than naming each one.</item>
    /// </list></para>
    ///
    /// <para><b>Nothing is invented to fill the panel out.</b> A device gets the facts its sources
    /// actually state and no more, so the TKO answers with two lines where the Freestyle family
    /// answers with three. A panel that always drew three would be writing the third.</para>
    ///
    /// <para><b>"hold" is the locked key itself.</b> The mockup writes the combinations as
    /// "hold + F8", not "SmartSet + F8": the panel is already showing the user which key it is
    /// talking about, and the boards spell that key differently (<c>SmartSet</c> on the Freestyle
    /// family, the TKO and the Advantage 360; <c>Program</c> on the Advantage2). Naming it twice in
    /// the same sentence would be the panel arguing with its own header.</para>
    /// </summary>
    public static class DeviceHotkeyCatalog
    {
        /// <summary>How every combination starts — the locked key, held. See the type's remarks.</summary>
        public const string HoldPrefix = "hold + ";

        /// <summary>Nothing at all, for a device whose configuration key this app has no facts about.</summary>
        private static readonly IReadOnlyList<DeviceHotkeyFact> Nothing = [];

        /// <summary>
        /// What <paramref name="device"/>'s configuration key does, in the order the panel lists
        /// it: the device hotkeys, then the v-Drive, then the profile switch. An unknown device —
        /// or one with no configuration key of its own, such as the Savant Elite2 — answers with an
        /// empty list, and the panel then draws no section rather than an empty one.
        /// </summary>
        public static IReadOnlyList<DeviceHotkeyFact> ForDevice(DeviceId device)
        {
            return device switch
            {
                DeviceId.FreestyleEdge or DeviceId.FreestylePro =>
                [
                    DeviceHotkeyFact.Silkscreen("F1…F12", "device hotkeys"),
                    DeviceHotkeyFact.Spec("F8", "mount the v-Drive", "specs/03-vdrive-and-files.md §1"),
                    DeviceHotkeyFact.Spec("1…9", "load a layout", "specs/02-devices.md, Freestyle Edge / Freestyle Pro")
                ],

                DeviceId.FreestyleEdgeRgb =>
                [
                    DeviceHotkeyFact.Silkscreen("F1…F12", "device hotkeys"),
                    DeviceHotkeyFact.Spec("F8", "mount the v-Drive", "specs/03-vdrive-and-files.md §1"),
                    DeviceHotkeyFact.Spec("1…9", "switch profile", "specs/02-devices.md, Freestyle Edge RGB")
                ],

                DeviceId.Tko =>
                [
                    DeviceHotkeyFact.Spec("right shift + V", "mount the v-Drive", "specs/03-vdrive-and-files.md §1"),
                    DeviceHotkeyFact.Spec("1…9", "switch profile", "specs/02-devices.md, TKO")
                ],

                DeviceId.Advantage360 =>
                [
                    DeviceHotkeyFact.Spec("v-Drive", "mount the v-Drive", "specs/03-vdrive-and-files.md §1"),
                    DeviceHotkeyFact.Spec("0…9", "switch profile", "specs/02-devices.md, Advantage 360 SmartSet")
                ],

                DeviceId.Advantage2 =>
                [
                    DeviceHotkeyFact.Spec("F1", "mount the v-Drive", "specs/03-vdrive-and-files.md §1")
                ],

                _ => Nothing
            };
        }

        /// <summary>Whether the panel has anything to say about this device's configuration key.</summary>
        public static bool HasFacts(DeviceId device)
        {
            return ForDevice(device).Count > 0;
        }
    }
}
