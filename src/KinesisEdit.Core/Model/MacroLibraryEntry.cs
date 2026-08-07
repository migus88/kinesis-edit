namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// One logical macro of a layout and every place it fires from — the row the Macros tab shows
    /// once, however many keys carry it. Immutable snapshot: rebuilt by
    /// <see cref="MacroLibrary.Refresh"/> after every edit, so an entry held across a mutation is
    /// stale and the library refuses it.
    /// </summary>
    public sealed class MacroLibraryEntry
    {
        /// <summary>
        /// The name to show, never empty and unique within the library. Either the stored
        /// <see cref="Macro.Name"/> or, when the macro is unnamed, the derived name of
        /// <see cref="MacroNaming.DeriveDisplayName"/> — with a <c>" (2)"</c>-style suffix when
        /// another entry already took the name (see <see cref="MacroLibrary"/>).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Whether <see cref="Name"/> came from <see cref="Macro.Name"/> (true) or was derived from
        /// the macro's content (false). Only an explicit name is written to
        /// <c>app_settings.txt</c>; a derived one is recomputed every load.
        /// </summary>
        public bool IsExplicitlyNamed { get; }

        /// <summary>
        /// The instance every other site is rewritten from — the macro of the first
        /// <see cref="Sites"/> entry. Edits go here, then <see cref="MacroLibrary.Propagate"/>.
        /// </summary>
        public Macro Canonical { get; }

        /// <summary>
        /// Every place this macro fires from, in layout order (layers, then keys, then slots, then
        /// the flat list). Always at least one.
        /// </summary>
        public IReadOnlyList<MacroSite> Sites { get; }

        /// <summary>How many keys/layers fire this macro — the "Also on [f7] · Fn" count.</summary>
        public int SiteCount => Sites.Count;

        /// <summary>Creates an entry; <paramref name="sites"/> must be non-empty.</summary>
        public MacroLibraryEntry(string name, bool isExplicitlyNamed, IReadOnlyList<MacroSite> sites)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(sites);

            if (sites.Count == 0)
            {
                throw new ArgumentException("A macro library entry needs at least one site.", nameof(sites));
            }

            Name = name;
            IsExplicitlyNamed = isExplicitlyNamed;
            Sites = sites;
            Canonical = sites[0].Macro;
        }
    }
}
