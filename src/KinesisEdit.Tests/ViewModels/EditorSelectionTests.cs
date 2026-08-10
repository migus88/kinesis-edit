using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The click contract, the arrow keys, the layer switch, the section strip and the
    /// <c>Review N</c> anchors on their own — <b>no editor, no profile session and no drive</b>.
    /// That is the point of the type existing: every claim about what a click on a cap means, and
    /// about which gestures may <em>not</em> arm the keyboard, used to have to be made through a
    /// loaded <c>KeyboardEditorViewModel</c>.
    /// <para>
    /// The editor is a fake host that records the calls the selection makes on it — in order, which
    /// is what the stand-down triple's assertions need — so this suite watches the selection's own
    /// behaviour rather than a board it does not own. <c>BeginRemap</c>, <c>CancelRemap</c> and
    /// <c>IsListening</c> are among those calls: they belong to <see cref="EditorKeystrokeRouter"/>,
    /// and a collaborator never sees another collaborator.
    /// </para>
    /// </summary>
    public class EditorSelectionTests
    {
        /// <summary>Row 1 of the state table: nothing selected, click <i>K</i> — <i>K</i> is selected and the rail opens.</summary>
        [AvaloniaFact]
        public void AFirstClickOnACap_SelectsIt_AndOpensTheRail()
        {
            var scene = new Scene();
            var key = scene.Key(0);

            scene.Selection.SelectKeyCommand.Execute(key);

            Assert.Same(key, scene.Selection.SelectedKey);
            Assert.True(key.IsSelected);

            // A click is a request for the inspector whichever branch it then takes, and the rail
            // is pushed the new subject by hand: a selection change writes nothing, so it never
            // reaches the editor's RefreshCounters funnel.
            Assert.Equal(1, scene.Host.OpenInspectorCalls);
            Assert.Equal(1, scene.Host.RefreshInspectorCalls);

            // The first click selects and nothing more.
            Assert.Equal(0, scene.Host.BeginRemapCalls);
            Assert.Equal(1, scene.Host.SelectedKeyChangedCalls);
        }

        /// <summary>Row 2: <i>K</i> selected, click <i>K</i> — the second click is what starts a remap.</summary>
        [AvaloniaFact]
        public void ASecondClickOnTheSelectedCap_BeginsARemap()
        {
            var scene = new Scene();
            var key = scene.Key(0);

            scene.Selection.SelectKeyCommand.Execute(key);
            scene.Selection.SelectKeyCommand.Execute(key);

            Assert.Equal(1, scene.Host.BeginRemapCalls);

            // The selection did not move, so it was announced exactly once — and the rail was
            // asked to open again, which is what reopens a rail the user pressed Escape on.
            Assert.Same(key, scene.Selection.SelectedKey);
            Assert.Equal(1, scene.Host.SelectedKeyChangedCalls);
            Assert.Equal(2, scene.Host.OpenInspectorCalls);
        }

        /// <summary>Row 3: <i>K</i> selected, click <i>L</i> — a different cap only ever selects.</summary>
        [AvaloniaFact]
        public void AClickOnADifferentCap_JustSelectsIt()
        {
            var scene = new Scene();
            var first = scene.Key(0);
            var second = scene.Key(1);

            scene.Selection.SelectKeyCommand.Execute(first);

            scene.Host.ResetCounts();

            scene.Selection.SelectKeyCommand.Execute(second);

            Assert.Same(second, scene.Selection.SelectedKey);
            Assert.True(second.IsSelected);
            Assert.False(first.IsSelected);
            Assert.Equal(0, scene.Host.BeginRemapCalls);

            // Moving the selection ends an in-flight listen: the assignment belongs to the cap it
            // was started on.
            Assert.Equal(1, scene.Host.CancelRemapCalls);
        }

        /// <summary>Row 4: <i>K</i> listening, click <i>K</i> — the same gesture that armed it disarms it.</summary>
        [AvaloniaFact]
        public void AClickOnTheListeningCap_CancelsTheListen_RatherThanRestartingIt()
        {
            var scene = new Scene();
            var key = scene.Key(0);

            scene.Selection.SelectKeyCommand.Execute(key);

            scene.Host.ResetCounts();
            scene.Host.IsListening = true;

            scene.Selection.SelectKeyCommand.Execute(key);

            Assert.Equal(0, scene.Host.BeginRemapCalls);
            Assert.Equal(1, scene.Host.CancelRemapCalls);
            Assert.Same(key, scene.Selection.SelectedKey);
        }

        /// <summary>Row 10: <c>SelectKeyCommand(null)</c> — nothing selected, and the listen ends with it.</summary>
        [AvaloniaFact]
        public void SelectKeyCommand_WithNull_ClearsTheSelection()
        {
            var scene = new Scene();
            var key = scene.Key(0);

            scene.Selection.SelectKeyCommand.Execute(key);

            scene.Host.ResetCounts();

            scene.Selection.SelectKeyCommand.Execute(null);

            Assert.Null(scene.Selection.SelectedKey);
            Assert.False(key.IsSelected);
            Assert.Equal(1, scene.Host.CancelRemapCalls);

            // A click on nothing is not a request for the rail, but the rail is still told that the
            // subject went away.
            Assert.Equal(0, scene.Host.OpenInspectorCalls);
            Assert.Equal(1, scene.Host.RefreshInspectorCalls);
        }

        /// <summary>
        /// Invariant 35: a board gesture is refused <b>in the handler</b>, never by
        /// <c>CanExecute</c>. A cap <c>Button</c> binds this command directly, so a predicate would
        /// disable all ~76 caps at once — and <c>:disabled</c> outranks every state in the keycap
        /// theme, so the board would go dead rather than inert.
        /// </summary>
        [AvaloniaFact]
        public void SelectKeyCommand_HasNoPredicate_WhateverTheEditorIsDoing()
        {
            var scene = new Scene();

            Assert.True(scene.Selection.SelectKeyCommand.CanExecute(null));
            Assert.True(scene.Selection.SelectKeyCommand.CanExecute(scene.Key(0)));

            scene.Host.IsLoading = true;
            scene.Host.IsBusy = true;
            scene.Host.IsCopyArmed = true;
            scene.Host.IsListening = true;

            Assert.True(scene.Selection.SelectKeyCommand.CanExecute(null));
            Assert.True(scene.Selection.SelectKeyCommand.CanExecute(scene.Key(0)));
        }

        /// <summary>
        /// An armed <c>Copy key…</c> takes the click ahead of the whole click contract: the next cap
        /// clicked <em>is</em> the target. Without the interception the second half of the pick
        /// would read as "a second hit on the selected cap" and remap the very key being copied
        /// from.
        /// </summary>
        [AvaloniaFact]
        public void AnArmedCopy_TakesTheClick_AheadOfTheClickContract()
        {
            var scene = new Scene();
            var key = scene.Key(0);

            scene.Selection.SelectKeyCommand.Execute(key);

            scene.Host.ResetCounts();
            scene.Host.IsCopyArmed = true;

            scene.Selection.SelectKeyCommand.Execute(key);

            Assert.Same(key, Assert.Single(scene.Host.CopyTargets));
            Assert.Equal(0, scene.Host.BeginRemapCalls);
            Assert.Equal(0, scene.Host.CancelCopyKeyCalls);
        }

        /// <summary>A click that selects nothing is not a target: it ends the pick and clears.</summary>
        [AvaloniaFact]
        public void AnArmedCopy_ClickedOffTheBoard_EndsThePick_AndFallsThroughToClearing()
        {
            var scene = new Scene();
            var key = scene.Key(0);

            scene.Selection.SelectKeyCommand.Execute(key);

            scene.Host.ResetCounts();
            scene.Host.IsCopyArmed = true;

            scene.Selection.SelectKeyCommand.Execute(null);

            Assert.Empty(scene.Host.CopyTargets);
            Assert.Equal(1, scene.Host.CancelCopyKeyCalls);
            Assert.Null(scene.Selection.SelectedKey);
            Assert.False(key.IsSelected);
        }

        /// <summary>
        /// Invariant 24: an arrow moves the selection and <b>nothing else</b>. It lands through
        /// <c>SelectKeyDirectly</c>; <c>SelectKeyCommand</c> would promote the second press on the
        /// already-selected cap into listening and eat the user's next keystroke.
        /// </summary>
        [AvaloniaFact]
        public void MoveSelection_LandsThroughSelectKeyDirectly_AndNeverArmsARemap()
        {
            var scene = Scene.Row();

            // With nothing selected the first cap of the shown layer is where an arrow lands, so
            // the grammar has an entry point that needs no click first.
            Assert.True(scene.Selection.MoveSelection(NavigationDirection.Right));
            Assert.Same(scene.Key(0), scene.Selection.SelectedKey);

            Assert.True(scene.Selection.MoveSelection(NavigationDirection.Right));
            Assert.Same(scene.Key(1), scene.Selection.SelectedKey);

            Assert.True(scene.Selection.MoveSelection(NavigationDirection.Left));
            Assert.Same(scene.Key(0), scene.Selection.SelectedKey);

            // Three moves, and the keyboard was never armed once — nor was the rail opened, because
            // an arrow is not a click.
            Assert.Equal(0, scene.Host.BeginRemapCalls);
            Assert.Equal(0, scene.Host.OpenInspectorCalls);

            // It is still a selection change, so the rail is told what the editor is now about.
            Assert.Equal(3, scene.Host.RefreshInspectorCalls);
        }

        /// <summary>An arrow that leaves the board answers false, and the selection stays put.</summary>
        [AvaloniaFact]
        public void MoveSelection_OffTheEdgeOfTheBoard_ReportsThatNothingMoved()
        {
            var scene = Scene.Row();

            scene.Selection.SelectKeyCommand.Execute(scene.Key(0));

            Assert.False(scene.Selection.MoveSelection(NavigationDirection.Left));
            Assert.Same(scene.Key(0), scene.Selection.SelectedKey);
        }

        /// <summary>The two flags every editing gate carries.</summary>
        [AvaloniaTheory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void MoveSelection_IsRefused_WhileLoadingOrSaving(bool isLoading, bool isBusy)
        {
            var scene = Scene.Row();

            scene.Host.IsLoading = isLoading;
            scene.Host.IsBusy = isBusy;

            Assert.False(scene.Selection.MoveSelection(NavigationDirection.Right));
            Assert.Null(scene.Selection.SelectedKey);
        }

        /// <summary>
        /// <c>None</c> is what <c>EditorShortcuts.ToDirection</c> answers for everything that is not
        /// a board move, and <c>KeyAdjacency.Next</c> throws on it — so it is refused up front.
        /// </summary>
        [AvaloniaFact]
        public void MoveSelection_WithNoDirection_DoesNothing()
        {
            var scene = Scene.Row();

            Assert.False(scene.Selection.MoveSelection(NavigationDirection.None));
            Assert.Null(scene.Selection.SelectedKey);
        }

        /// <summary>
        /// A section the strip does not carry stays shut — and is refused <b>before</b> anything is
        /// stood down, so a binding that asks for an absent tab cannot cancel a remap as a side
        /// effect of being told no.
        /// </summary>
        [AvaloniaFact]
        public void SelectTab_RefusesASectionTheStripDoesNotCarry()
        {
            var scene = new Scene();

            Assert.DoesNotContain(scene.Selection.Tabs, tab => tab.Tab == EditorTab.Lighting);

            scene.Selection.SelectTab(EditorTab.Lighting);

            Assert.Equal(EditorTab.Keys, scene.Selection.SelectedTab);
            Assert.Equal(0, scene.Host.TabChangedCalls);
            Assert.Empty(scene.Host.Calls);
        }

        /// <summary>
        /// The two-way-bindable setter runs the same guard as the command, so a binding cannot open
        /// what a click cannot.
        /// </summary>
        [AvaloniaFact]
        public void SelectedTabSetter_RunsTheSameGuardAsTheCommand()
        {
            var scene = new Scene();

            scene.Selection.SelectedTab = EditorTab.Lighting;

            Assert.Equal(EditorTab.Keys, scene.Selection.SelectedTab);

            scene.Selection.SelectedTab = EditorTab.Settings;

            Assert.Equal(EditorTab.Settings, scene.Selection.SelectedTab);
            Assert.Equal(1, scene.Host.TabChangedCalls);
        }

        /// <summary>
        /// The stand-down triple, in the order the section switch runs it and with the rest of the
        /// switch behind it. It is deliberately not a helper: the calls differ site by site and each
        /// has its own reason.
        /// </summary>
        [AvaloniaFact]
        public void SelectTab_StandsTheTripleDownInOrder_ThenMovesTheStrip()
        {
            var scene = new Scene();

            scene.Selection.SelectTab(EditorTab.Settings);

            Assert.Equal(
                new[]
                {
                    "CancelRemap",
                    "CancelCopyKey",
                    "DeactivateInspector",
                    "RefreshAdvisorySummary",
                    "NotifyCommands"
                },
                scene.Host.Calls);

            Assert.Equal(EditorTab.Settings, scene.Selection.SelectedTab);
            Assert.Equal(1, scene.Host.TabChangedCalls);

            foreach (var tab in scene.Selection.Tabs)
            {
                Assert.Equal(tab.Tab == EditorTab.Settings, tab.IsSelected);
            }
        }

        /// <summary>
        /// Re-opening the open section announces nothing — the same "one write per real change" the
        /// property setter enforced — but still stands the triple down, because that half was never
        /// conditional.
        /// </summary>
        [AvaloniaFact]
        public void SelectTab_ToTheOpenSection_AnnouncesNothing_ButStillStandsDown()
        {
            var scene = new Scene();

            scene.Selection.SelectTab(EditorTab.Keys);

            Assert.Equal(0, scene.Host.TabChangedCalls);
            Assert.Equal(1, scene.Host.CancelRemapCalls);
            Assert.Equal(1, scene.Host.CancelCopyKeyCalls);
            Assert.Equal(1, scene.Host.DeactivateInspectorCalls);
        }

        /// <summary>
        /// The command's whole predicate: whether it was handed a tab. Nothing else — which is why
        /// the editor's <c>NotifyCommands()</c> deliberately never re-asks it.
        /// </summary>
        [AvaloniaFact]
        public void SelectTabCommand_RefusesOnlyANullTab()
        {
            var scene = new Scene();

            Assert.False(scene.Selection.SelectTabCommand.CanExecute(null));

            foreach (var tab in scene.Selection.Tabs)
            {
                Assert.True(scene.Selection.SelectTabCommand.CanExecute(tab));
            }

            scene.Selection.SelectTabCommand.Execute(scene.Tab(EditorTab.Settings));

            Assert.Equal(EditorTab.Settings, scene.Selection.SelectedTab);
        }

        /// <summary>
        /// Invariant 28's other half: <c>SelectLayer</c> carries <b>no identity guard</b> and needs
        /// none, because it writes nothing to the model. Switching to the layer that is already
        /// shown, three times over, leaves the layout exactly as it was and announces the layer
        /// once.
        /// </summary>
        [AvaloniaFact]
        public void SelectLayer_WritesNothingToTheModel_AndIsIdempotentWithoutAGuard()
        {
            var scene = new Scene();
            var layer = scene.Selection.Layers[1];

            scene.Selection.SelectLayer(layer);
            scene.Selection.SelectLayer(layer);
            scene.Selection.SelectLayer(layer);

            // One real move, then two calls that changed nothing — and there is no early return in
            // the method that could have made that true. It is true because nothing in it writes.
            Assert.Same(layer, scene.Selection.SelectedLayer);
            Assert.Equal(1, scene.Host.SelectedLayerChangedCalls);

            Assert.Equal(0, scene.Layout.ModifiedKeyCount);
            Assert.Equal(0, scene.Layout.MacroCount);

            foreach (var key in layer.Keys)
            {
                Assert.False(key.Key.IsModified);
            }
        }

        /// <summary>
        /// The layer switch is the one stand-down site that also drops the selection: anything
        /// half-done belongs to the layer it was started on.
        /// </summary>
        [AvaloniaFact]
        public void SelectLayer_StandsTheTripleDown_ClearsTheKey_AndRefreshesTheStripAndLegend()
        {
            var scene = new Scene();
            var key = scene.Key(0);

            scene.Selection.SelectKeyCommand.Execute(key);

            scene.Host.ResetCounts();

            scene.Selection.SelectLayer(scene.Selection.Layers[1]);

            Assert.Equal(
                new[]
                {
                    "CancelRemap",
                    "CancelCopyKey",
                    "DeactivateInspector",
                    "RefreshAdvisorySummary",
                    "RefreshLegend"
                },
                scene.Host.Calls);

            Assert.Null(scene.Selection.SelectedKey);
            Assert.False(key.IsSelected);
        }

        /// <summary>The layer switch marks the shown layer and unmarks every other one.</summary>
        [AvaloniaFact]
        public void SelectLayer_MarksTheShownLayerAndNoOther()
        {
            var scene = new Scene();

            scene.Selection.SelectLayerCommand.Execute(scene.Selection.Layers[1]);

            Assert.Same(scene.Selection.Layers[1], scene.Selection.SelectedLayer);

            for (var index = 0; index < scene.Selection.Layers.Count; index++)
            {
                Assert.Equal(index == 1, scene.Selection.Layers[index].IsSelected);
            }
        }

        /// <summary>
        /// <c>Review N</c>'s key half lands on the anchored cap through <c>SelectKeyDirectly</c> —
        /// never the click contract, whose second-click promotion would arm the keyboard on a
        /// gesture that is reading, not editing (invariant 24).
        /// </summary>
        [AvaloniaFact]
        public void SelectAnchoredKey_LandsOnTheAnchoredCap_WithoutArmingTheKeyboard()
        {
            var scene = new Scene();
            var anchored = scene.Key(1);

            scene.Selection.SelectKeyCommand.Execute(anchored);

            scene.Host.ResetCounts();

            // The anchor names the cap that is already selected, which is exactly the case the
            // click contract would promote into listening.
            scene.Selection.SelectAnchoredKey(new AdvisoryAnchor { Tab = EditorTab.Keys, KeyIndex = anchored.Index });

            Assert.Same(anchored, scene.Selection.SelectedKey);
            Assert.Equal(0, scene.Host.BeginRemapCalls);

            scene.Selection.SelectAnchoredKey(
                new AdvisoryAnchor { Tab = EditorTab.Keys, KeyIndex = scene.Key(0).Index });

            Assert.Same(scene.Key(0), scene.Selection.SelectedKey);
            Assert.Equal(0, scene.Host.BeginRemapCalls);
        }

        /// <summary>An anchor with no position — a layout-wide advisory — selects nothing.</summary>
        [AvaloniaFact]
        public void SelectAnchoredKey_WithNoPosition_SelectsNothing()
        {
            var scene = new Scene();

            scene.Selection.SelectAnchoredKey(new AdvisoryAnchor { Tab = EditorTab.Keys });

            Assert.Null(scene.Selection.SelectedKey);
        }

        /// <summary>
        /// <c>Review N</c>'s macro half opens the anchored <em>site</em> — layer, key, slot — where
        /// macros are edited, and never arms Record: reviewing is reading.
        /// </summary>
        [AvaloniaFact]
        public void SelectAnchoredMacro_OpensTheAnchoredSite()
        {
            var scene = new Scene();

            scene.Selection.SelectAnchoredMacro(new AdvisoryAnchor
            {
                Tab = EditorTab.Keys,
                Surface = AdvisorySurface.MacroPanel,
                LayerIndex = 1,
                KeyIndex = 7,
                MacroIndex = 3
            });

            Assert.Equal((1, 7, 3, false), Assert.Single(scene.Host.MacroSites));
        }

        /// <summary>A flat-list macro names no slot, so the anchor falls back to the flat-list one.</summary>
        [AvaloniaFact]
        public void SelectAnchoredMacro_WithNoSlot_OpensTheFlatListSite()
        {
            var scene = new Scene();

            scene.Selection.SelectAnchoredMacro(new AdvisoryAnchor
            {
                Tab = EditorTab.Keys,
                Surface = AdvisorySurface.MacroPanel,
                LayerIndex = 0,
                KeyIndex = 4
            });

            Assert.Equal((0, 4, MacroSites.FlatListSlot, false), Assert.Single(scene.Host.MacroSites));
        }

        /// <summary>An anchor missing either index is not a site, so nothing is opened.</summary>
        [AvaloniaFact]
        public void SelectAnchoredMacro_WithNoSite_OpensNothing()
        {
            var scene = new Scene();

            scene.Selection.SelectAnchoredMacro(new AdvisoryAnchor { Tab = EditorTab.Keys, KeyIndex = 4 });
            scene.Selection.SelectAnchoredMacro(new AdvisoryAnchor { Tab = EditorTab.Keys, LayerIndex = 0 });

            Assert.Empty(scene.Host.MacroSites);
        }

        /// <summary>
        /// The picture is announced when it really is a different picture, and not when a second
        /// load with nothing to draw hands over the same empty list.
        /// </summary>
        [AvaloniaFact]
        public void Layers_AnnounceOnlyWhenTheCollectionReallyMoved()
        {
            var scene = new Scene();
            var built = scene.Selection.Layers;

            scene.Selection.Layers = built;

            Assert.Equal(0, scene.Host.LayersChangedCalls);

            scene.Selection.Layers = [];

            Assert.Equal(1, scene.Host.LayersChangedCalls);
            Assert.Empty(scene.Selection.Layers);

            scene.Selection.Layers = [];

            Assert.Equal(1, scene.Host.LayersChangedCalls);
        }

        /// <summary>
        /// The strip is fixed at construction from device-level facts, which is why the lighting
        /// answer is a constructor parameter: the editor is built before any profile is read, and
        /// demo mode never reads one.
        /// </summary>
        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void Tabs_AreBuiltOnceFromDeviceFacts(bool isLightingSupported)
        {
            var scene = new Scene(isLightingSupported);
            var strip = scene.Selection.Tabs;

            Assert.Equal(isLightingSupported, strip.Any(tab => tab.Tab == EditorTab.Lighting));

            scene.Selection.SelectTab(EditorTab.Settings);

            Assert.Same(strip, scene.Selection.Tabs);
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingCollaborator()
        {
            var host = new FakeEditorSelectionHost();
            var device = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);

            Assert.Throws<ArgumentNullException>(
                () => new EditorSelection(null!, device, isLightingSupported: false, VisualCatalog.FreestyleEdgeRgb));

            Assert.Throws<ArgumentNullException>(
                () => new EditorSelection(host, null!, isLightingSupported: false, VisualCatalog.FreestyleEdgeRgb));
        }

        /// <summary>
        /// A selection over a real model and a fake editor, with nothing else. The layers are handed
        /// over exactly as the editor's <c>Apply</c> hands them over, which is the only path that
        /// builds the picture, and the first layer is selected the way <c>Apply</c> selects it.
        /// </summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public FakeEditorSelectionHost Host { get; }

            public EditorSelection Selection { get; }

            public Scene(bool isLightingSupported = false)
                : this(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb), VisualCatalog.FreestyleEdgeRgb, isLightingSupported)
            {
            }

            private Scene(KeyboardLayout layout, KeyboardVisual visual, bool isLightingSupported)
            {
                Layout = layout;

                Host = new FakeEditorSelectionHost();
                Selection = new EditorSelection(
                    Host,
                    DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb),
                    isLightingSupported,
                    visual);

                Selection.Layers = KeyboardLayerViewModel.BuildAll(layout, visual, lighting: null);
                Selection.SelectLayer(Selection.Layers[0]);

                // Everything above is the editor's own Apply, not a claim any test makes.
                Host.ResetCounts();
            }

            /// <summary>
            /// A scene over a board of three caps in one row, one unit apart — the only shape an
            /// arrow test can make an exact claim against, because <c>KeyAdjacency</c> scores the
            /// real geometry rather than list order.
            /// </summary>
            public static Scene Row()
            {
                return new Scene(
                    TestLayouts.CreateLayout("esc", "F1", "F2"),
                    TestLayouts.CreateVisual(0, 1, 2),
                    isLightingSupported: false);
            }

            public KeyboardKeyViewModel Key(int index)
            {
                return Selection.Layers[0].Keys[index];
            }

            public EditorTabViewModel Tab(EditorTab tab)
            {
                return Selection.Tabs.Single(entry => entry.Tab == tab);
            }
        }

        /// <summary>
        /// The editor as <see cref="EditorSelection"/> sees it: the four flags a gesture is decided
        /// from, and a tally — in order — of the calls moving the pointer makes back. Three of them
        /// belong to <see cref="EditorKeystrokeRouter"/> and reach it through the editor, which is
        /// the whole reason this interface exists.
        /// <para>
        /// There is deliberately <b>no</b> <c>RefreshCounters</c> here, and none on the interface: a
        /// selection change writes nothing to the model, so no path of this collaborator may reach
        /// the editor's post-write funnel (invariant 16). Leaving it off the interface is what makes
        /// that structural rather than a convention.
        /// </para>
        /// </summary>
        private sealed class FakeEditorSelectionHost : IEditorSelectionHost
        {
            public bool IsLoading { get; set; }

            public bool IsBusy { get; set; }

            public bool IsCopyArmed { get; set; }

            public bool IsListening { get; set; }

            /// <summary>Every call the selection made, in order — what the stand-down triple is asserted on.</summary>
            public List<string> Calls { get; } = [];

            /// <summary>The caps an armed copy was finished onto.</summary>
            public List<KeyboardKeyViewModel> CopyTargets { get; } = [];

            /// <summary>The macro sites <c>Review N</c> asked to be opened.</summary>
            public List<(int LayerIndex, int KeyIndex, int Slot, bool StartRecording)> MacroSites { get; } = [];

            public int OpenInspectorCalls { get; private set; }

            public int RefreshInspectorCalls { get; private set; }

            public int DeactivateInspectorCalls { get; private set; }

            public int BeginRemapCalls { get; private set; }

            public int CancelRemapCalls { get; private set; }

            public int CancelCopyKeyCalls { get; private set; }

            public int LayersChangedCalls { get; private set; }

            public int SelectedLayerChangedCalls { get; private set; }

            public int SelectedKeyChangedCalls { get; private set; }

            public int TabChangedCalls { get; private set; }

            public void ResetCounts()
            {
                OpenInspectorCalls = 0;
                RefreshInspectorCalls = 0;
                DeactivateInspectorCalls = 0;
                BeginRemapCalls = 0;
                CancelRemapCalls = 0;
                CancelCopyKeyCalls = 0;
                LayersChangedCalls = 0;
                SelectedLayerChangedCalls = 0;
                SelectedKeyChangedCalls = 0;
                TabChangedCalls = 0;

                Calls.Clear();
                CopyTargets.Clear();
                MacroSites.Clear();
            }

            public void OpenInspector()
            {
                OpenInspectorCalls++;

                Calls.Add(nameof(OpenInspector));
            }

            public void RefreshInspector()
            {
                RefreshInspectorCalls++;

                Calls.Add(nameof(RefreshInspector));
            }

            public void DeactivateInspector()
            {
                DeactivateInspectorCalls++;

                Calls.Add(nameof(DeactivateInspector));
            }

            public void BeginRemap()
            {
                BeginRemapCalls++;

                Calls.Add(nameof(BeginRemap));
            }

            public void CancelRemap()
            {
                CancelRemapCalls++;

                Calls.Add(nameof(CancelRemap));
            }

            public void CancelCopyKey()
            {
                CancelCopyKeyCalls++;

                Calls.Add(nameof(CancelCopyKey));
            }

            public void CompleteCopyKey(KeyboardKeyViewModel target)
            {
                CopyTargets.Add(target);

                Calls.Add(nameof(CompleteCopyKey));
            }

            public void EditMacroAt(int layerIndex, int keyIndex, int slot, bool startRecording)
            {
                MacroSites.Add((layerIndex, keyIndex, slot, startRecording));

                Calls.Add(nameof(EditMacroAt));
            }

            public void RefreshAdvisorySummary()
            {
                Calls.Add(nameof(RefreshAdvisorySummary));
            }

            public void RefreshLegend()
            {
                Calls.Add(nameof(RefreshLegend));
            }

            public void NotifyCommands()
            {
                Calls.Add(nameof(NotifyCommands));
            }

            public void OnLayersChanged()
            {
                LayersChangedCalls++;
            }

            public void OnSelectedLayerChanged()
            {
                SelectedLayerChangedCalls++;
            }

            public void OnSelectedKeyChanged()
            {
                SelectedKeyChangedCalls++;
            }

            public void OnTabChanged()
            {
                TabChangedCalls++;
            }
        }
    }
}
