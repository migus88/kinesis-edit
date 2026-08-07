namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The layout's macros seen as <b>one logical macro per name, with many trigger sites</b> —
    /// the model behind the redesign's Macros tab ("every macro has a name, so the tab lists them
    /// once: rename, see which keys and layers fire each one, duplicate, delete").
    /// <para>
    /// <b>The file has no shared macro.</b> Every key's slot holds its own copy (06 §1), and the
    /// Gen2 flat list holds one entry per trigger, so "the same macro on two keys" exists nowhere
    /// on disk. This type owns that identity in the app: it groups the sites, keeps their content
    /// in step (<see cref="Propagate"/>) and lets a name be assigned to a further key
    /// (<see cref="AssignTo"/>). Nothing here changes what the firmware reads — the layout still
    /// carries two independent copies.
    /// </para>
    /// <para>
    /// <b>Both stores, no device switch.</b> Slot devices (<see cref="KeyboardKey.Macro1"/>..
    /// <c>Macro5</c>) and the Advantage360's flat list
    /// (<see cref="KeyboardLayout.Macros"/>/<see cref="KeyboardLayout.UsesFlatMacroList"/>) are read
    /// and written through the same site walk; which one applies is read off the layout, never off
    /// a device id.
    /// </para>
    /// <para>
    /// <b>Entries are a snapshot.</b> Every mutation rebuilds <see cref="Entries"/>, so an entry
    /// held across one is stale; each mutating method returns the fresh entry, and passing a stale
    /// one throws <see cref="ArgumentException"/>.
    /// </para>
    /// </summary>
    public sealed partial class MacroLibrary
    {
        /// <summary>Slot value of a macro that lives in the Gen2 flat list (06 §1).</summary>
        public const int FlatListSlot = 0;

        /// <summary>Slot argument of <see cref="AssignTo"/> meaning "the key's first empty slot".</summary>
        public const int FirstEmptySlot = 0;

        /// <summary>The layout this library is a view of.</summary>
        public KeyboardLayout Layout { get; }

        /// <summary>
        /// Whether macros are stored in <see cref="KeyboardLayout.Macros"/> rather than per-key
        /// slots (Advantage360 only, 06 §1) — the one fact that changes how a site is written.
        /// </summary>
        public bool UsesFlatMacroList => Layout.UsesFlatMacroList;

        /// <summary>
        /// One entry per logical macro, in site order, each with a unique <see cref="MacroLibraryEntry.Name"/>.
        /// </summary>
        public IReadOnlyList<MacroLibraryEntry> Entries { get; private set; }

        /// <summary>Builds the library over <paramref name="layout"/> and reconciles its names.</summary>
        public MacroLibrary(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            Layout = layout;
            Entries = MacroLibraryBuilder.Build(layout);
        }

        /// <summary>
        /// Every place a macro sits in <paramref name="layout"/>, in a fixed order: layers by index,
        /// keys by index, slots 1..5, then the flat list in list order. Public because the load step
        /// that applies stored names and the save step that collects them must walk exactly the same
        /// sites the library groups.
        /// </summary>
        public static IReadOnlyList<MacroSite> EnumerateSites(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            return MacroLibraryBuilder.EnumerateSites(layout);
        }

        /// <summary>
        /// The load step: stamps stored names onto a freshly parsed layout, which always has none
        /// (see <see cref="Macro.Name"/>). <paramref name="nameLookup"/> answers "what is this site
        /// called?" — typically a lookup into <c>AppSettings.MacroNames</c> keyed by
        /// <c>Settings.MacroNameKey</c>; null or blank leaves the macro unnamed, so its name is
        /// derived at display time.
        /// <para>
        /// Call this <b>before</b> constructing the library: grouping is by name, so names applied
        /// afterwards need a <see cref="Refresh"/> to take effect.
        /// </para>
        /// </summary>
        public static void ApplyNames(KeyboardLayout layout, Func<MacroSite, string?> nameLookup)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(nameLookup);

            foreach (var site in EnumerateSites(layout))
            {
                site.Macro.Name = MacroNaming.Sanitize(nameLookup(site));
            }
        }

        /// <summary>Rebuilds <see cref="Entries"/> from the layout's current state.</summary>
        public void Refresh()
        {
            Entries = MacroLibraryBuilder.Build(Layout);
        }

        /// <summary>
        /// The entry called <paramref name="name"/> (trimmed, case-insensitive), or null — the
        /// lookup behind the inspector's "pick a macro by name" dropdown.
        /// </summary>
        public MacroLibraryEntry? Find(string? name)
        {
            foreach (var entry in Entries)
            {
                if (MacroNaming.AreSameName(entry.Name, name))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// The entry one of whose sites holds <paramref name="macro"/> (by reference), or null —
        /// how a key's selected slot maps back onto a library row.
        /// </summary>
        public MacroLibraryEntry? FindByMacro(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            foreach (var entry in Entries)
            {
                foreach (var site in entry.Sites)
                {
                    if (ReferenceEquals(site.Macro, macro))
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// A name no current entry holds: <paramref name="name"/> itself when it is free, otherwise
        /// the first free <c>" (2)"</c>/<c>" (3)"</c>… spelling (<see cref="MacroNaming.Disambiguate"/>).
        /// </summary>
        public string CreateUniqueName(string? name)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in Entries)
            {
                taken.Add(entry.Name);
            }

            return MacroLibraryBuilder.MakeUnique(MacroNaming.Sanitize(name), taken);
        }

        /// <summary>
        /// The explicitly-named sites and their names — everything the settings layer persists as
        /// <c>macro_name_&lt;profile&gt;_&lt;layer&gt;_&lt;trigger&gt;_&lt;slot&gt;</c>. Sites whose
        /// macro is unnamed are <b>absent</b>: a derived name is recomputed on every load and is
        /// never written (see <see cref="MacroNaming.DeriveDisplayName"/>). Hand the whole list to
        /// <c>AppSettings.WithMacroNamesForProfile</c>, which turns everything missing from it into
        /// a removal.
        /// </summary>
        public IReadOnlyList<KeyValuePair<MacroSite, string>> EnumerateStoredNames()
        {
            var names = new List<KeyValuePair<MacroSite, string>>();

            foreach (var entry in Entries)
            {
                if (!entry.IsExplicitlyNamed)
                {
                    continue;
                }

                foreach (var site in entry.Sites)
                {
                    names.Add(KeyValuePair.Create(site, entry.Name));
                }
            }

            return names;
        }

        private void EnsureOwned(MacroLibraryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            foreach (var candidate in Entries)
            {
                if (ReferenceEquals(candidate, entry))
                {
                    return;
                }
            }

            throw new ArgumentException(
                "The entry does not belong to this library's current snapshot; take a fresh one from Entries.",
                nameof(entry));
        }

        private KeyboardLayer? FindLayerOf(KeyboardKey key)
        {
            foreach (var layer in Layout.Layers)
            {
                foreach (var candidate in layer.Keys)
                {
                    if (ReferenceEquals(candidate, key))
                    {
                        return layer;
                    }
                }
            }

            return null;
        }
    }
}
