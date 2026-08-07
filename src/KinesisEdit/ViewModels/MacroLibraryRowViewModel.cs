using System.Text;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One logical macro of the profile as the Macros tab lists it — <b>once, by name</b>, whatever
    /// number of keys and layers fire it (mockup <c>2i</c>: "every macro has a name, so the tab
    /// lists them once: rename, see which keys and layers fire each one, duplicate, delete").
    /// <para>
    /// It is built from a <see cref="MacroLibraryEntry"/> and never from a walk over the layout's
    /// keys, because the entry <em>is</em> the answer to "which keys and layers fire this one".
    /// <see cref="MacroLibrary.Entries"/> is a snapshot: every mutation rebuilds it, so these rows
    /// are rebuilt with it rather than re-marked, and a row held across an edit is stale.
    /// </para>
    /// <para>
    /// <b>Mono is the file's voice.</b> The keystrokes, the trigger tokens and the length are values
    /// that exist verbatim in <c>layoutN.txt</c> or are counted against it; the macro's <em>name</em>
    /// is not — it lives in <c>app_settings.txt</c> and is the app speaking — so it is sans.
    /// </para>
    /// </summary>
    public sealed class MacroLibraryRowViewModel : ViewModelBase
    {
        /// <summary>Between a site's token and its layer: <c>[f3] · Fn</c>.</summary>
        public const string SiteSeparator = " · ";

        /// <summary>Between two sites of the same macro.</summary>
        public const string SiteJoin = ", ";

        /// <summary>Between a co-trigger and the trigger it is held with (06 §5).</summary>
        public const string CoTriggerSeparator = " + ";

        /// <summary>Opens the "which keys and layers fire it" line. This app's wording.</summary>
        public const string FiresFromPrefix = "Fires from ";

        /// <summary>What a flat-list macro with no trigger yet reads as (06 §1).</summary>
        public const string UnassignedTriggerCaption = "Unassigned";

        /// <summary>The library entry this row shows. Stale after any library mutation.</summary>
        public MacroLibraryEntry Entry { get; }

        /// <summary>What the macro is called — its stored name, or the one Core derives from it.</summary>
        public string Name { get; }

        /// <summary>Whether the name was stored rather than derived from the macro's content.</summary>
        public bool IsExplicitlyNamed => Entry.IsExplicitlyNamed;

        /// <summary>The keystrokes as the layout file carries them (06 §3), mono.</summary>
        public string ContentsText { get; }

        /// <summary>Every trigger this macro fires from, co-triggers included: <c>[lshft] + [f3]</c>.</summary>
        public string TriggerText { get; }

        /// <summary>Every layer it fires on.</summary>
        public string LayerText { get; }

        /// <summary>
        /// The macro's length in the <b>device's own</b> per-macro metric — weighted keystrokes
        /// everywhere but the Advantage360, whose 500 counts serialized macro-text characters
        /// (<see cref="MacroLengthMetric"/>, the same code <c>KeyboardLayout.Validate</c> uses).
        /// </summary>
        public int Length { get; }

        /// <summary>That length, grouped the way every count in the app is.</summary>
        public string LengthText { get; }

        /// <summary>Whether it is past the device's per-macro cap. Amber, and it still saves.</summary>
        public bool IsOverBudget { get; }

        /// <summary>
        /// The budget advisory, verbatim from mockup <c>1i</c> — "512 of 500 characters — over the
        /// device budget. Saved as-is." — or an empty string. It is
        /// <see cref="AdvisoryText.MacroCharacters"/>'s, not a second spelling of it.
        /// </summary>
        public string BudgetAdvisory { get; }

        /// <summary>"[f3] · Fn, [f7] · Fn" — every place this macro fires from, bare.</summary>
        public string SiteListText { get; }

        /// <summary>The same, opened by <see cref="FiresFromPrefix"/> — the line a row draws.</summary>
        public string SitesText { get; }

        /// <summary>How many keys/layers fire this macro.</summary>
        public int SiteCount => Entry.SiteCount;

        /// <summary>Whether more than one does.</summary>
        public bool HasSeveralSites => Entry.SiteCount > 1;

        /// <summary>The layers this macro fires on, for the tab's layer filter.</summary>
        public IReadOnlyList<int> LayerIndexes { get; }

        /// <summary>Whether this row's name is being edited in place. Only ever one row at a time.</summary>
        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetProperty(ref _isRenaming, value);
        }

        /// <summary>
        /// The name being typed. Bound two-way to the row's field; <b>blank clears</b> the stored
        /// name, so the macro goes back to the one Core derives from its content.
        /// </summary>
        public string RenameText
        {
            get => _renameText;
            set => SetProperty(ref _renameText, value);
        }

        private readonly string _haystack;
        private string _renameText;
        private bool _isRenaming;

        /// <summary>
        /// Renders <paramref name="entry"/> for <paramref name="layout"/>, against the device's
        /// per-macro cap (<paramref name="maxMacroLength"/>; null where the device states none).
        /// </summary>
        public MacroLibraryRowViewModel(MacroLibraryEntry entry, KeyboardLayout layout, int? maxMacroLength)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));

            ArgumentNullException.ThrowIfNull(layout);

            var dialect = layout.Dialect;

            Name = entry.Name;
            _renameText = entry.IsExplicitlyNamed ? entry.Name : string.Empty;

            ContentsText = MacroKeystrokeRenderer.RenderKeystrokes(entry.Canonical, dialect);
            TriggerText = BuildTriggers(entry, dialect);
            LayerText = BuildLayers(entry, layout);
            LayerIndexes = BuildLayerIndexes(entry);
            SiteListText = BuildSites(entry, layout, dialect);
            SitesText = SiteListText.Length > 0 ? FiresFromPrefix + SiteListText : string.Empty;

            Length = MacroLengthMetric.Measure(entry.Canonical, layout);
            LengthText = AdvisoryText.Number(Length);
            IsOverBudget = maxMacroLength is int limit && Length > limit;
            BudgetAdvisory = IsOverBudget && maxMacroLength is int cap
                ? AdvisoryText.MacroCharacters(Length, cap)
                : string.Empty;

            // Built once rather than per keystroke of the query: the tab filters on every character
            // the user types, and the answer cannot move while the snapshot stands.
            _haystack = BuildHaystack(entry, Name, ContentsText, TriggerText, LayerText);
        }

        /// <summary>
        /// Whether this row answers <paramref name="query"/> — matched against the <b>name, the
        /// trigger and the contents</b> (mockup <c>1i</c>: "Search name, trigger, or contents…"),
        /// case-insensitively. An empty query matches everything.
        /// </summary>
        public bool Matches(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return _haystack.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Whether this macro fires on <paramref name="layerIndex"/>; null is "any layer".</summary>
        public bool IsOnLayer(int? layerIndex)
        {
            if (layerIndex is not int index)
            {
                return true;
            }

            foreach (var candidate in LayerIndexes)
            {
                if (candidate == index)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Puts the field back to what the macro is called now — the rename's Cancel.</summary>
        public void ResetRenameText()
        {
            RenameText = Entry.IsExplicitlyNamed ? Entry.Name : string.Empty;
        }

        private static string BuildTriggers(MacroLibraryEntry entry, TokenDialect dialect)
        {
            var builder = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var site in entry.Sites)
            {
                var trigger = DescribeTrigger(site, dialect);

                if (!seen.Add(trigger))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(SiteJoin);
                }

                builder.Append(trigger);
            }

            return builder.ToString();
        }

        /// <summary>
        /// One site's trigger, co-triggers first (06 §5: "the trigger is the key plus its co-trigger
        /// set"). Both halves are file tokens, so both are mono — mockup <c>1i</c> draws the
        /// modifiers as <c>⇧</c>, which is in neither embedded family; see the deviation note in
        /// docs/app/design-system.md.
        /// </summary>
        private static string DescribeTrigger(MacroSite site, TokenDialect dialect)
        {
            var parts = new List<string>();

            foreach (var coTrigger in site.Macro.CoTriggers)
            {
                parts.Add(KeyboardKeyViewModel.FormatToken(coTrigger, dialect));
            }

            var trigger = KeyRegistry.FindByCode(site.TriggerKeyCode);

            parts.Add(trigger is null
                ? UnassignedTriggerCaption
                : KeyboardKeyViewModel.FormatToken(trigger, dialect));

            return string.Join(CoTriggerSeparator, parts);
        }

        private static string BuildLayers(MacroLibraryEntry entry, KeyboardLayout layout)
        {
            var builder = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var site in entry.Sites)
            {
                var caption = DescribeLayer(site.LayerIndex, layout);

                if (caption.Length == 0 || !seen.Add(caption))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(SiteJoin);
                }

                builder.Append(caption);
            }

            return builder.ToString();
        }

        private static string DescribeLayer(int layerIndex, KeyboardLayout layout)
        {
            return layerIndex >= 0 && layerIndex < layout.Layers.Count
                ? LayerCaptions.ForLayer(layout.Layers[layerIndex], layout.Dialect)
                : string.Empty;
        }

        private static IReadOnlyList<int> BuildLayerIndexes(MacroLibraryEntry entry)
        {
            var indexes = new List<int>();

            foreach (var site in entry.Sites)
            {
                if (!indexes.Contains(site.LayerIndex))
                {
                    indexes.Add(site.LayerIndex);
                }
            }

            return indexes;
        }

        private static string BuildSites(MacroLibraryEntry entry, KeyboardLayout layout, TokenDialect dialect)
        {
            var builder = new StringBuilder();

            foreach (var site in entry.Sites)
            {
                if (builder.Length > 0)
                {
                    builder.Append(SiteJoin);
                }

                var layer = DescribeLayer(site.LayerIndex, layout);

                builder.Append(DescribeTrigger(site, dialect));

                if (layer.Length > 0)
                {
                    builder.Append(SiteSeparator).Append(layer);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// What the search reads. The <b>typed text</b> joins the rendered tokens deliberately:
        /// <c>{b}{e}{s}{t}</c> is what the file carries, and a user searching a macro by what it
        /// types would never find it otherwise (<see cref="MacroNaming.DeriveTypedText"/>).
        /// </summary>
        private static string BuildHaystack(
            MacroLibraryEntry entry,
            string name,
            string contents,
            string triggers,
            string layers)
        {
            return string.Join(
                '\n',
                name,
                contents,
                MacroNaming.DeriveTypedText(entry.Canonical),
                triggers,
                layers);
        }
    }
}
