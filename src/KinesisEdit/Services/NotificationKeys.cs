using KinesisEdit.Core.Settings;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The suppression keys a <see cref="MessageBoxRequest.SuppressionKey"/> may carry: the twelve
    /// <c>*_msg</c> names of specs/08-settings.md §3 plus the four this app adds for its own
    /// prompts. Each is a boolean persisted in <c>app_settings.txt</c> as <c>on</c>/<c>off</c>
    /// where <b><c>on</c> means "hide this notification"</b>; a missing key defaults to <c>off</c>
    /// = show.
    /// <para>
    /// These are <b>aliases</b>, not a second list: every constant is the corresponding
    /// <see cref="SettingsKeys"/> key, and <see cref="All"/> is derived from
    /// <see cref="AppPreferenceCatalog"/> by filtering to
    /// <see cref="AppPreferencePolarity.Suppression"/>. So a suppression key cannot exist without
    /// a catalog descriptor, and <c>advisory_detail</c> — a <i>display</i> preference where
    /// <c>on</c> means "expand" — is deliberately absent here: it can never be a box's
    /// suppression key, and the type system now says so.
    /// </para>
    /// </summary>
    public static class NotificationKeys
    {
        /// <summary>Startup welcome/introduction dialog.</summary>
        public const string AppIntro = SettingsKeys.AppIntroMessage;

        /// <summary>"Save As" informational dialog.</summary>
        public const string SaveAs = SettingsKeys.SaveAsMessage;

        /// <summary>"Profile N Saved" / "Settings Saved" notifications.</summary>
        public const string Save = SettingsKeys.SaveMessage;

        /// <summary>Multiplay macro notification.</summary>
        public const string Multiplay = SettingsKeys.MultiplayMessage;

        /// <summary>Macro speed notification.</summary>
        public const string Speed = SettingsKeys.SpeedMessage;

        /// <summary>"Macro copied. Now select a new trigger key…" notification.</summary>
        public const string CopyMacro = SettingsKeys.CopyMacroMessage;

        /// <summary>"Do you want to reset the current Macro?" confirmation.</summary>
        public const string ResetKey = SettingsKeys.ResetKeyMessage;

        /// <summary>Startup firmware-update reminder.</summary>
        public const string CheckFirmware = SettingsKeys.FirmwareCheckMessage;

        /// <summary>Lighting-saved notification (RGB legacy UI).</summary>
        public const string SaveLighting = SettingsKeys.SaveLightingMessage;

        /// <summary>Settings-saved notification.</summary>
        public const string SaveSettings = SettingsKeys.SaveSettingsMessage;

        /// <summary>"Windows Combination Active" macro-recording hint.</summary>
        public const string WindowsCombo = SettingsKeys.WindowsCombinationMessage;

        /// <summary>"Downstroke/Upstroke Active" half-keystroke hint.</summary>
        public const string UpDownKeystroke = SettingsKeys.UpDownKeystrokeMessage;

        /// <summary>The Save / Discard / Cancel prompt shown on leaving a session with unsaved changes.</summary>
        public const string UnsavedChanges = SettingsKeys.UnsavedChangesMessage;

        /// <summary>The confirmation shown before a layer's remaps and macros are erased.</summary>
        public const string ResetLayer = SettingsKeys.ResetLayerMessage;

        /// <summary>The "keystrokes captured" summary shown when macro recording stops.</summary>
        public const string CaptureSummary = SettingsKeys.CaptureSummaryMessage;

        /// <summary>The confirmation shown before the editor switches keyboard variant.</summary>
        public const string SwitchVariant = SettingsKeys.SwitchVariantMessage;

        /// <summary>
        /// Every suppression key, in <see cref="AppPreferenceCatalog"/> order — the five of mockup
        /// 1j first (of which four are suppression keys), then the twelve of spec 08 §3 in spec
        /// table order. Derived, so this list can never drift from the catalog.
        /// </summary>
        public static IReadOnlyList<string> All { get; } =
        [
            .. AppPreferenceCatalog.All
                .Where(descriptor => descriptor.IsSuppression)
                .Select(descriptor => descriptor.Key)
        ];
    }
}
