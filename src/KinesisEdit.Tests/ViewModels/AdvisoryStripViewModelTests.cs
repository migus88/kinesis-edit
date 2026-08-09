using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The advisory strip on its own — <b>no editor, no profile session, no drive and no Avalonia
    /// runtime</b>. That is the point of the type existing: the strip's whole state used to live on
    /// <c>KeyboardEditorViewModel</c>, so every claim about the count, the sentence and the
    /// <c>Review N</c> walk had to be made through a loaded editor.
    /// <para>
    /// Selecting what a note is about is a pair of callbacks, so this suite watches the anchors the
    /// strip hands out rather than a board it does not own.
    /// </para>
    /// </summary>
    public sealed class AdvisoryStripViewModelTests
    {
        [Fact]
        public void ANewStrip_HasNothingToSay()
        {
            var strip = new AdvisoryStripViewModel(_ => { }, _ => { });

            Assert.Equal(0, strip.AdvisoryCount);
            Assert.Equal(string.Empty, strip.AdvisorySummary);
            Assert.False(strip.HasAdvisories);
            Assert.Equal(AdvisoryText.Review(0), strip.ReviewCaption);
        }

        [Fact]
        public void Project_NarrowsTheSetToTheOpenSection()
        {
            var layout = CreateLayoutWithADuplicate(out var layerIndex);
            var advisories = EditorAdvisories.Build(layout);
            var strip = new AdvisoryStripViewModel(_ => { }, _ => { });

            strip.Project(advisories, EditorTab.Keys, layerIndex);

            Assert.Equal(advisories.ForTab(EditorTab.Keys, layerIndex).Count, strip.AdvisoryCount);
            Assert.True(strip.HasAdvisories);
            Assert.Equal(advisories.SummaryFor(EditorTab.Keys, layerIndex), strip.AdvisorySummary);
            Assert.Equal(AdvisoryText.Review(strip.AdvisoryCount), strip.ReviewCaption);

            // The duplicates are the Layout tab's, so the Settings tab has nothing to say and the
            // strip collapses — one control, one row, every tab.
            strip.Project(advisories, EditorTab.Settings, layerIndex);

            Assert.Equal(0, strip.AdvisoryCount);
            Assert.Equal(string.Empty, strip.AdvisorySummary);
            Assert.False(strip.HasAdvisories);
        }

        [Fact]
        public void Project_RaisesTheThreeBoundProperties()
        {
            // HasAdvisories and ReviewCaption are derived from the count, so the strip has to say
            // so itself or the row would stay collapsed with a note in it.
            var layout = CreateLayoutWithADuplicate(out var layerIndex);
            var strip = new AdvisoryStripViewModel(_ => { }, _ => { });
            var changed = new List<string>();

            strip.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

            strip.Project(EditorAdvisories.Build(layout), EditorTab.Keys, layerIndex);

            Assert.Contains(nameof(AdvisoryStripViewModel.AdvisoryCount), changed);
            Assert.Contains(nameof(AdvisoryStripViewModel.HasAdvisories), changed);
            Assert.Contains(nameof(AdvisoryStripViewModel.ReviewCaption), changed);
            Assert.Contains(nameof(AdvisoryStripViewModel.AdvisorySummary), changed);
        }

        [Fact]
        public void ReviewN_HandsOutEveryAnchoredKeyInTurnAndWrapsRound()
        {
            var layout = CreateLayoutWithADuplicate(out var layerIndex);
            var advisories = EditorAdvisories.Build(layout);
            var keys = new List<int?>();
            var macros = new List<AdvisoryAnchor>();
            var strip = new AdvisoryStripViewModel(anchor => keys.Add(anchor.KeyIndex), macros.Add);

            strip.Project(advisories, EditorTab.Keys, layerIndex);

            var expected = advisories
                .ForTab(EditorTab.Keys, layerIndex)
                .Where(advisory => advisory.KeyIndex is not null)
                .Select(advisory => advisory.KeyIndex)
                .ToArray();

            Assert.True(expected.Length >= 2, $"The fixture anchored only {expected.Length} advisories.");

            for (var press = 0; press < expected.Length; press++)
            {
                strip.ReviewAdvisoriesCommand.Execute(null);
            }

            Assert.Equal(expected, keys);

            // It wraps rather than stopping at the end.
            strip.ReviewAdvisoriesCommand.Execute(null);

            Assert.Equal(expected[0], keys[^1]);

            // And the macro callback is never the one the Layout tab reaches.
            Assert.Empty(macros);
        }

        [Fact]
        public void ReviewN_ForAMacroPanelAnchor_ReachesTheMacroCallbackInstead()
        {
            // Issue #140: the tab is no longer the discriminator. Both kinds of advisory arrive on
            // EditorTab.Keys now, so this asserts the surface does the dispatching — a macro budget
            // reaches the macro callback FROM THE LAYOUT TAB.
            var layout = CreateLayoutWithAnOverCoTriggeredMacro(out var layer, out var key);
            var advisories = EditorAdvisories.Build(layout);
            var keys = new List<AdvisoryAnchor>();
            var macros = new List<AdvisoryAnchor>();
            var strip = new AdvisoryStripViewModel(keys.Add, macros.Add);

            strip.Project(advisories, EditorTab.Keys, layer);

            Assert.True(strip.HasAdvisories, "The fixture produced no macro advisory.");

            strip.ReviewAdvisoriesCommand.Execute(null);

            var anchor = Assert.Single(macros);

            Assert.Empty(keys);
            Assert.Equal(EditorTab.Keys, anchor.Tab);
            Assert.Equal(AdvisorySurface.MacroPanel, anchor.Surface);
            Assert.Equal(key, anchor.KeyIndex);
            Assert.Equal(1, anchor.MacroIndex);
        }

        [Fact]
        public void ReviewN_OnTheLayoutTab_SplitsTheTwoKindsByTheirSurfaceAndNotByTheTab()
        {
            // Both kinds on one tab, in one walk: the board's duplicate-token notes reach key
            // selection and the macro budget reaches the Macro panel. Nothing but the anchor's own
            // Surface tells them apart now, so this is the case that pins the split.
            var layout = CreateLayoutWithAnOverCoTriggeredMacro(out var layerIndex, out var macroKey);
            var layer = layout.Layers[layerIndex];

            layer.Keys[TestLayouts.RgbDigitTwoKeyIndex].Remap(layer.Keys[0].OriginalKey);

            var advisories = EditorAdvisories.Build(layout);
            var keys = new List<AdvisoryAnchor>();
            var macros = new List<AdvisoryAnchor>();
            var strip = new AdvisoryStripViewModel(keys.Add, macros.Add);

            strip.Project(advisories, EditorTab.Keys, layerIndex);

            var targets = advisories
                .ForTab(EditorTab.Keys, layerIndex)
                .Where(advisory => advisory.KeyIndex is not null)
                .ToArray();

            Assert.Contains(targets, advisory => advisory.Surface == AdvisorySurface.Board);
            Assert.Contains(targets, advisory => advisory.Surface == AdvisorySurface.MacroPanel);

            for (var press = 0; press < targets.Length; press++)
            {
                strip.ReviewAdvisoriesCommand.Execute(null);
            }

            Assert.Equal(
                targets.Count(advisory => advisory.Surface == AdvisorySurface.MacroPanel),
                macros.Count);
            Assert.Equal(
                targets.Count(advisory => advisory.Surface == AdvisorySurface.Board),
                keys.Count);
            Assert.All(macros, anchor => Assert.Equal(AdvisorySurface.MacroPanel, anchor.Surface));
            Assert.All(keys, anchor => Assert.Equal(AdvisorySurface.Board, anchor.Surface));
            Assert.Contains(macros, anchor => anchor.KeyIndex == macroKey);
        }

        [Fact]
        public void Project_ForAFlatListMacroAdvisory_CountsItAndKeepsItOnTheMacroPanelSurface()
        {
            // THE CASE THE FIELD EXISTS FOR, at the strip. A flat-list board reports its macro
            // findings with no slot at all (06 §1), so inferring the surface from
            // `MacroIndex is not null` would call exactly these a board note — silently, on the one
            // board nobody can open yet.
            //
            // The strip still SHOWS it (its count and its sentence are the section's), and it never
            // reaches `Review N` — a flat-list finding names no position either, so `ReviewTargets`
            // filters it out and there is nothing for the walk to select. Both halves are asserted:
            // shown, and not selectable.
            var layout = CreateFlatListLayoutWithAnOverCoTriggeredMacro();
            var advisories = EditorAdvisories.Build(layout);
            var keys = new List<AdvisoryAnchor>();
            var macros = new List<AdvisoryAnchor>();
            var strip = new AdvisoryStripViewModel(keys.Add, macros.Add);

            var flat = Assert.Single(
                advisories.All,
                advisory => advisory.Surface == AdvisorySurface.MacroPanel && advisory.MacroIndex is null);

            Assert.Equal(EditorTab.Keys, flat.Tab);
            Assert.Null(flat.KeyIndex);

            strip.Project(advisories, EditorTab.Keys, flat.LayerIndex);

            Assert.True(strip.HasAdvisories, "The flat-list advisory was not shown by the strip.");

            strip.ReviewAdvisoriesCommand.Execute(null);

            Assert.Empty(keys);
            Assert.Empty(macros);
        }

        [Fact]
        public void ReviewN_WithNothingAnchored_IsANoOpAndIsNeverDisabled()
        {
            // "No advisory blocks, clamps or refuses anything": the one command in the editor with
            // no predicate at all.
            var reached = 0;
            var strip = new AdvisoryStripViewModel(_ => reached++, _ => reached++);

            Assert.True(strip.ReviewAdvisoriesCommand.CanExecute(null));

            strip.ReviewAdvisoriesCommand.Execute(null);

            Assert.Equal(0, reached);
        }

        [Fact]
        public void Project_RestartsTheReviewWalk()
        {
            // The walk walked a different list; carrying its index over would skip the first note
            // of the section the user just opened.
            var layout = CreateLayoutWithADuplicate(out var layerIndex);
            var advisories = EditorAdvisories.Build(layout);
            var keys = new List<int?>();
            var strip = new AdvisoryStripViewModel(anchor => keys.Add(anchor.KeyIndex), _ => { });

            strip.Project(advisories, EditorTab.Keys, layerIndex);
            strip.ReviewAdvisoriesCommand.Execute(null);
            strip.ReviewAdvisoriesCommand.Execute(null);

            strip.Project(advisories, EditorTab.Keys, layerIndex);
            strip.ReviewAdvisoriesCommand.Execute(null);

            Assert.Equal(keys[0], keys[^1]);
        }

        [Theory]
        [InlineData(null, 0, null)]
        [InlineData("Reload the profile.", 0, "Reload the profile.")]
        public void BuildPostSaveMessage_WithNoAdvisories_IsTheDevicesOwnWording(
            string? postSave,
            int advisoryCount,
            string? expected)
        {
            Assert.Equal(expected, AdvisoryStripViewModel.BuildPostSaveMessage(postSave, advisoryCount));
        }

        [Fact]
        public void BuildPostSaveMessage_WithAdvisories_AddsTheCountInTheSameBreath()
        {
            // The save happened — an advisory never stopped it — so the count is stated, not warned
            // about separately.
            Assert.Equal(
                "Reload the profile. " + AdvisoryText.SavedWithAdvisories(3),
                AdvisoryStripViewModel.BuildPostSaveMessage("Reload the profile.", 3));

            Assert.Equal(
                AdvisoryText.SavedWithAdvisories(3),
                AdvisoryStripViewModel.BuildPostSaveMessage(null, 3));
        }

        [Fact]
        public void TheConstructor_RefusesAMissingCallback()
        {
            Assert.Throws<ArgumentNullException>(() => new AdvisoryStripViewModel(null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new AdvisoryStripViewModel(_ => { }, null!));
        }

        [Fact]
        public void AStripWithNoStore_ReadsThePreferenceAtItsDefault()
        {
            // The no-device case (NullAppPreferencesStore): one trimmed line, which is what
            // `advisory_detail` absent means.
            using var strip = new AdvisoryStripViewModel(_ => { }, _ => { });

            Assert.False(strip.IsExpanded);
        }

        [Fact]
        public void TheExpansionPreference_IsReadWithItsOwnPolarityAndNeverAsAHideFlag()
        {
            // `advisory_detail` is the one display preference among seventeen: stored `on` means
            // "expand" and absent means "one line". Read it the way the twelve *_msg hide flags
            // beside it in the same file are read, and the strip is expanded by default and
            // collapses when the user asks for detail.
            var store = new FakeAppPreferencesStore();
            using var strip = new AdvisoryStripViewModel(_ => { }, _ => { }, store);

            Assert.False(strip.IsExpanded);

            store.SetInitial(AppSettings.Empty with { IsAdvisoryDetailExpanded = true });
            store.Update(settings => settings);

            Assert.True(strip.IsExpanded);

            // A suppression flag in the same file moves nothing here.
            store.SetInitial(AppSettings.Empty with { IsResetLayerConfirmationHidden = true });
            store.Update(settings => settings);

            Assert.False(strip.IsExpanded);
        }

        [Fact]
        public void TheStrip_FollowsTheStoreUntilItIsDisposed()
        {
            // The store belongs to the device session and outlives the editor, so a strip that
            // stayed subscribed would be re-read for every preference the next session writes.
            var store = new FakeAppPreferencesStore();
            var strip = new AdvisoryStripViewModel(_ => { }, _ => { }, store);

            store.Update(settings => AppPreferenceCatalog.AdvisoryDetail.SetValue(settings, true));

            Assert.True(strip.IsExpanded);

            strip.Dispose();
            strip.Dispose();

            store.Update(settings => AppPreferenceCatalog.AdvisoryDetail.SetValue(settings, false));

            Assert.True(strip.IsExpanded);
        }

        /// <summary>
        /// A Freestyle Edge RGB layout with one token remapped onto a second position, which is
        /// what <c>DuplicateKeyScan</c> reports — the cheapest fixture that anchors more than one
        /// advisory to the Layout tab.
        /// </summary>
        private static KeyboardLayout CreateLayoutWithADuplicate(out int layerIndex)
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];

            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layer.Keys[0].OriginalKey);

            layerIndex = layer.Index;

            return layout;
        }

        /// <summary>
        /// A Freestyle Edge RGB layout whose first slot macro is past the device's co-trigger cap —
        /// the cheapest fixture that anchors a <see cref="AdvisorySurface.MacroPanel"/> advisory to
        /// a layer, a key <b>and</b> a slot.
        /// </summary>
        private static KeyboardLayout CreateLayoutWithAnOverCoTriggeredMacro(out int layerIndex, out int keyIndex)
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];
            var key = layer.Keys[TestLayouts.RgbDigitOneKeyIndex];
            var macro = layout.CreateMacro();
            var limit = layout.Device.Macros.MaxCoTriggersPerMacro!.Value;

            foreach (var token in new[] { "lshft", "rshft", "lctrl", "rctrl", "lalt", "ralt" }.Take(limit + 1))
            {
                macro.AddCoTrigger(TestLayouts.Gen1Key(token));
            }

            key.SetMacro(1, macro);

            layerIndex = layer.Index;
            keyIndex = key.Index;

            return layout;
        }

        /// <summary>
        /// An Advantage 360 layout — the one board that keeps its macros in a flat list (06 §1) —
        /// with one macro past the co-trigger cap. <c>Validate()</c> anchors a flat-list finding
        /// with neither a key nor a slot, which is exactly the shape
        /// <see cref="AdvisorySurface"/> exists to keep off the board's selection path.
        /// </summary>
        private static KeyboardLayout CreateFlatListLayoutWithAnOverCoTriggeredMacro()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            Assert.True(layout.UsesFlatMacroList, "The Advantage 360 stopped using the flat macro list.");

            var macro = layout.CreateMacro();
            var limit = layout.Device.Macros.MaxCoTriggersPerMacro!.Value;

            macro.LayerIndex = layout.Layers[0].Index;
            macro.TriggerKey = TestLayouts.Gen1Key("f1").Code;
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));

            foreach (var token in new[] { "lshft", "rshft", "lctrl", "rctrl", "lalt", "ralt" }.Take(limit + 1))
            {
                macro.AddCoTrigger(TestLayouts.Gen1Key(token));
            }

            layout.AddMacro(macro);

            return layout;
        }
    }
}
