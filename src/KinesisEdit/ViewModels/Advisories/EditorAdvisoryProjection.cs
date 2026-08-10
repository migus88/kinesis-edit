using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels.Advisories
{
    /// <summary>
    /// The fan-out of the advisory set: build it from the layout, mark the caps it names, tell each
    /// layer how many of its positions carry one, and hand the strip the slice the open section is
    /// about.
    /// <para>
    /// It is a type of its own rather than two more methods on <see cref="KeyboardEditorViewModel"/>
    /// — already the largest class in the app, which docs/guides/Coding Conventions.md forbids
    /// growing into a god class — and what earns it the file is invariant 21: <b>every advisory
    /// appears exactly twice</b>, a mark on the exact key and one summary strip per section. Both
    /// appearances are written here, in one method, so a source that grows a third surface or loses
    /// its second has one place to be wrong rather than two.
    /// </para>
    /// <para>
    /// <b>A rebuild always re-projects.</b> That used to be true only because
    /// <c>RebuildAdvisories</c> remembered to call <c>RefreshAdvisorySummary</c> after it;
    /// <see cref="Rebuild"/> ends in <see cref="Project"/> itself, which makes it structural. The
    /// cheap half stays separate because a tab or layer switch moves what the strip is <em>about</em>
    /// without touching the model, and <see cref="EditorAdvisories.Build"/> walks every key of every
    /// layer.
    /// </para>
    /// <para>
    /// <b>It owns the set.</b> Nothing else holds one: the editor's <c>Advisories</c> reads through
    /// to <see cref="Advisories"/>, and the key inspector is refreshed from that same property. A
    /// projection that built a set and handed it away would leave the cheap half —
    /// <see cref="Project"/> on a tab or layer switch — being passed back the very set it had just
    /// produced, which is a round trip with two places to go stale rather than one owner.
    /// <b>It is still not a view model:</b> the editor publishes the set under its own name, so a
    /// view never sees this type, and the strip it projects onto is the one bindable surface —
    /// <see cref="AdvisoryStripViewModel"/>'s.
    /// </para>
    /// <para>
    /// <b>Nothing here blocks anything</b> (docs/design/README.md, "advisories never block"): it is
    /// reachable from no <c>CanExecute</c>, it clamps nothing, and an over-budget layout is written
    /// as it stands.
    /// </para>
    /// </summary>
    public sealed class EditorAdvisoryProjection
    {
        /// <summary>
        /// What the app has noticed about the open profile right now — the set the editor publishes
        /// as its own <c>Advisories</c>, the save toast counts and the key inspector is refreshed
        /// from. <see cref="EditorAdvisories.Empty"/> until the first <see cref="Rebuild"/>, which
        /// is what a device with no model and a clean profile both have.
        /// </summary>
        public EditorAdvisories Advisories { get; private set; } = EditorAdvisories.Empty;

        private readonly AdvisoryStripViewModel _strip;

        /// <summary>Creates the fan-out over the strip that shows the summary half of it.</summary>
        public EditorAdvisoryProjection(AdvisoryStripViewModel strip)
        {
            _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        }

        /// <summary>
        /// Re-reads what the app has to say about <paramref name="layout"/> and pushes it everywhere
        /// it is shown: the per-key flag, each layer's tally, and the strip's own count and
        /// sentence. <b>True when the set actually moved</b>, which is the editor's cue to announce
        /// <c>Advisories</c> — identity is the whole test, because
        /// <see cref="EditorAdvisories"/> is immutable and rebuilt whole, and a layout that produced
        /// nothing to say hands back the one <see cref="EditorAdvisories.Empty"/> instance.
        /// <para>
        /// Called from the editor's <c>RefreshCounters()</c> — the funnel every layout-mutation path
        /// already ends in — and deliberately <b>not</b> from its dirty-state refresh: the set is
        /// derived from the <see cref="KeyboardLayout"/> alone, the only path that refreshes the
        /// dirty state on its own is a lighting write, and <see cref="KeyboardLayout.Validate"/>
        /// walks every key of every layer. Paying for that on each click of the colour picker would
        /// be felt for a set that cannot have changed.
        /// </para>
        /// </summary>
        public bool Rebuild(
            KeyboardLayout? layout,
            IReadOnlyList<KeyboardLayerViewModel> layers,
            EditorTab tab,
            int? layerIndex)
        {
            ArgumentNullException.ThrowIfNull(layers);

            var previous = Advisories;
            var advisories = EditorAdvisories.Build(layout);

            Advisories = advisories;

            foreach (var layer in layers)
            {
                foreach (var key in layer.Keys)
                {
                    key.HasAdvisory = advisories.HasAdvisoryForKey(layer.Index, key.Index);
                }

                // The one count the layer cannot derive from its own caps: the fact lives out here,
                // in EditorAdvisories, so it is pushed in exactly like the per-cap flag above. It is
                // an aggregate of what the strip already says, not a third place an advisory is
                // reported (invariant 21).
                layer.AdvisoryCount = advisories.CountForLayer(layer.Index);
            }

            Project(tab, layerIndex);

            return !ReferenceEquals(previous, advisories);
        }

        /// <summary>
        /// Hands the strip the current set narrowed to the open section, without rebuilding it: the
        /// tab and the layer decide which advisories the strip is about, and neither changes the
        /// model. This is the whole of a tab switch's and a layer switch's advisory work.
        /// </summary>
        public void Project(EditorTab tab, int? layerIndex)
        {
            _strip.Project(Advisories, tab, layerIndex);
        }
    }
}
