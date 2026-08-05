namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Typed in-memory model of <c>settings/app_settings.txt</c> (specs/08-settings.md §3):
    /// the twelve per-dialog notification hide flags and the twelve custom picker colors.
    /// For every hide flag, <b>true means "hide this notification"</b> (the file value
    /// <c>on</c>); null means the key is absent, which the spec defines as <c>off</c> = show —
    /// so both null and false mean the notification is shown. Colors are null when unset; an
    /// unset color's key is skipped on save, never written empty (spec 08 §3).
    /// </summary>
    public sealed record AppSettings
    {
        /// <summary>Number of custom-color slots, <c>cust_color_1</c>…<c>cust_color_12</c> (spec 08 §3).</summary>
        public const int CustomColorCount = 12;

        /// <summary>A model with every key absent: all notifications shown, no custom colors.</summary>
        public static AppSettings Empty { get; } = new();

        /// <summary>Hide flag <c>app_intro_msg</c>: startup welcome/introduction dialog (spec 08 §3).</summary>
        public bool? IsAppIntroMessageHidden { get; init; }

        /// <summary>Hide flag <c>saveas_msg</c>: "Save As" informational dialog (spec 08 §3).</summary>
        public bool? IsSaveAsMessageHidden { get; init; }

        /// <summary>Hide flag <c>save_msg</c>: "Profile N Saved" / "Settings Saved" notifications (spec 08 §3).</summary>
        public bool? IsSaveMessageHidden { get; init; }

        /// <summary>Hide flag <c>multiplay_msg</c>: multiplay macro notification (spec 08 §3).</summary>
        public bool? IsMultiplayMessageHidden { get; init; }

        /// <summary>Hide flag <c>speed_msg</c>: macro speed notification (spec 08 §3).</summary>
        public bool? IsSpeedMessageHidden { get; init; }

        /// <summary>Hide flag <c>copy_macro_msg</c>: "Macro copied. Now select a new trigger key…" (spec 08 §3).</summary>
        public bool? IsCopyMacroMessageHidden { get; init; }

        /// <summary>Hide flag <c>reset_key_msg</c>: "Do you want to reset the current Macro?" confirmation (spec 08 §3).</summary>
        public bool? IsResetKeyMessageHidden { get; init; }

        /// <summary>Hide flag <c>app_checkfirm_msg</c>: startup firmware-update reminder (spec 08 §3).</summary>
        public bool? IsFirmwareCheckMessageHidden { get; init; }

        /// <summary>Hide flag <c>savelighting_msg</c>: lighting-saved notification (spec 08 §3).</summary>
        public bool? IsSaveLightingMessageHidden { get; init; }

        /// <summary>Hide flag <c>savesettings_msg</c>: settings-saved notification (spec 08 §3).</summary>
        public bool? IsSaveSettingsMessageHidden { get; init; }

        /// <summary>Hide flag <c>windowscombo_msg</c>: "Windows Combination Active" macro-recording hint (spec 08 §3).</summary>
        public bool? IsWindowsCombinationMessageHidden { get; init; }

        /// <summary>Hide flag <c>updownkeystroke_msg</c>: "Downstroke/Upstroke Active" half-keystroke hint (spec 08 §3).</summary>
        public bool? IsUpDownKeystrokeMessageHidden { get; init; }

        /// <summary>
        /// The twelve custom picker colors, index 0 = <c>cust_color_1</c> … index 11 =
        /// <c>cust_color_12</c> (spec 08 §3). Always exactly
        /// <see cref="CustomColorCount"/> entries; null entries are unset slots.
        /// </summary>
        public IReadOnlyList<SettingsColor?> CustomColors
        {
            get => _customColors;
            init
            {
                ArgumentNullException.ThrowIfNull(value);

                if (value.Count != CustomColorCount)
                {
                    throw new ArgumentException(
                        $"CustomColors must contain exactly {CustomColorCount} entries (specs/08-settings.md §3).",
                        nameof(value));
                }

                _customColors = value;
            }
        }

        private readonly IReadOnlyList<SettingsColor?> _customColors = new SettingsColor?[CustomColorCount];

        /// <summary>
        /// Returns a copy with one custom-color slot replaced. <paramref name="slotNumber"/> is
        /// 1-based and uses the same numbering as
        /// <see cref="SettingsKeys.GetCustomColorKey"/> — 1 = <c>cust_color_1</c> … 12 =
        /// <c>cust_color_12</c>. Passing null clears the slot, and a cleared slot's key is
        /// skipped on save rather than written empty (spec 08 §3). Throws
        /// <see cref="ArgumentOutOfRangeException"/> outside 1-<see cref="CustomColorCount"/>.
        /// </summary>
        public AppSettings WithCustomColor(int slotNumber, SettingsColor? color)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(slotNumber, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(slotNumber, CustomColorCount);

            var colors = new SettingsColor?[CustomColorCount];

            for (var index = 0; index < CustomColorCount; index++)
            {
                colors[index] = _customColors[index];
            }

            colors[slotNumber - 1] = color;

            return this with { CustomColors = colors };
        }
    }
}
