namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Turns a layout's macro stores into the library's entries: the site walk and the load-time
    /// reconciliation of names. Kept out of <see cref="MacroLibrary"/> so the library itself only
    /// owns state and edits.
    /// </summary>
    internal static class MacroLibraryBuilder
    {
        /// <summary>
        /// Every place a macro sits, in a fixed order: layers by index, keys by index, slots 1..5,
        /// and then <see cref="KeyboardLayout.Macros"/> in list order. Covers exactly the macros
        /// <see cref="KeyboardLayout.EnumerateMacros"/> yields, except that one instance parked at
        /// two places yields <b>two sites</b> — it really is on two positions.
        /// <para>
        /// TKO edge zones are lighting zones, not typing keys (05 §4.4), so they are outside this
        /// walk exactly as they are outside every layout counter.
        /// </para>
        /// </summary>
        public static IReadOnlyList<MacroSite> EnumerateSites(KeyboardLayout layout)
        {
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
                    SlotNumber = 0,
                    Macro = macro
                });
            }

            return sites;
        }

        /// <summary>
        /// Groups the sites into logical macros and hands each group a unique display name.
        /// <list type="bullet">
        /// <item>Sites group together when their macros carry the <b>same name</b>
        /// (<see cref="MacroNaming.AreSameName"/>), are both named or both unnamed, <b>and</b> hold
        /// equivalent content (<see cref="Macro.IsEquivalentTo"/>). Same name + different content is
        /// two macros, kept apart with a <c>" (2)"</c> suffix — never merged, never dropped.</item>
        /// <item>An unnamed macro takes <see cref="MacroNaming.DeriveDisplayName"/>; a derived name
        /// that collides is disambiguated the same way.</item>
        /// <item>Order is the site order above, so the reconciliation is deterministic: the same
        /// layout always produces the same entries with the same names.</item>
        /// </list>
        /// A group that was <em>explicitly</em> named has its resolved name stamped back onto every
        /// one of its macros, so the disambiguation survives the next save; a derived name is never
        /// stamped, which keeps <see cref="Macro.Name"/> empty and stops it reaching disk.
        /// </summary>
        public static IReadOnlyList<MacroLibraryEntry> Build(KeyboardLayout layout)
        {
            var groups = GroupSites(layout);
            var takenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<MacroLibraryEntry>(groups.Count);

            foreach (var group in groups)
            {
                var name = MakeUnique(group.BaseName, takenNames);

                takenNames.Add(name);

                if (group.IsExplicitlyNamed)
                {
                    StampName(group, name);
                }

                entries.Add(new MacroLibraryEntry(name, group.IsExplicitlyNamed, group.Sites));
            }

            return entries;
        }

        /// <summary>
        /// The first spelling of <paramref name="baseName"/> that <paramref name="takenNames"/> does
        /// not already hold, per <see cref="MacroNaming.Disambiguate"/>.
        /// </summary>
        public static string MakeUnique(string baseName, HashSet<string> takenNames)
        {
            for (var ordinal = 1; ; ordinal++)
            {
                var candidate = MacroNaming.Disambiguate(baseName, ordinal);

                if (!takenNames.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        private static List<SiteGroup> GroupSites(KeyboardLayout layout)
        {
            var groups = new List<SiteGroup>();

            foreach (var site in EnumerateSites(layout))
            {
                var storedName = MacroNaming.Sanitize(site.Macro.Name);
                var isExplicitlyNamed = storedName.Length > 0;
                var baseName = isExplicitlyNamed
                    ? storedName
                    : MacroNaming.DeriveDisplayName(site.Macro, layout);

                var group = FindGroup(groups, baseName, isExplicitlyNamed, site.Macro);

                if (group is null)
                {
                    group = new SiteGroup(baseName, isExplicitlyNamed);
                    groups.Add(group);
                }

                group.Sites.Add(site);
            }

            return groups;
        }

        private static SiteGroup? FindGroup(
            List<SiteGroup> groups,
            string baseName,
            bool isExplicitlyNamed,
            Macro macro)
        {
            foreach (var group in groups)
            {
                if (group.IsExplicitlyNamed == isExplicitlyNamed
                    && MacroNaming.AreSameName(group.BaseName, baseName)
                    && group.Sites[0].Macro.IsEquivalentTo(macro))
                {
                    return group;
                }
            }

            return null;
        }

        private static void StampName(SiteGroup group, string name)
        {
            foreach (var site in group.Sites)
            {
                site.Macro.Name = name;
            }
        }

        private sealed class SiteGroup
        {
            public string BaseName { get; }

            public bool IsExplicitlyNamed { get; }

            public List<MacroSite> Sites { get; } = [];

            public SiteGroup(string baseName, bool isExplicitlyNamed)
            {
                BaseName = baseName;
                IsExplicitlyNamed = isExplicitlyNamed;
            }
        }
    }
}
