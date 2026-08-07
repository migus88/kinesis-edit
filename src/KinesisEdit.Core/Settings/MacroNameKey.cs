using System.Globalization;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Identity of one macro name in <c>settings/app_settings.txt</c>: the profile, layer, trigger
    /// key and macro slot the name belongs to, spelled on disk as
    /// <c>macro_name_&lt;profile&gt;_&lt;layer&gt;_&lt;trigger&gt;_&lt;slot&gt;</c>.
    /// <para>
    /// <b>Why these four and not a macro id.</b> <c>Macro.Id</c> is a fresh <see cref="Guid"/> per
    /// edit and is never written to any file, so it cannot survive a reload. The macro's *place* is
    /// what both the layout file and this file can name: trigger identity plus layer plus slot
    /// (05 §1.3, 06 §1). Slot is <b>0</b> for the Advantage360's flat list, which has no slots.
    /// </para>
    /// <para>
    /// <b>The profile is part of the key</b>, because one device file holds every profile's names
    /// (spec 08 §3: one <c>app_settings.txt</c> per drive, nine layout profiles behind it). Every
    /// write path takes the profile number explicitly so saving profile 1 can never touch
    /// profile 2's keys.
    /// </para>
    /// <para>
    /// Components are non-negative decimals with no sign and no padding, so the key is scanned by
    /// prefix and split on <c>_</c> with no ambiguity. A macro with no trigger or no layer (an
    /// unbound Gen2 flat-list macro, 06 §1) has no key at all — <see cref="TryCreate"/> returns
    /// false and its name is not persisted.
    /// </para>
    /// </summary>
    public readonly record struct MacroNameKey
    {
        /// <summary>Layout profile number, 1-9 (spec 08 §2 <c>startup_file</c>/<c>profile</c> range).</summary>
        public int ProfileNumber { get; }

        /// <summary>Layer index the macro fires on (05 §1.4).</summary>
        public int LayerIndex { get; }

        /// <summary>Code of the key's trigger identity (05 §1.3; 06 §1).</summary>
        public int TriggerKeyCode { get; }

        /// <summary>Macro slot 1-5, or <b>0</b> for a flat-list macro (06 §1).</summary>
        public int SlotNumber { get; }

        /// <summary>
        /// Creates a key. Throws <see cref="ArgumentOutOfRangeException"/> for a profile below 1 or
        /// any negative component — those cannot be spelled as unsigned decimals; use
        /// <see cref="TryCreate"/> where the values come from a macro that may be unbound.
        /// </summary>
        public MacroNameKey(int profileNumber, int layerIndex, int triggerKeyCode, int slotNumber)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(profileNumber, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(layerIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(triggerKeyCode);
            ArgumentOutOfRangeException.ThrowIfNegative(slotNumber);

            ProfileNumber = profileNumber;
            LayerIndex = layerIndex;
            TriggerKeyCode = triggerKeyCode;
            SlotNumber = slotNumber;
        }

        /// <summary>
        /// The non-throwing form: false when any component is out of range, which is how an unbound
        /// macro (<c>LayerIndex</c>/<c>TriggerKey</c> still -1) declines to be persisted.
        /// </summary>
        public static bool TryCreate(
            int profileNumber,
            int layerIndex,
            int triggerKeyCode,
            int slotNumber,
            out MacroNameKey key)
        {
            if (profileNumber < 1 || layerIndex < 0 || triggerKeyCode < 0 || slotNumber < 0)
            {
                key = default;

                return false;
            }

            key = new MacroNameKey(profileNumber, layerIndex, triggerKeyCode, slotNumber);

            return true;
        }

        /// <summary>
        /// Parses a whole settings key (<c>macro_name_1_0_114_2</c>), case-insensitively on the
        /// prefix. False for anything that is not exactly the prefix plus four unsigned decimals.
        /// </summary>
        public static bool TryParse(string? settingsKey, out MacroNameKey key)
        {
            key = default;

            if (settingsKey is null
                || !settingsKey.StartsWith(SettingsKeys.MacroNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryParseSuffix(settingsKey[SettingsKeys.MacroNamePrefix.Length..], out key);
        }

        /// <summary>
        /// Parses the part <b>after</b> <see cref="SettingsKeys.MacroNamePrefix"/> — what a prefix
        /// scan of the file hands back (<c>1_0_114_2</c>).
        /// </summary>
        public static bool TryParseSuffix(string? suffix, out MacroNameKey key)
        {
            key = default;

            if (suffix is null)
            {
                return false;
            }

            var parts = suffix.Split('_');

            if (parts.Length != 4)
            {
                return false;
            }

            if (!TryParseComponent(parts[0], out var profileNumber)
                || !TryParseComponent(parts[1], out var layerIndex)
                || !TryParseComponent(parts[2], out var triggerKeyCode)
                || !TryParseComponent(parts[3], out var slotNumber))
            {
                return false;
            }

            return TryCreate(profileNumber, layerIndex, triggerKeyCode, slotNumber, out key);
        }

        /// <summary>The settings key this identity is written as (spec 08 §1 <c>key=value</c>).</summary>
        public string ToSettingsKey()
        {
            return SettingsKeys.GetMacroNameKey(this);
        }

        // Strict: no sign, no whitespace, no thousands separators, invariant culture — a settings
        // key is machine-written text, and anything else is a foreign line this app must not claim.
        private static bool TryParseComponent(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }
}
