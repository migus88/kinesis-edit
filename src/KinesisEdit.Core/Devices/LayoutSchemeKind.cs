namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// How a device names its layout/profile files on the v-Drive, per the master device
    /// table in specs/02-devices.md and specs/03-vdrive-and-files.md §4.
    /// </summary>
    public enum LayoutSchemeKind
    {
        /// <summary>No layout files (CROSSFIRE keypad, Advantage 360 Professional).</summary>
        None = 0,

        /// <summary>
        /// Numbered profile pairs <c>layout&lt;n&gt;.txt</c> (+ <c>led&lt;n&gt;.txt</c> where
        /// the device has lighting), n = 1..9 (Gen1 boards and Advantage 360; 03 §4.1, §4.3).
        /// </summary>
        NumberedProfiles = 1,

        /// <summary>
        /// Advantage2: named files <c>&lt;position&gt;_qwerty.txt</c> / <c>&lt;position&gt;_dvorak.txt</c>
        /// in <c>active\</c>, position a-z or 0-9 (03 §4.2).
        /// </summary>
        QwertyDvorakPositions = 2,

        /// <summary>Savant Elite 2: the single fixed file <c>active\pedals.txt</c> (03 §4.4).</summary>
        PedalFile = 3
    }
}
