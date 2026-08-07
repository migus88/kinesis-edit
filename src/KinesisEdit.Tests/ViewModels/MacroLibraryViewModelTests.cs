using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Macros tab (mockup <c>1i</c>) as a <b>library</b>: one row per named macro whichever
    /// number of keys fire it, the slot cards of the selected key on a slot device, the flat table
    /// on the Advantage360, and the four per-macro edits of mockup <c>2i</c> — rename, duplicate,
    /// delete, and "which keys and layers fire it".
    /// <para>
    /// The branch is chosen by <see cref="MacroCapability.UsesFlatMacroList"/> and by nothing else,
    /// so every branch fact below is asserted with <b>two real catalog devices</b> rather than with a
    /// flag the test set itself.
    /// </para>
    /// </summary>
    public sealed class MacroLibraryViewModelTests
    {
        [Fact]
        public void Strings_MatchTheMockVerbatim()
        {
            Assert.Equal("Pick another key", MacroLibraryViewModel.PickAnotherKeyCaption);
            Assert.Equal("New macro", MacroLibraryViewModel.NewMacroCaption);
            Assert.Equal("Search name, trigger, or contents…", MacroLibraryViewModel.SearchPlaceholder);
            Assert.Equal("All layers", MacroLayerFilter.AllLayersCaption);
            Assert.Equal("Macro", MacroLibraryViewModel.MacroColumnCaption);
            Assert.Equal("Trigger", MacroLibraryViewModel.TriggerColumnCaption);
            Assert.Equal("Layer", MacroLibraryViewModel.LayerColumnCaption);
            Assert.Equal("Length", MacroLibraryViewModel.LengthColumnCaption);
            Assert.Equal("layout keystroke budget", MacroLibraryViewModel.LayoutBudgetMeterLabel);
            Assert.Equal("this macro", MacroLibraryViewModel.MacroLengthMeterLabel);
            Assert.Equal("ACTIVE", MacroSlotViewModel.ActiveBadge);
            Assert.Equal("Make active", MacroSlotViewModel.MakeActiveCaption);
            Assert.Equal("Record a macro", MacroSlotViewModel.RecordCaption);
            Assert.Equal("Slot 4 — empty", MacroSlotViewModel.BuildTitle(4, MacroSlotViewModel.EmptyCaption));
            Assert.Equal("Slot 1 — Sign-off block", MacroSlotViewModel.BuildTitle(1, "Sign-off block"));
        }

        /// <summary>
        /// The branch is a fact about the <b>device's macro store</b> (06 §1) and never about a
        /// device id — the whole point of "all screens render from this record".
        /// </summary>
        [Fact]
        public void TheBranch_IsChosenByTheCapabilityRecordAlone()
        {
            var slots = new Scene(DeviceId.FreestyleEdgeRgb);
            var flat = new Scene(DeviceId.Advantage360);

            Assert.False(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Macros.UsesFlatMacroList);
            Assert.True(DeviceCatalog.GetById(DeviceId.Advantage360).Macros.UsesFlatMacroList);

            Assert.True(slots.Panel.UsesMacroSlots);
            Assert.False(slots.Panel.UsesFlatMacroList);
            Assert.Equal(MacroLibraryViewModel.PickAnotherKeyCaption, slots.Panel.PrimaryActionCaption);

            Assert.True(flat.Panel.UsesFlatMacroList);
            Assert.False(flat.Panel.UsesMacroSlots);
            Assert.Equal(MacroLibraryViewModel.NewMacroCaption, flat.Panel.PrimaryActionCaption);
            Assert.Empty(flat.Panel.Slots);
        }

        [Fact]
        public void Header_OnTheSlotBranch_NamesTheSelectedKeyAndTheBoard()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            Assert.Equal("Macros · Freestyle Edge RGB", scene.Panel.Header);
            Assert.Equal(MacroLibraryViewModel.NoKeySubtitle, scene.Panel.Subtitle);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            var caption = KeyCaption.For(scene.Key!.Key.TriggerKey, scene.Layout.Dialect, KeyCaption.IsMacOs);

            Assert.Equal($"Macros on {caption} · Freestyle Edge RGB", scene.Panel.Header);
        }

        [Fact]
        public void Header_OnTheFlatBranch_IsTheOneFlatListPerProfile()
        {
            var scene = new Scene(DeviceId.Advantage360);

            Assert.Equal("Macro library · Advantage 360 · one flat list per profile", scene.Panel.Header);
            Assert.Equal("No macros · trigger + layer set per macro".Replace("No macros", "0 macros"), scene.Panel.Subtitle);

            scene.AddFlatMacro("a");
            scene.Refresh();

            Assert.Equal("1 macro · trigger + layer set per macro", scene.Panel.Subtitle);

            scene.AddFlatMacro("b");
            scene.Refresh();

            Assert.Equal("2 macros · trigger + layer set per macro", scene.Panel.Subtitle);
        }

        /// <summary>
        /// 06 §1: the Advantage2 and Freestyle Edge/Pro dialects persist macro slots 1-3 of the
        /// model's five, and <c>LayoutFileSerializer</c> writes exactly
        /// <see cref="MacroCapability.PersistedSlotsPerKey"/> of them — so a fourth card would offer
        /// a macro the next save silently drops.
        /// </summary>
        [Fact]
        public void Slots_OnADialectThatPersistsThreeOfFiveSlots_DrawsOnlyTheThree()
        {
            var scene = new Scene(DeviceId.FreestyleEdge);

            scene.SelectFirstMacroKey();

            var macros = DeviceCatalog.GetById(DeviceId.FreestyleEdge).Macros;

            Assert.Equal(5, macros.SlotsPerKey);
            Assert.Equal(3, macros.PersistedSlotsPerKey);
            Assert.Equal(new[] { 1, 2, 3 }, scene.Panel.Slots.Select(card => card.Slot));
        }

        [Fact]
        public void Slots_OnAPositionThatRefusesMacros_AreNotRenderedAtAll()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            // 05 §5.3 marks exactly the modifier positions as unable to carry a macro. A feature the
            // position lacks is not rendered at all, rather than drawn and refused.
            scene.Select(TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(scene.Key!.CanAssignMacro);
            Assert.Empty(scene.Panel.Slots);
        }

        [Fact]
        public void Subtitle_OnTheSlotBranch_CountsTheUsedSlotsAndTheActiveOne()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal("0 of 5 slots used · 0 active", scene.Panel.Subtitle);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 2, "b");
            scene.Key!.Key.ActiveMacroIndex = 1;
            scene.Refresh();

            Assert.Equal("2 of 5 slots used · 1 active", scene.Panel.Subtitle);
        }

        [Fact]
        public void Slots_BadgeTheActiveOneAndOfferMakeActiveOnTheRest()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 2, "b");
            scene.Key!.Key.ActiveMacroIndex = 1;
            scene.Refresh();

            Assert.True(scene.Panel.Slots[0].IsActive);
            Assert.False(scene.Panel.Slots[0].CanMakeActive);
            Assert.True(scene.Panel.Slots[1].CanMakeActive);

            // An empty card is never the active one, however the model's field happens to point.
            Assert.False(scene.Panel.Slots[2].IsActive);
            Assert.True(scene.Panel.Slots[2].IsEmpty);

            scene.Panel.MakeActiveCommand.Execute(scene.Panel.Slots[1]);

            Assert.Equal(2, scene.Key.Key.ActiveMacroIndex);
            Assert.True(scene.Panel.Slots[1].IsActive);

            // ActiveMacroIndex is in-memory only (05 §1.3) and is never serialized, so moving it
            // must not tell the user they have unsaved work.
            Assert.False(scene.IsDirty);
            Assert.Equal(1, scene.RefreshOnlyCount);
        }

        [Fact]
        public void RecordMacroCommand_OnAnEmptySlot_JumpsToTheInspectorWithRecordArmed()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            var empty = scene.Panel.Slots[2];

            Assert.True(empty.IsEmpty);

            scene.Panel.RecordMacroCommand.Execute(empty);

            // The tab never captures a keystroke: it hands the position to the one surface that does.
            var edit = Assert.Single(scene.Edits);

            Assert.Equal(0, edit.LayerIndex);
            Assert.Equal(TestLayouts.RgbDigitOneKeyIndex, edit.KeyIndex);
            Assert.Equal(3, edit.Slot);
            Assert.True(edit.StartRecording);
        }

        [Fact]
        public void PrimaryActionCommand_OnTheSlotBranch_LeavesForTheBoard()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.PrimaryActionCommand.Execute(null);

            Assert.Equal(1, scene.PickCount);
            Assert.Empty(scene.Edits);
        }

        [Fact]
        public void Rows_ListEachMacroOnceByName_WithEverySiteItFiresFrom()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.PlaceMacro(TestLayouts.RgbDigitTwoKeyIndex, 1, "a");
            scene.Refresh();

            // Two copies, one name, one row — the whole point of the redesign. Core groups them; the
            // tab lists the group.
            var row = Assert.Single(scene.Panel.Rows);

            Assert.Equal(2, row.SiteCount);
            Assert.True(row.HasSeveralSites);
            Assert.StartsWith(MacroLibraryRowViewModel.FiresFromPrefix, row.SitesText, StringComparison.Ordinal);
            Assert.Contains("[1]", row.TriggerText, StringComparison.Ordinal);
            Assert.Contains("[2]", row.TriggerText, StringComparison.Ordinal);
        }

        [Fact]
        public void Rows_RenderTheKeystrokesTheFileCarriesAndTheDevicesOwnLength()
        {
            var slots = new Scene(DeviceId.FreestyleEdgeRgb);

            slots.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a", "b");
            slots.Refresh();

            var slotRow = Assert.Single(slots.Panel.Rows);

            Assert.Equal("{a}{b}", slotRow.ContentsText);

            // Two metrics, one code path: weighted keystrokes here (04 §5.3)...
            Assert.Equal(2, slotRow.Length);
            Assert.Equal(MacroLengthMetric.Measure(slotRow.Entry.Canonical, slots.Layout), slotRow.Length);

            var flat = new Scene(DeviceId.Advantage360);

            flat.AddFlatMacro("a");
            flat.Refresh();

            var flatRow = Assert.Single(flat.Panel.Rows);

            // ...and the serialized macro-text length on the Advantage360 (06 §6), which is the
            // rendered `{a}` and not a keystroke count.
            Assert.Equal("{a}".Length, flatRow.Length);
            Assert.NotEqual(flatRow.Entry.Canonical.WeightedKeystrokeCount, flatRow.Length);
            Assert.Equal(MacroLengthMetric.Measure(flatRow.Entry.Canonical, flat.Layout), flatRow.Length);
        }

        [Fact]
        public void Rows_OverThePerMacroBudget_CarryTheMocksAmberSentenceAndKeepTheirRow()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);
            var limit = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Macros.MaxCharactersPerMacro!.Value;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, Enumerable.Repeat("a", limit + 1).ToArray());
            scene.Refresh();

            var row = Assert.Single(scene.Panel.Rows);

            Assert.True(row.IsOverBudget);
            Assert.Equal("301 of 300 characters — over the device budget. Saved as-is.", row.BudgetAdvisory);

            // Reported, never refused: the macro keeps its row, its card and its place in the model.
            Assert.Equal(1, scene.Layout.MacroCount);
            Assert.True(scene.Panel.Slots[0].IsOverBudget);
            Assert.Equal(row.BudgetAdvisory, scene.Panel.Slots[0].BudgetAdvisory);
        }

        [Fact]
        public void Meters_ReadTheDevicesOwnBudgets_AndGoAmberWithoutRefusingAnything()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);
            var macros = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Macros;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a", "b");
            scene.Key!.Key.ActiveMacroIndex = 1;
            scene.Refresh();

            Assert.Equal(macros.MaxTotalKeystrokes, scene.Panel.LayoutKeystrokeMeter.Limit);
            Assert.Equal("2 / 7 200", scene.Panel.LayoutKeystrokeMeter.Caption);
            Assert.Equal("2 / 300", scene.Panel.MacroLengthMeter.Caption);
            Assert.Equal("1 / 100", scene.Panel.MacroCountMeter.Caption);
            Assert.False(scene.Panel.MacroLengthMeter.IsOverBudget);

            var limit = macros.MaxCharactersPerMacro!.Value;

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 2, Enumerable.Repeat("a", limit + 1).ToArray());
            scene.Refresh();
            scene.Panel.SelectSlotCommand.Execute(scene.Panel.Slots[1]);

            Assert.True(scene.Panel.MacroLengthMeter.IsOverBudget);
            Assert.Equal("301 / 300", scene.Panel.MacroLengthMeter.Caption);
            Assert.Equal(2, scene.Layout.MacroCount);
        }

        /// <summary>
        /// 06 §6 states no macros-per-layout figure for the Advantage2. A null limit is "no limit",
        /// never zero, so the caption is the bare number and nothing can be over budget.
        /// </summary>
        [Fact]
        public void MacroCountMeter_OnADeviceThatStatesNoCount_ReadsTheBareNumber()
        {
            var scene = new Scene(DeviceId.Advantage2);

            scene.SelectFirstMacroKey();
            scene.PlaceMacro(scene.Key!.Index, 1, "a");
            scene.Refresh();

            Assert.NotEmpty(scene.Panel.Slots);

            Assert.Null(scene.Panel.MacroCountMeter.Limit);
            Assert.Equal("1", scene.Panel.MacroCountMeter.Caption);
            Assert.False(scene.Panel.MacroCountMeter.IsOverBudget);
        }

        [Fact]
        public void Search_MatchesTheName_TheTrigger_AndTheContents()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "b", "e", "s", "t");
            scene.PlaceMacro(TestLayouts.RgbDigitTwoKeyIndex, 1, "z");
            scene.Refresh();

            scene.Library.Rename(scene.Library.Entries[1], "Sign-off block");
            scene.Refresh();

            Assert.Equal(2, scene.Panel.Rows.Count);

            // By name...
            scene.Panel.SearchQuery = "sign-off";

            Assert.Equal("Sign-off block", Assert.Single(scene.Panel.Rows).Name);

            // ...by contents, both as the file spells them and as the macro types them...
            scene.Panel.SearchQuery = "{e}";

            Assert.Equal("best", Assert.Single(scene.Panel.Rows).Name);

            scene.Panel.SearchQuery = "best";

            Assert.Single(scene.Panel.Rows);

            // ...and by trigger token.
            scene.Panel.SearchQuery = "[2]";

            Assert.Equal("Sign-off block", Assert.Single(scene.Panel.Rows).Name);

            scene.Panel.SearchQuery = "nothing matches this";

            Assert.Empty(scene.Panel.Rows);
            Assert.True(scene.Panel.HasNoMatches);
            Assert.False(scene.Panel.IsEmpty);

            scene.Panel.SearchQuery = string.Empty;

            Assert.Equal(2, scene.Panel.Rows.Count);
        }

        [Fact]
        public void LayerFilter_NarrowsToOneLayerAndDefaultsToEveryLayer()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a", layerIndex: 0);
            scene.PlaceMacro(TestLayouts.RgbDigitTwoKeyIndex, 1, "b", layerIndex: 1);
            scene.Refresh();

            Assert.Equal(3, scene.Panel.LayerFilters.Count);
            Assert.Null(scene.Panel.LayerFilters[0].LayerIndex);
            Assert.Equal(2, scene.Panel.Rows.Count);

            scene.Panel.SelectedLayerFilter = scene.Panel.LayerFilters[2];

            Assert.Equal("b", Assert.Single(scene.Panel.Rows).Name);

            scene.Panel.SelectedLayerFilter = scene.Panel.LayerFilters[0];

            Assert.Equal(2, scene.Panel.Rows.Count);
        }

        [Fact]
        public void Rename_GoesThroughTheEditorsOnePath_AndABlankNameClearsIt()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.Refresh();

            var row = Assert.Single(scene.Panel.Rows);

            Assert.False(row.IsExplicitlyNamed);

            scene.Panel.BeginRenameCommand.Execute(row);

            Assert.True(row.IsRenaming);

            row.RenameText = "Sign-off block";

            scene.Panel.CommitRenameCommand.Execute(row);

            var renamed = Assert.Single(scene.Panel.Rows);

            Assert.Equal("Sign-off block", renamed.Name);
            Assert.True(renamed.IsExplicitlyNamed);
            Assert.True(scene.IsDirty);

            // Blank clears the stored name: the macro goes back to the one Core derives from it.
            scene.Panel.BeginRenameCommand.Execute(renamed);

            renamed.RenameText = string.Empty;

            scene.Panel.CommitRenameCommand.Execute(renamed);

            var cleared = Assert.Single(scene.Panel.Rows);

            Assert.False(cleared.IsExplicitlyNamed);
            Assert.Equal("a", cleared.Name);
        }

        [Fact]
        public void CancelRename_WritesNothing()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.Refresh();

            var row = Assert.Single(scene.Panel.Rows);

            scene.Panel.BeginRenameCommand.Execute(row);

            row.RenameText = "Never written";

            scene.Panel.CancelRenameCommand.Execute(row);

            Assert.False(row.IsRenaming);
            Assert.Equal("a", Assert.Single(scene.Panel.Rows).Name);
            Assert.False(scene.IsDirty);
        }

        [Fact]
        public void Duplicate_MakesAnIndependentSecondMacro_AndDoesNotRefuseTheTriggerCollision()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.Refresh();

            scene.Panel.DuplicateCommand.Execute(scene.Panel.Rows[0]);

            Assert.Equal(2, scene.Panel.Rows.Count);
            Assert.Equal(2, scene.Layout.MacroCount);
            Assert.True(scene.IsDirty);

            // The copy lands on the SAME trigger, which Validate reports as a duplicate trigger
            // (06 §5) — reported, never refused, like every other limit in this model.
            Assert.Contains(
                scene.Layout.Validate(),
                violation => violation.Kind == ModelViolationKind.MacroTriggerCollision);
        }

        [Fact]
        public void Duplicate_WithNoSlotLeft_ReportsItAndWritesNothing()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);
            var slots = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Macros.SlotsPerKey;
            var tokens = new[] { "a", "b", "c", "d", "e" };

            for (var slot = 1; slot <= slots; slot++)
            {
                scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, slot, tokens[slot - 1]);
            }

            scene.Refresh();

            scene.Panel.DuplicateCommand.Execute(scene.Panel.Rows[0]);

            Assert.Equal(MacroLibraryViewModel.DuplicateRefusedMessage, scene.Panel.Message);
            Assert.True(scene.Panel.HasMessage);
            Assert.Equal(slots, scene.Layout.MacroCount);
        }

        [Fact]
        public async Task Delete_NamesEverySiteInTheQuestionBeforeAnythingGoes()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.PlaceMacro(TestLayouts.RgbDigitTwoKeyIndex, 1, "a");
            scene.Refresh();

            var row = Assert.Single(scene.Panel.Rows);

            await scene.Panel.DeleteCommand.ExecuteAsync(row);

            var question = Assert.Single(scene.Questions);

            Assert.Equal(MacroLibraryViewModel.DeleteTitle, question.Title);
            Assert.Equal(MacroLibraryViewModel.DeleteConfirmCaption, question.ConfirmCaption);
            Assert.Contains("[1]", question.Message, StringComparison.Ordinal);
            Assert.Contains("[2]", question.Message, StringComparison.Ordinal);
            Assert.Contains("all 2 keys", question.Message, StringComparison.Ordinal);

            // Off EVERY site it occupied, which is exactly why the question had to name them.
            Assert.Empty(scene.Panel.Rows);
            Assert.Equal(0, scene.Layout.MacroCount);
            Assert.True(scene.IsDirty);
        }

        [Fact]
        public async Task Delete_WhenTheQuestionIsDeclined_ErasesNothing()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb) { ConfirmAnswer = false };

            scene.PlaceMacro(TestLayouts.RgbDigitOneKeyIndex, 1, "a");
            scene.Refresh();

            await scene.Panel.DeleteCommand.ExecuteAsync(scene.Panel.Rows[0]);

            Assert.Single(scene.Questions);
            Assert.Single(scene.Panel.Rows);
            Assert.Equal(1, scene.Layout.MacroCount);
            Assert.False(scene.IsDirty);
        }

        [Fact]
        public void EditMacroCommand_OpensTheMacroWhereItIsEdited()
        {
            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            scene.PlaceMacro(TestLayouts.RgbDigitTwoKeyIndex, 2, "a");
            scene.Refresh();

            scene.Panel.EditMacroCommand.Execute(scene.Panel.Rows[0]);

            var edit = Assert.Single(scene.Edits);

            Assert.Equal(0, edit.LayerIndex);
            Assert.Equal(TestLayouts.RgbDigitTwoKeyIndex, edit.KeyIndex);
            Assert.Equal(2, edit.Slot);

            // Opening a macro is reading, not recording.
            Assert.False(edit.StartRecording);
        }

        [Fact]
        public void NotSupported_IsSaidByTheTabItself()
        {
            // The strip carries the Macros tab on every board (EditorTabViewModel), so the tab is the
            // one place that answers for a device with no macros at all.
            Assert.Equal("This device does not support macros.", MacroLibraryViewModel.NotSupportedMessage);

            var scene = new Scene(DeviceId.FreestyleEdgeRgb);

            Assert.True(scene.Panel.IsSupported);
        }

        /// <summary>
        /// One Macros tab over one real layout, its library and a host that records what the tab
        /// asked the editor to do.
        /// </summary>
        private sealed class Scene : IMacroLibraryHost
        {
            public KeyboardLayout Layout { get; }

            public MacroLibrary Library { get; }

            public MacroLibraryViewModel Panel { get; }

            public KeyboardKeyViewModel? Key { get; private set; }

            public bool ConfirmAnswer { get; init; } = true;

            public bool IsDirty { get; private set; }

            public int PickCount { get; private set; }

            public int RefreshOnlyCount { get; private set; }

            public List<MacroEdit> Edits { get; } = [];

            public List<DeleteQuestion> Questions { get; } = [];

            private readonly IReadOnlyList<KeyboardLayerViewModel> _layers;

            public Scene(DeviceId deviceId)
            {
                Layout = KeyboardLayout.Create(deviceId);
                Library = new MacroLibrary(Layout);

                _layers = KeyboardLayerViewModel.BuildAll(Layout, ResolveVisual(deviceId, Layout), null);

                Panel = new MacroLibraryViewModel(TestDevices.CreateSnapshot(deviceId), this, () => Library);

                Refresh();
            }

            public void Select(int keyIndex)
            {
                Key = _layers[0].FindByIndex(keyIndex);

                Refresh();
            }

            /// <summary>The first position of layer 0 that accepts a macro (05 §5.3).</summary>
            public void SelectFirstMacroKey()
            {
                foreach (var key in Layout.Layers[0].Keys)
                {
                    if (key.CanAssignMacro)
                    {
                        Select(key.Index);

                        return;
                    }
                }

                throw new InvalidOperationException("The device has no position that accepts a macro.");
            }

            public void Refresh()
            {
                Library.Refresh();

                Panel.Refresh(Key, Key is null ? null : _layers[0], Layout);
            }

            /// <summary>Puts a macro straight into a key's slot — the model's own write path.</summary>
            public void PlaceMacro(int keyIndex, int slot, params string[] tokens)
            {
                PlaceMacro(keyIndex, slot, 0, tokens);
            }

            public void PlaceMacro(int keyIndex, int slot, string token, int layerIndex)
            {
                PlaceMacro(keyIndex, slot, layerIndex, token);
            }

            /// <summary>Appends a macro to the Gen2 flat list, tagged with its trigger and layer (06 §1).</summary>
            public void AddFlatMacro(params string[] tokens)
            {
                var key = Layout.Layers[0].Keys.First(candidate => candidate.CanAssignMacro);
                var macro = BuildMacro(TokenDialect.Gen2, tokens);

                macro.TriggerKey = key.TriggerKey.Code;
                macro.LayerIndex = 0;

                Layout.AddMacro(macro);
            }

            public MacroLibraryEntry? RenameMacro(MacroLibraryEntry entry, string? newName)
            {
                MacroLibraryEntry renamed;

                try
                {
                    renamed = Library.Rename(entry, newName);
                }
                catch (ArgumentException)
                {
                    return null;
                }

                IsDirty = true;

                Refresh();

                return renamed;
            }

            public void CommitMacroLibraryEdit()
            {
                IsDirty = true;

                Refresh();
            }

            public void RefreshMacroViews()
            {
                RefreshOnlyCount++;

                Refresh();
            }

            public void PickMacroKey()
            {
                PickCount++;
            }

            public void EditMacroAt(int layerIndex, int keyIndex, int slot, bool startRecording)
            {
                Edits.Add(new MacroEdit(layerIndex, keyIndex, slot, startRecording));
            }

            public Task<bool> ConfirmMacroDeleteAsync(
                string title,
                string message,
                string confirmCaption,
                string declineCaption)
            {
                Questions.Add(new DeleteQuestion(title, message, confirmCaption, declineCaption));

                return Task.FromResult(ConfirmAnswer);
            }

            private void PlaceMacro(int keyIndex, int slot, int layerIndex, params string[] tokens)
            {
                var layer = Layout.Layers[layerIndex];
                var key = layer.Keys.Single(candidate => candidate.Index == keyIndex);
                var macro = BuildMacro(Layout.Dialect, tokens);

                macro.TriggerKey = key.TriggerKey.Code;
                macro.LayerIndex = layerIndex;

                key.SetMacro(slot, macro);
            }

            /// <summary>
            /// The board's own drawing where there is one, and a one-row stand-in where there is
            /// not: only the Freestyle Edge RGB has its picture authored (issues #39-#42), and the
            /// per-device rules this suite asserts are the <b>capability record's</b>, not the
            /// geometry's.
            /// </summary>
            private static KeyboardVisual ResolveVisual(DeviceId deviceId, KeyboardLayout layout)
            {
                if (VisualCatalog.TryGet(deviceId, out var visual))
                {
                    return visual;
                }

                var keys = new List<KeyVisual>(layout.Layers[0].Keys.Count);

                foreach (var key in layout.Layers[0].Keys)
                {
                    keys.Add(new KeyVisual(key.Index, key.Index, 0));
                }

                return new KeyboardVisual(LayoutVariant.None, keys);
            }

            private Macro BuildMacro(TokenDialect dialect, params string[] tokens)
            {
                var macro = Layout.CreateMacro();

                foreach (var token in tokens)
                {
                    macro.AddKeystroke(new Keystroke(KeyRegistry.FindByToken(token, dialect)!));
                }

                return macro;
            }
        }

        /// <summary>One <c>EditMacroAt</c> the tab asked for.</summary>
        private readonly record struct MacroEdit(int LayerIndex, int KeyIndex, int Slot, bool StartRecording);

        /// <summary>One delete confirmation the tab put on screen.</summary>
        private readonly record struct DeleteQuestion(
            string Title,
            string Message,
            string ConfirmCaption,
            string DeclineCaption);
    }
}
