using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The advisory fan-out on its own — <b>no editor, no profile session and no drive</b>. It
    /// builds the set from a layout, marks the caps it names, tells each layer how many of its
    /// positions carry one and hands the strip the slice the open section is about; all four used to
    /// be one private method of <c>KeyboardEditorViewModel</c>.
    /// <para>
    /// What the suite is really pinning is invariant 21 — <b>every advisory appears exactly
    /// twice</b>, a mark on the exact key and one summary strip per section — and that a rebuild
    /// always re-projects, which the projection now guarantees structurally rather than by
    /// remembering to. The set is the projection's own, so a projection never has to be handed back
    /// what it just produced.
    /// </para>
    /// </summary>
    public class EditorAdvisoryProjectionTests
    {
        [AvaloniaFact]
        public void Rebuild_ReadsTheLayout_AndKeepsTheSet()
        {
            // The projection owns the set; the editor publishes this very property as `Advisories`,
            // which the save toast counts and the key inspector is refreshed from.
            var scene = new Scene(CreateLayoutWithADuplicate());

            Assert.Equal(0, scene.Projection.Advisories.Total);

            var advisories = scene.Rebuild();

            Assert.True(advisories.Total > 0);
            Assert.Equal(EditorAdvisories.Build(scene.Layout).Total, advisories.Total);
        }

        [AvaloniaFact]
        public void Rebuild_ReportsWhetherTheSetMoved_WhichIsTheEditorsCueToAnnounceIt()
        {
            // The editor raises `Advisories` off this answer, so it has to be the identity test the
            // property's SetProperty used to make: EditorAdvisories is immutable and rebuilt whole,
            // and a layout with nothing to say hands back the one Empty instance every time.
            var scene = new Scene(CreateLayoutWithADuplicate());

            Assert.True(scene.Projection.Rebuild(scene.Layout, scene.Layers, EditorTab.Keys, scene.Layers[0].Index));
            Assert.True(scene.Projection.Rebuild(scene.Layout, scene.Layers, EditorTab.Keys, scene.Layers[0].Index));

            Assert.True(scene.Projection.Rebuild(layout: null, scene.Layers, EditorTab.Keys, layerIndex: 0));
            Assert.False(scene.Projection.Rebuild(layout: null, scene.Layers, EditorTab.Keys, layerIndex: 0));
        }

        [AvaloniaFact]
        public void Rebuild_MarksEveryCapTheSetNames_AndOnlyThose()
        {
            // Appearance one of the two: a mark on the exact key. A duplicate token produces one
            // advisory per position of the group, so both caps of the pair carry it.
            var scene = new Scene(CreateLayoutWithADuplicate());

            var advisories = scene.Rebuild();

            foreach (var layer in scene.Layers)
            {
                foreach (var key in layer.Keys)
                {
                    Assert.Equal(advisories.HasAdvisoryForKey(layer.Index, key.Index), key.HasAdvisory);
                }
            }

            Assert.Contains(scene.Layers[0].Keys, key => key.HasAdvisory);
        }

        [AvaloniaFact]
        public void Rebuild_PushesEachLayersTally_WhichTheLayerCannotDeriveItself()
        {
            var scene = new Scene(CreateLayoutWithADuplicate());

            var advisories = scene.Rebuild();

            foreach (var layer in scene.Layers)
            {
                Assert.Equal(advisories.CountForLayer(layer.Index), layer.AdvisoryCount);
            }

            Assert.True(scene.Layers[0].AdvisoryCount > 0);
            Assert.Equal(0, scene.Layers[1].AdvisoryCount);
        }

        [AvaloniaFact]
        public void Rebuild_AlwaysEndsInAProjection()
        {
            // Appearance two: the summary strip. It used to hold only because RebuildAdvisories
            // remembered to call RefreshAdvisorySummary afterwards; Rebuild ends in Project itself.
            var scene = new Scene(CreateLayoutWithADuplicate());

            var advisories = scene.Rebuild();

            Assert.Equal(
                advisories.ForTab(EditorTab.Keys, scene.Layers[0].Index).Count,
                scene.Strip.AdvisoryCount);
            Assert.Equal(
                advisories.SummaryFor(EditorTab.Keys, scene.Layers[0].Index),
                scene.Strip.AdvisorySummary);
            Assert.True(scene.Strip.HasAdvisories);
        }

        [AvaloniaFact]
        public void Rebuild_AfterAMutationThatClearedIt_TakesTheMarksBackOff()
        {
            // Core announces nothing, so the set is replaced whole rather than mutated — and the
            // per-cap flag has to come off with it, or the board keeps an amber that is over.
            var scene = new Scene(CreateLayoutWithADuplicate());

            scene.Rebuild();

            Assert.Contains(scene.Layers[0].Keys, key => key.HasAdvisory);

            scene.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ClearRemap();

            var advisories = scene.Rebuild();

            Assert.Equal(0, advisories.Total);
            Assert.DoesNotContain(scene.Layers[0].Keys, key => key.HasAdvisory);
            Assert.Equal(0, scene.Layers[0].AdvisoryCount);
            Assert.False(scene.Strip.HasAdvisories);
        }

        [AvaloniaFact]
        public void Rebuild_WithNoLayoutAtAll_ProducesAnEmptySet()
        {
            var scene = new Scene(CreateLayoutWithADuplicate());

            scene.Projection.Rebuild(layout: null, scene.Layers, EditorTab.Keys, layerIndex: 0);

            Assert.Equal(0, scene.Projection.Advisories.Total);
            Assert.False(scene.Strip.HasAdvisories);
        }

        [AvaloniaFact]
        public void Project_NarrowsAnExistingSetToTheOpenSection_WithoutRebuildingIt()
        {
            // The cheap half: a tab or layer switch moves what the strip is *about* and writes
            // nothing, which is why it does not go through Build's walk of every key of every layer.
            var scene = new Scene(CreateLayoutWithADuplicate());
            var advisories = scene.Rebuild();

            Assert.True(scene.Strip.HasAdvisories);

            scene.Projection.Project(EditorTab.Settings, scene.Layers[0].Index);

            Assert.Equal(0, scene.Strip.AdvisoryCount);
            Assert.Equal(string.Empty, scene.Strip.AdvisorySummary);
            Assert.False(scene.Strip.HasAdvisories);

            // The duplicates are on the first layer, so the second one has nothing to say either.
            scene.Projection.Project(EditorTab.Keys, scene.Layers[1].Index);

            Assert.Equal(0, scene.Strip.AdvisoryCount);

            scene.Projection.Project(EditorTab.Keys, scene.Layers[0].Index);

            Assert.Equal(advisories.ForTab(EditorTab.Keys, scene.Layers[0].Index).Count, scene.Strip.AdvisoryCount);

            // The set itself did not move: the tab and the layer decide what the strip is *about*.
            Assert.Same(advisories, scene.Projection.Advisories);
        }

        [AvaloniaFact]
        public void Project_LeavesTheCapsAlone()
        {
            // It is the *cheap* half, and a projection that also re-marked the board would be the
            // rebuild it deliberately is not.
            var scene = new Scene(CreateLayoutWithADuplicate());

            scene.Rebuild();

            var marked = scene.Layers[0].Keys.Count(key => key.HasAdvisory);

            scene.Projection.Project(EditorTab.Settings, scene.Layers[0].Index);

            Assert.False(scene.Strip.HasAdvisories);
            Assert.Equal(marked, scene.Layers[0].Keys.Count(key => key.HasAdvisory));
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingStrip()
        {
            Assert.Throws<ArgumentNullException>(() => new EditorAdvisoryProjection(null!));
        }

        [AvaloniaFact]
        public void Rebuild_RefusesAMissingLayerCollection()
        {
            var scene = new Scene(CreateLayoutWithADuplicate());

            Assert.Throws<ArgumentNullException>(
                () => scene.Projection.Rebuild(scene.Layout, null!, EditorTab.Keys, layerIndex: 0));
        }

        /// <summary>
        /// A layout whose first layer carries one duplicated token — the cheapest fixture that
        /// produces a board-anchored advisory on one layer and none on the other.
        /// </summary>
        private static KeyboardLayout CreateLayoutWithADuplicate()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];

            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layer.Keys[0].OriginalKey);

            return layout;
        }

        /// <summary>A projection over a real RGB model, a real strip, and nothing else.</summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public IReadOnlyList<KeyboardLayerViewModel> Layers { get; }

            public AdvisoryStripViewModel Strip { get; }

            public EditorAdvisoryProjection Projection { get; }

            public Scene(KeyboardLayout layout)
            {
                Layout = layout;
                Layers = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null);

                Strip = new AdvisoryStripViewModel(_ => { }, _ => { });
                Projection = new EditorAdvisoryProjection(Strip);
            }

            /// <summary>
            /// Rebuilds onto the Layout tab and the first layer — the editor's own funnel — and
            /// hands back the set the projection now holds.
            /// </summary>
            public EditorAdvisories Rebuild()
            {
                Projection.Rebuild(Layout, Layers, EditorTab.Keys, Layers[0].Index);

                return Projection.Advisories;
            }
        }
    }
}
