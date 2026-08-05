namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// The numeric value ranges of the keyboard-settings keys per specs/08-settings.md §2,
    /// shared by the parser (values outside a range read as absent — read tolerantly) and the
    /// serializer (values outside a range are refused) and usable by the future settings UI
    /// for slider bounds.
    /// </summary>
    public static class SettingsValueRanges
    {
        /// <summary>Lowest startup profile number, <c>layout&lt;N&gt;.txt</c> / <c>profile=&lt;N&gt;</c> (spec 08 §2).</summary>
        public const int MinimumProfileNumber = 1;

        /// <summary>Highest startup profile number (spec 08 §2).</summary>
        public const int MaximumProfileNumber = 9;

        /// <summary>Lowest macro playback speed; 0 = playback disabled on every device (spec 08 §2).</summary>
        public const int MinimumMacroSpeed = 0;

        /// <summary>Highest macro playback speed, "maximum 9 for all" (spec 08 §2).</summary>
        public const int MaximumMacroSpeed = 9;

        /// <summary>Lowest status-report play speed; 0 = disabled (spec 08 §2).</summary>
        public const int MinimumStatusPlaySpeed = 0;

        /// <summary>Highest status-report play speed, "UI sliders are capped at 4" (spec 08 §2).</summary>
        public const int MaximumStatusPlaySpeed = 4;
    }
}
