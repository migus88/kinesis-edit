namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Typed in-memory model of <c>settings/app_settings.txt</c> (specs/08-settings.md §3):
    /// the twelve per-dialog notification hide flags and the twelve custom picker colors.
    /// For every hide flag, <b>true means "hide this notification"</b> (the file value
    /// <c>on</c>); null means the key is absent, which the spec defines as <c>off</c> = show —
    /// so both null and false mean the notification is shown. Colors are null when unset; an
    /// unset color's key is skipped on save, never written empty (spec 08 §3).
    /// <para>
    /// <b>Five keys here are not in spec 08</b> — four further hide flags this app's own prompts
    /// need (<c>warn_unsaved_msg</c>, <c>reset_layer_msg</c>, <c>capture_summary_msg</c>,
    /// <c>switch_variant_msg</c>) and one display preference (<c>advisory_detail</c>). Do not go
    /// hunting for them in the spec: they are additions, and they are safe on disk because
    /// <c>SettingsService.SaveAppSettings</c> writes through <c>IVDriveFileService</c>'s
    /// read-modify-write merge and the legacy Pascal app ignores keys it does not model.
    /// </para>
    /// <para>
    /// <b>A sixth addition is a whole key family</b>: <see cref="MacroNames"/>, the
    /// <c>macro_name_*</c> keys of <see cref="MacroNameKey"/>. It is the only part of this file
    /// that is <b>per profile</b> and the only part written by the editor's Save rather than
    /// through immediately — see <c>docs/app/settings.md</c>.
    /// </para>
    /// <para>
    /// <b>Two polarities live in this record and they are opposites.</b> Every <c>Is…Hidden</c>
    /// property is a suppression flag — <c>on</c> = hide, absent = show. <c>advisory_detail</c>
    /// is a <i>display</i> preference — <c>on</c> = expand, absent = the one-line form — and is
    /// named <see cref="IsAdvisoryDetailExpanded"/> so no call site can mistake it for a hide
    /// flag. Never fold the two into one "flag" abstraction.
    /// </para>
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
        /// Hide flag <c>warn_unsaved_msg</c>: the Save / Discard / Cancel prompt shown when a
        /// session with unsaved changes is left. <b>Not a spec 08 key</b> (see the type summary),
        /// but it follows the §3 convention exactly — true = <c>on</c> = <b>hide</b> the warning,
        /// null = absent = warn.
        /// </summary>
        public bool? IsUnsavedChangesWarningHidden { get; init; }

        /// <summary>
        /// Hide flag <c>reset_layer_msg</c>: the confirmation shown before a layer's remaps and
        /// macros are erased. <b>Not a spec 08 key</b> (see the type summary); §3 convention —
        /// true = <c>on</c> = <b>hide</b> the confirmation, null = absent = confirm.
        /// </summary>
        public bool? IsResetLayerConfirmationHidden { get; init; }

        /// <summary>
        /// Hide flag <c>capture_summary_msg</c>: the "keystrokes captured" summary shown when
        /// macro recording stops. <b>Not a spec 08 key</b> (see the type summary); §3 convention —
        /// true = <c>on</c> = <b>hide</b> the summary, null = absent = show it.
        /// </summary>
        public bool? IsCaptureSummaryHidden { get; init; }

        /// <summary>
        /// Hide flag <c>switch_variant_msg</c>: the confirmation shown before the editor switches
        /// to another keyboard variant. <b>Not a spec 08 key</b> (see the type summary); §3
        /// convention — true = <c>on</c> = <b>hide</b> the confirmation, null = absent = confirm.
        /// </summary>
        public bool? IsSwitchVariantConfirmationHidden { get; init; }

        /// <summary>
        /// Display preference <c>advisory_detail</c>. <b>The odd one out: this is not a hide
        /// flag.</b> True = <c>on</c> = <b>expand</b> advisory warnings to their full text; null =
        /// absent = <c>off</c> = the single trimmed line. Reading it as "hidden" inverts the
        /// screen, which is why the property is named for what <c>on</c> actually does.
        /// </summary>
        public bool? IsAdvisoryDetailExpanded { get; init; }

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

        /// <summary>
        /// Macro names by place, the <c>macro_name_*</c> half of this file
        /// (<see cref="MacroNameKey"/>). Keyed by profile + layer + trigger + slot, because
        /// <c>Macro.Id</c> is edit-time identity and reaches no file.
        /// <para>
        /// <b>An empty value is a tombstone, not a name.</b> A blank macro name is never written
        /// (unnamed macros derive a display name from their content instead), so an entry whose
        /// value is empty means "this key must be <b>deleted</b> from the file" —
        /// <see cref="AppSettingsSerializer.SerializeMacroNames"/> skips it and
        /// <see cref="AppSettingsSerializer.SerializeMacroNameRemovals"/> emits it. This is the
        /// same two-halves shape the custom colours use, and for the same reason: skipping a key
        /// and clearing it look identical to a read-modify-write merge.
        /// </para>
        /// <para>
        /// The map spans <b>every</b> profile of the device, since one file holds them all. Rewrite
        /// one profile's names with <see cref="WithMacroNamesForProfile"/>, which is what makes the
        /// tombstones — and never touches another profile's entries.
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<MacroNameKey, string> MacroNames
        {
            get => _macroNames;
            init
            {
                ArgumentNullException.ThrowIfNull(value);

                _macroNames = value;
            }
        }

        private readonly IReadOnlyList<SettingsColor?> _customColors = new SettingsColor?[CustomColorCount];

        private readonly IReadOnlyDictionary<MacroNameKey, string> _macroNames =
            new Dictionary<MacroNameKey, string>();

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

        /// <summary>
        /// The stored name of one macro place, or null when the file carries none — in which case
        /// the caller derives one from the macro's content
        /// (<c>Model.MacroNaming.DeriveDisplayName</c>). A tombstoned entry (empty value) also
        /// reads as null: it is a pending deletion, not a name.
        /// </summary>
        public string? GetMacroName(MacroNameKey key)
        {
            return _macroNames.TryGetValue(key, out var name) && name.Length > 0 ? name : null;
        }

        /// <summary>
        /// Returns a copy with one macro name set. A null or blank <paramref name="name"/> leaves a
        /// <b>tombstone</b> — the entry stays with an empty value so the next save deletes the key
        /// — rather than dropping the entry, which a merge could not tell from "unchanged".
        /// </summary>
        public AppSettings WithMacroName(MacroNameKey key, string? name)
        {
            var names = new Dictionary<MacroNameKey, string>(_macroNames)
            {
                [key] = name?.Trim() ?? string.Empty
            };

            return this with { MacroNames = names };
        }

        /// <summary>
        /// Replaces <b>all</b> of <paramref name="profileNumber"/>'s macro names with
        /// <paramref name="names"/>, tombstoning every key of that profile the new set does not
        /// carry. Entries of every other profile are untouched, which is the whole point: one
        /// device file holds nine profiles' names and saving one must never delete another's.
        /// <para>
        /// Hand it the complete current picture of the profile —
        /// <c>MacroLibrary.EnumerateStoredNames()</c> mapped through
        /// <see cref="MacroNameKey.TryCreate"/> — and renames, deletions and unassignments all
        /// resolve by themselves: whatever is gone becomes a removal.
        /// </para>
        /// <para>
        /// Entries in <paramref name="names"/> whose <see cref="MacroNameKey.ProfileNumber"/> is not
        /// <paramref name="profileNumber"/> are rejected with <see cref="ArgumentException"/> — a
        /// caller mixing profiles here is the exact mistake this signature exists to stop.
        /// </para>
        /// </summary>
        public AppSettings WithMacroNamesForProfile(
            int profileNumber,
            IEnumerable<KeyValuePair<MacroNameKey, string>> names)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(profileNumber, 1);
            ArgumentNullException.ThrowIfNull(names);

            var updated = new Dictionary<MacroNameKey, string>(_macroNames);

            foreach (var pair in _macroNames)
            {
                if (pair.Key.ProfileNumber == profileNumber)
                {
                    updated[pair.Key] = string.Empty;
                }
            }

            foreach (var pair in names)
            {
                if (pair.Key.ProfileNumber != profileNumber)
                {
                    throw new ArgumentException(
                        $"Name key names profile {pair.Key.ProfileNumber}, but profile {profileNumber} is being written.",
                        nameof(names));
                }

                updated[pair.Key] = pair.Value?.Trim() ?? string.Empty;
            }

            return this with { MacroNames = updated };
        }
    }
}
