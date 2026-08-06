using KinesisEdit.Core.Settings;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Every preference stored in <c>app_settings.txt</c> that the app offers the user, as data:
    /// the twelve <c>*_msg</c> hide flags of specs/08-settings.md §3 plus the five this app adds
    /// (docs/design/mockups.md, mockup 1j). Seventeen descriptors, each carrying its key, its
    /// wording, its <see cref="AppPreferencePolarity"/> and the accessors that reach its
    /// <see cref="AppSettings"/> property.
    /// <para>
    /// <b>This is the single source.</b> The preferences view model, the preference store's
    /// read/write switch and the tests all enumerate it instead of restating the list — adding a
    /// preference anywhere else without a descriptor here leaves it unrenderable, unwritable and
    /// untested, and <c>AppPreferenceCatalogTests</c> fails if a suppression key exists outside it.
    /// </para>
    /// <para>
    /// <b>Order is meaningful.</b> <see cref="Featured"/> is the five preferences mockup 1j names,
    /// in 1j's order; <see cref="Additional"/> is the remaining twelve, in spec 08 §3 table order,
    /// and its <see cref="System.Collections.Generic.IReadOnlyCollection{T}.Count"/> is the
    /// screen's "+N more" — the mockup's literal "+7" assumed twelve preferences and there are
    /// seventeen, so the count is computed, never typed.
    /// </para>
    /// </summary>
    public static class AppPreferenceCatalog
    {
        /// <summary>Mockup 1j row 1. Forward-declared: the prompt itself is issue #52's.</summary>
        public static AppPreferenceDescriptor UnsavedChangesWarning { get; } = new(
            SettingsKeys.UnsavedChangesMessage,
            "Warn before leaving with unsaved changes",
            "The Save / Discard / Cancel prompt shown when you leave a session that has edits you have not written to the drive.",
            AppPreferencePolarity.Suppression,
            hasLiveConsumer: false,
            settings => settings.IsUnsavedChangesWarningHidden,
            (settings, stored) => settings with { IsUnsavedChangesWarningHidden = stored });

        /// <summary>Mockup 1j row 2. The first preference with a live consumer: the editor's layer reset.</summary>
        public static AppPreferenceDescriptor ResetLayerConfirmation { get; } = new(
            SettingsKeys.ResetLayerMessage,
            "Confirm before resetting a layer",
            "The confirmation shown before a layer's remaps and macros are erased.",
            AppPreferencePolarity.Suppression,
            hasLiveConsumer: true,
            settings => settings.IsResetLayerConfirmationHidden,
            (settings, stored) => settings with { IsResetLayerConfirmationHidden = stored });

        /// <summary>
        /// Mockup 1j row 3. Forward-declared: no such summary exists yet. 1j draws this row
        /// <i>unticked</i>, which is that board's stored answer (<c>capture_summary_msg=on</c>) and
        /// not the default — absent always means "show" on disk, so the default here is ticked.
        /// </summary>
        public static AppPreferenceDescriptor CaptureSummary { get; } = new(
            SettingsKeys.CaptureSummaryMessage,
            "Show the 'keystrokes captured' summary after recording",
            "The summary of what was captured, shown when macro recording stops.",
            AppPreferencePolarity.Suppression,
            hasLiveConsumer: false,
            settings => settings.IsCaptureSummaryHidden,
            (settings, stored) => settings with { IsCaptureSummaryHidden = stored });

        /// <summary>Mockup 1j row 4. Forward-declared: variant switching is issue #42's.</summary>
        public static AppPreferenceDescriptor SwitchVariantConfirmation { get; } = new(
            SettingsKeys.SwitchVariantMessage,
            "Confirm before switching keyboard variant",
            "The confirmation shown before the editor loads a different variant of this board.",
            AppPreferencePolarity.Suppression,
            hasLiveConsumer: false,
            settings => settings.IsSwitchVariantConfirmationHidden,
            (settings, stored) => settings with { IsSwitchVariantConfirmationHidden = stored });

        /// <summary>
        /// Mockup 1j row 5, and <b>the one preference whose polarity is the other way round</b>:
        /// stored <c>on</c> means "expand", not "hide", and it is off by default. Read by the
        /// advisory strip.
        /// </summary>
        public static AppPreferenceDescriptor AdvisoryDetail { get; } = new(
            SettingsKeys.AdvisoryDetail,
            "Explain advisory warnings in full instead of one line",
            "Advisory strips show their whole message rather than a single trimmed line.",
            AppPreferencePolarity.Display,
            hasLiveConsumer: true,
            settings => settings.IsAdvisoryDetailExpanded,
            (settings, stored) => settings with { IsAdvisoryDetailExpanded = stored });

        /// <summary>The five preferences mockup 1j names, in 1j's order; the screen renders these unfolded.</summary>
        public static IReadOnlyList<AppPreferenceDescriptor> Featured { get; } =
        [
            UnsavedChangesWarning,
            ResetLayerConfirmation,
            CaptureSummary,
            SwitchVariantConfirmation,
            AdvisoryDetail
        ];

        /// <summary>
        /// The remaining preferences — the twelve spec 08 §3 hide flags, in spec table order —
        /// which the screen puts behind a disclosure captioned with this list's count. All twelve
        /// are forward-declared: their dialogs belong to spec 11 work that has not landed.
        /// </summary>
        public static IReadOnlyList<AppPreferenceDescriptor> Additional { get; } =
        [
            CreateLegacy(
                SettingsKeys.AppIntroMessage,
                "Show the welcome dialog at startup",
                "The introduction shown when the app opens.",
                settings => settings.IsAppIntroMessageHidden,
                (settings, stored) => settings with { IsAppIntroMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.SaveAsMessage,
                "Explain 'Save As' before it runs",
                "The informational dialog shown when a profile is saved under another number.",
                settings => settings.IsSaveAsMessageHidden,
                (settings, stored) => settings with { IsSaveAsMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.SaveMessage,
                "Confirm after saving a profile",
                "The 'Profile N saved' notification shown once a profile is written.",
                settings => settings.IsSaveMessageHidden,
                (settings, stored) => settings with { IsSaveMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.MultiplayMessage,
                "Explain multiplay when a macro repeats",
                "The notice shown when a macro is set to play more than once.",
                settings => settings.IsMultiplayMessageHidden,
                (settings, stored) => settings with { IsMultiplayMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.SpeedMessage,
                "Explain macro speed when it changes",
                "The notice shown when a macro's playback speed is changed.",
                settings => settings.IsSpeedMessageHidden,
                (settings, stored) => settings with { IsSpeedMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.CopyMacroMessage,
                "Explain how to finish copying a macro",
                "The 'Macro copied. Now select a new trigger key…' prompt.",
                settings => settings.IsCopyMacroMessageHidden,
                (settings, stored) => settings with { IsCopyMacroMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.ResetKeyMessage,
                "Confirm before resetting a macro",
                "The confirmation shown before a single key's macro is erased.",
                settings => settings.IsResetKeyMessageHidden,
                (settings, stored) => settings with { IsResetKeyMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.FirmwareCheckMessage,
                "Remind me to check for firmware updates",
                "The startup reminder to compare the board's firmware against the current release.",
                settings => settings.IsFirmwareCheckMessageHidden,
                (settings, stored) => settings with { IsFirmwareCheckMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.SaveLightingMessage,
                "Confirm after saving lighting",
                "The notification shown once a lighting file is written.",
                settings => settings.IsSaveLightingMessageHidden,
                (settings, stored) => settings with { IsSaveLightingMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.SaveSettingsMessage,
                "Confirm after saving settings",
                "The 'Settings saved' notification shown once device settings are written.",
                settings => settings.IsSaveSettingsMessageHidden,
                (settings, stored) => settings with { IsSaveSettingsMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.WindowsCombinationMessage,
                "Explain Windows key combinations while recording",
                "The 'Windows Combination Active' hint shown during macro recording.",
                settings => settings.IsWindowsCombinationMessageHidden,
                (settings, stored) => settings with { IsWindowsCombinationMessageHidden = stored }),
            CreateLegacy(
                SettingsKeys.UpDownKeystrokeMessage,
                "Explain half keystrokes while recording",
                "The 'Downstroke/Upstroke Active' hint shown when a keystroke records press-only or release-only.",
                settings => settings.IsUpDownKeystrokeMessageHidden,
                (settings, stored) => settings with { IsUpDownKeystrokeMessageHidden = stored })
        ];

        /// <summary>Every preference: <see cref="Featured"/> then <see cref="Additional"/>, seventeen in all.</summary>
        public static IReadOnlyList<AppPreferenceDescriptor> All { get; } = [.. Featured, .. Additional];

        private static readonly IReadOnlyDictionary<string, AppPreferenceDescriptor> _byKey =
            All.ToDictionary(descriptor => descriptor.Key, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The descriptor for <paramref name="key"/>, matched case-insensitively (spec 08 §1), or
        /// null when the catalog does not model the key — which is how a stray key ends up written
        /// nowhere rather than written wrongly.
        /// </summary>
        public static AppPreferenceDescriptor? Find(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return _byKey.TryGetValue(key.Trim(), out var descriptor) ? descriptor : null;
        }

        // The twelve spec 08 §3 flags share every trait but their wording and their property, so
        // only those vary here: suppression polarity, and no consumer until their dialogs exist.
        private static AppPreferenceDescriptor CreateLegacy(
            string key,
            string caption,
            string description,
            Func<AppSettings, bool?> readStoredFlag,
            Func<AppSettings, bool, AppSettings> writeStoredFlag)
        {
            return new AppPreferenceDescriptor(
                key,
                caption,
                description,
                AppPreferencePolarity.Suppression,
                hasLiveConsumer: false,
                readStoredFlag,
                writeStoredFlag);
        }
    }
}
