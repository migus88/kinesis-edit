namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Serializes an <see cref="AppSettings"/> into the key/value pairs the app writes to
    /// app_settings.txt, per specs/08-settings.md §3. Flags are persisted lowercase
    /// <c>on</c>/<c>off</c>; flags that were never set (null) and unset colors are skipped — an
    /// unset color's key is never written empty. Pure data-out — the pairs are applied through
    /// <c>IVDriveFileService</c> so unmanaged lines survive, which is also why the five keys
    /// this app adds beyond spec 08 (see <see cref="AppSettings"/>) are safe on disk.
    /// <para>
    /// <see cref="Serialize"/> alone cannot say "this slot is now empty" — skipping a key and
    /// clearing it look identical to a merge. <see cref="SerializeRemovals"/> is the other half:
    /// the keys to take off the file.
    /// </para>
    /// <para>
    /// <see cref="AppendOnOffFlag"/> writes <c>on</c> for true and <c>off</c> for false and knows
    /// nothing about meaning: for the sixteen hide flags <c>on</c> = hide, for
    /// <c>advisory_detail</c> <c>on</c> = expand. The polarity lives in the property names.
    /// </para>
    /// <para>
    /// The <c>macro_name_*</c> family has its own pair,
    /// <see cref="SerializeMacroNames"/>/<see cref="SerializeMacroNameRemovals"/>, and both
    /// <b>require the profile number being written</b>: one file holds every profile's names, and
    /// a removal set that did not name its profile could delete another profile's. Keeping them out
    /// of <see cref="Serialize"/>/<see cref="SerializeRemovals"/> is what makes that impossible to
    /// get wrong by omission — those two have no profile, so they neither write nor remove a name.
    /// </para>
    /// </summary>
    public static class AppSettingsSerializer
    {
        private const string OnValue = "on";
        private const string OffValue = "off";

        /// <summary>
        /// Emits the pairs to write: the set flags — the twelve of spec 08 §3 in table order,
        /// then this app's five additions — and then the set colors.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Serialize(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var pairs = new List<KeyValuePair<string, string>>();

            AppendOnOffFlag(pairs, SettingsKeys.AppIntroMessage, settings.IsAppIntroMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.SaveAsMessage, settings.IsSaveAsMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.SaveMessage, settings.IsSaveMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.MultiplayMessage, settings.IsMultiplayMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.SpeedMessage, settings.IsSpeedMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.CopyMacroMessage, settings.IsCopyMacroMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.ResetKeyMessage, settings.IsResetKeyMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.FirmwareCheckMessage, settings.IsFirmwareCheckMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.SaveLightingMessage, settings.IsSaveLightingMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.SaveSettingsMessage, settings.IsSaveSettingsMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.WindowsCombinationMessage, settings.IsWindowsCombinationMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.UpDownKeystrokeMessage, settings.IsUpDownKeystrokeMessageHidden);
            AppendOnOffFlag(pairs, SettingsKeys.UnsavedChangesMessage, settings.IsUnsavedChangesWarningHidden);
            AppendOnOffFlag(pairs, SettingsKeys.ResetLayerMessage, settings.IsResetLayerConfirmationHidden);
            AppendOnOffFlag(pairs, SettingsKeys.CaptureSummaryMessage, settings.IsCaptureSummaryHidden);
            AppendOnOffFlag(pairs, SettingsKeys.SwitchVariantMessage, settings.IsSwitchVariantConfirmationHidden);
            AppendOnOffFlag(pairs, SettingsKeys.AdvisoryDetail, settings.IsAdvisoryDetailExpanded);

            for (var slotNumber = 1; slotNumber <= AppSettings.CustomColorCount; slotNumber++)
            {
                if (settings.CustomColors[slotNumber - 1] is SettingsColor color)
                {
                    pairs.Add(KeyValuePair.Create(SettingsKeys.GetCustomColorKey(slotNumber), color.ToString()));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Emits the keys to <b>delete</b> from the file: the custom-color slots this model
        /// leaves unset. <see cref="Serialize"/> skips such a slot rather than writing
        /// <c>cust_color_N=</c> (spec 08 §3 — a color key is never written empty), so a merge on
        /// its own can only ever add or replace a slot, never clear one: the stale line would
        /// survive and the swatch would return on the next load. Clearing is therefore expressed
        /// as a removal, which <c>IVDriveFileService.UpdateSettingsFile</c> applies with the same
        /// '='-separator key rule.
        /// <para>
        /// Flags are deliberately absent from this set. A null flag means "never answered", not
        /// "cleared" — answering one always writes an explicit <c>on</c>/<c>off</c>, so a flag has
        /// no cleared state to express — and deleting its key would throw away a line this app
        /// never owned, possibly one the legacy Pascal app wrote.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> SerializeRemovals(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var keys = new List<string>();

            for (var slotNumber = 1; slotNumber <= AppSettings.CustomColorCount; slotNumber++)
            {
                if (settings.CustomColors[slotNumber - 1] is null)
                {
                    keys.Add(SettingsKeys.GetCustomColorKey(slotNumber));
                }
            }

            return keys;
        }

        /// <summary>
        /// Emits the <c>macro_name_*</c> pairs of <b>one profile</b>, in key order (profile, layer,
        /// trigger, slot) so two saves of an unchanged model produce byte-identical output. Blank
        /// entries are skipped — an unnamed macro derives its display name from its content and
        /// never occupies a line — which is exactly why
        /// <see cref="SerializeMacroNameRemovals"/> exists.
        /// <para>
        /// <b>The profile number is required, not optional.</b> One <c>app_settings.txt</c> holds
        /// every profile's names, so a call that did not name one would have to guess, and guessing
        /// wrong here deletes another profile's work. It is also why macro names are absent from
        /// <see cref="Serialize"/>: that method has no profile to scope itself to, and the swatch
        /// and preference writes that call it must not carry a rename to disk.
        /// </para>
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> SerializeMacroNames(
            AppSettings settings,
            int profileNumber)
        {
            var pairs = new List<KeyValuePair<string, string>>();

            foreach (var key in EnumerateProfileKeys(settings, profileNumber))
            {
                var name = settings.MacroNames[key];

                if (name.Length > 0)
                {
                    pairs.Add(KeyValuePair.Create(SettingsKeys.GetMacroNameKey(key), name));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Emits the <c>macro_name_*</c> keys of <b>one profile</b> to <b>delete</b>: its tombstoned
        /// entries, i.e. the places whose macro was renamed to nothing, unassigned or deleted
        /// (<see cref="AppSettings.WithMacroNamesForProfile"/>). Without this half a deleted macro's
        /// name would survive on the drive and reattach itself to whatever later occupied the same
        /// layer/trigger/slot — the identical failure a cleared colour swatch had (issue #95).
        /// <para>
        /// <b>Scoped to <paramref name="profileNumber"/> and nothing else.</b> Keys belonging to
        /// other profiles are never returned, whatever state they are in.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> SerializeMacroNameRemovals(AppSettings settings, int profileNumber)
        {
            var keys = new List<string>();

            foreach (var key in EnumerateProfileKeys(settings, profileNumber))
            {
                if (settings.MacroNames[key].Length == 0)
                {
                    keys.Add(SettingsKeys.GetMacroNameKey(key));
                }
            }

            return keys;
        }

        private static void AppendOnOffFlag(List<KeyValuePair<string, string>> pairs, string key, bool? value)
        {
            if (value is bool flag)
            {
                pairs.Add(KeyValuePair.Create(key, flag ? OnValue : OffValue));
            }
        }

        // A dictionary has no order, and two saves of an unchanged model must produce the same file.
        private static List<MacroNameKey> EnumerateProfileKeys(AppSettings settings, int profileNumber)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentOutOfRangeException.ThrowIfLessThan(profileNumber, 1);

            var keys = new List<MacroNameKey>();

            foreach (var pair in settings.MacroNames)
            {
                if (pair.Key.ProfileNumber == profileNumber)
                {
                    keys.Add(pair.Key);
                }
            }

            keys.Sort(static (left, right) =>
            {
                var byLayer = left.LayerIndex.CompareTo(right.LayerIndex);

                if (byLayer != 0)
                {
                    return byLayer;
                }

                var byTrigger = left.TriggerKeyCode.CompareTo(right.TriggerKeyCode);

                return byTrigger != 0 ? byTrigger : left.SlotNumber.CompareTo(right.SlotNumber);
            });

            return keys;
        }
    }
}
