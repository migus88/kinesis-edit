namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The walk over every place a macro sits in a layout, and the two name steps built on it —
    /// the load stamp (<see cref="ApplyNames"/>) and the save harvest
    /// (<see cref="EnumerateStoredNames"/>).
    /// <para>
    /// <b>There is no shared macro anywhere on disk.</b> Every key's slot holds its own independent
    /// copy (06 §1) and the Gen2 flat list holds one entry per trigger, so a site is simply a
    /// <em>location</em>: two keys carrying identical content are two macros that happen to look
    /// alike, and editing one leaves the other alone. Nothing here groups them.
    /// </para>
    /// <para>
    /// <b>Both stores, no device switch.</b> Slot devices (<see cref="KeyboardKey.Macro1"/>..
    /// <c>Macro5</c>) and the Advantage360's flat list
    /// (<see cref="KeyboardLayout.Macros"/>/<see cref="KeyboardLayout.UsesFlatMacroList"/>) are read
    /// through the same walk; which one a layout uses is read off the layout, never off a device id.
    /// </para>
    /// </summary>
    public static class MacroSites
    {
        /// <summary>Slot value of a macro that lives in the Gen2 flat list (06 §1).</summary>
        public const int FlatListSlot = 0;

        /// <summary>
        /// Slot argument of <see cref="MacroPlacement.CopyTo"/> meaning "the key's first empty
        /// slot". Deliberately <b>not</b> <see cref="FlatListSlot"/>: the flat list's own slot
        /// number is 0, and one value meaning both "wherever it fits" and "the flat-list slot"
        /// leaves a caller unable to say which it meant.
        /// </summary>
        public const int FirstEmptySlot = -1;

        /// <summary>
        /// Every place a macro sits in <paramref name="layout"/>, in a fixed order: layers by index,
        /// keys by index, slots <see cref="Macro.MinMacroIndex"/>..<see cref="Macro.MaxMacroIndex"/>,
        /// then <see cref="KeyboardLayout.Macros"/> in list order. Covers exactly the macros
        /// <see cref="KeyboardLayout.EnumerateMacros"/> yields, except that one instance parked at
        /// two places yields <b>two sites</b> — it really is on two positions.
        /// <para>
        /// A layout with an empty flat list simply contributes nothing from it, so no
        /// <see cref="KeyboardLayout.UsesFlatMacroList"/> branch is needed here.
        /// </para>
        /// <para>
        /// TKO edge zones are lighting zones, not typing keys (05 §4.4), so they are outside this
        /// walk exactly as they are outside every layout counter.
        /// </para>
        /// </summary>
        public static IReadOnlyList<MacroSite> Enumerate(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var sites = new List<MacroSite>();

            foreach (var layer in layout.Layers)
            {
                foreach (var key in layer.Keys)
                {
                    for (var slot = Macro.MinMacroIndex; slot <= Macro.MaxMacroIndex; slot++)
                    {
                        if (key.GetMacro(slot) is not Macro macro)
                        {
                            continue;
                        }

                        sites.Add(new MacroSite
                        {
                            LayerIndex = layer.Index,

                            // 05 §1.3: fn1s and keyt trigger as their *original* action even though
                            // the position carries its own token, and macro lookup follows trigger
                            // identity (keyboard-model.md invariant 4). A site keyed by the position
                            // key would name "lfn", and the settings key written under it is one the
                            // next load never looks up — the name would round-trip to nothing.
                            TriggerKeyCode = key.TriggerKey.Code,
                            SlotNumber = slot,
                            Macro = macro,
                            Key = key
                        });
                    }
                }
            }

            foreach (var macro in layout.Macros)
            {
                sites.Add(new MacroSite
                {
                    LayerIndex = macro.LayerIndex,
                    TriggerKeyCode = macro.TriggerKey,
                    SlotNumber = FlatListSlot,
                    Macro = macro
                });
            }

            return sites;
        }

        /// <summary>
        /// The load step: stamps stored names onto a freshly parsed layout, which always has none
        /// (see <see cref="Macro.Name"/>). <paramref name="nameLookup"/> answers "what is this site
        /// called?" — typically a lookup into <c>AppSettings.MacroNames</c> keyed by
        /// <c>Settings.MacroNameKey</c>.
        /// <para>
        /// <b>Every site is written, including the ones with no stored name</b>: a lookup returning
        /// null or blank <em>resets</em> that macro's name to the empty string rather than leaving
        /// it alone. That is what makes the stamp a faithful picture of the file for a layout just
        /// off the parser — and it is also the hazard, because re-running it over a layout the user
        /// has been editing wipes every unsaved rename with nothing on screen to say so. Callers
        /// that keep layouts alive across profile switches must run this only on a genuine load.
        /// </para>
        /// </summary>
        public static void ApplyNames(KeyboardLayout layout, Func<MacroSite, string?> nameLookup)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(nameLookup);

            foreach (var site in Enumerate(layout))
            {
                site.Macro.Name = MacroNaming.Sanitize(nameLookup(site));
            }
        }

        /// <summary>
        /// The save step: one pair per site whose macro carries a name, and everything the settings
        /// layer persists as
        /// <c>macro_name_&lt;profile&gt;_&lt;layer&gt;_&lt;trigger&gt;_&lt;slot&gt;</c>.
        /// <para>
        /// Sites whose macro is unnamed are <b>absent</b>. A derived display name
        /// (<see cref="MacroNaming.DeriveDisplayName"/>) is recomputed on every load and never
        /// stored, so it follows the macro's content instead of freezing — which is only true while
        /// <see cref="Macro.Name"/> stays empty for it.
        /// </para>
        /// <para>
        /// Two keys carrying the same name yield <b>two independent pairs</b>, because they are two
        /// macros in two places; nothing here merges them.
        /// </para>
        /// <para>
        /// Hand the whole list to <c>AppSettings.WithMacroNamesForProfile</c>, which turns
        /// everything missing from it into a removal — so a cleared name needs no separate delete.
        /// </para>
        /// </summary>
        public static IReadOnlyList<KeyValuePair<MacroSite, string>> EnumerateStoredNames(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var names = new List<KeyValuePair<MacroSite, string>>();

            foreach (var site in Enumerate(layout))
            {
                // Macro.Name is sanitized by its own setter, so an empty string is the model's
                // "unnamed" and no second normalisation is owed here.
                var name = site.Macro.Name;

                if (name.Length == 0)
                {
                    continue;
                }

                names.Add(KeyValuePair.Create(site, name));
            }

            return names;
        }
    }
}
