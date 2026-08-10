using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// §11.6's macro insertion and ⌘F on their own — <b>no editor, no profile session and no
    /// drive</b>. The rail is a real <see cref="KeyInspectorViewModel"/> over a real model, because
    /// the whole gate is a question about it; everything else the type reads is a fake host that
    /// records what was asked of it.
    /// <para>
    /// The two insert paths are what this suite is about: the picker's answer landing on the macro
    /// the rail has open, and the same write refused when there is no macro to land on.
    /// </para>
    /// </summary>
    public class MacroInsertionHostTests
    {
        [AvaloniaFact]
        public void CanInsertIntoMacro_WantsTheRailOpen_OnItsMacroPanel_OverARealMacro()
        {
            // Without the mode test the button would stay live beside a Remap panel and the picked
            // token would be appended to a macro the user cannot see.
            var scene = new Scene();

            scene.GiveTheKeyAMacro();

            // Nothing selected: the rail is not showing a position at all.
            Assert.False(scene.Insertion.CanInsertIntoMacro());

            // Selecting a position that carries a macro opens the rail on its Macro panel, which is
            // the one state the insertion is offered in.
            scene.SelectKey();

            Assert.True(scene.Inspector.IsOpen);
            Assert.Equal(KeyInspectorMode.Macro, scene.Inspector.SelectedMode);
            Assert.True(scene.Insertion.CanInsertIntoMacro());

            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.Remap));

            Assert.False(scene.Insertion.CanInsertIntoMacro());

            scene.OpenMacroPanel();

            Assert.True(scene.Insertion.CanInsertIntoMacro());

            // A rail the user dismissed has no position to act on either.
            scene.Inspector.Close();

            Assert.False(scene.Insertion.CanInsertIntoMacro());
        }

        [AvaloniaFact]
        public void CanInsertIntoMacro_WithTheRailOnItsMacroPanelButNoMacroThere_IsRefused()
        {
            var scene = new Scene();

            scene.SelectKey();
            scene.OpenMacroPanel();

            Assert.Null(scene.Insertion.FindOpenMacro());
            Assert.False(scene.Insertion.CanInsertIntoMacro());

            scene.GiveTheKeyAMacro();

            Assert.NotNull(scene.Insertion.FindOpenMacro());
            Assert.True(scene.Insertion.CanInsertIntoMacro());
        }

        [AvaloniaTheory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void CanInsertIntoMacro_IsRefusedWhileTheEditorIsReadingOrWriting(bool isLoading, bool isBusy)
        {
            var scene = new Scene();

            scene.GiveTheKeyAMacro();
            scene.SelectKey();
            scene.OpenMacroPanel();

            scene.Host.IsLoading = isLoading;
            scene.Host.IsBusy = isBusy;

            Assert.False(scene.Insertion.CanInsertIntoMacro());
        }

        [AvaloniaFact]
        public void CanInsertIntoMacro_WithAPanelAlreadyOpen_IsRefused()
        {
            // A modal owns the keyboard, and ⌘F never replaces one feature panel with another.
            var scene = new Scene();

            scene.GiveTheKeyAMacro();
            scene.SelectKey();
            scene.OpenMacroPanel();

            scene.Host.ActiveOverlay = new TokenPickerOverlayViewModel(
                TokenPickerOverlayViewModel.MacroTitle,
                TokenDialect.Gen1);

            Assert.False(scene.Insertion.CanInsertIntoMacro());
        }

        [AvaloniaFact]
        public void CanOpenSearch_WantsALoadedEditorAndNothingElse()
        {
            // ⌘F always has somewhere to go: with no macro open it is the rail's own search field,
            // so the gate is about the editor rather than about the rail.
            var scene = new Scene();

            Assert.True(scene.Insertion.CanOpenSearch());

            scene.Host.Layout = null;

            Assert.False(scene.Insertion.CanOpenSearch());

            scene.Host.Layout = scene.Layout;
            scene.Host.IsBusy = true;

            Assert.False(scene.Insertion.CanOpenSearch());
        }

        [AvaloniaFact]
        public void InsertIntoOpenMacro_AppendsToTheMacroTheRailHasOpen_AndEndsInTheFunnel()
        {
            // Core announces nothing, so the funnel is what re-reads the step list, the counters,
            // the advisories and the dirty flag (invariant 16).
            var scene = new Scene();
            var macro = scene.GiveTheKeyAMacro();

            scene.SelectKey();
            scene.OpenMacroPanel();

            Assert.True(scene.Insertion.InsertIntoOpenMacro(TestLayouts.Gen1Key("esc")));

            var keystroke = Assert.Single(macro.Keystrokes);

            Assert.Equal("esc", keystroke.Key.Gen1Token);
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaFact]
        public void InsertIntoOpenMacro_WithNoMacroOpen_WritesNothingAndSaysSo()
        {
            var scene = new Scene();

            scene.SelectKey();
            scene.OpenMacroPanel();

            Assert.False(scene.Insertion.InsertIntoOpenMacro(TestLayouts.Gen1Key("esc")));
            Assert.Equal(0, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaFact]
        public void InsertIntoOpenMacro_RefusesAMissingKey()
        {
            var scene = new Scene();

            Assert.Throws<ArgumentNullException>(() => scene.Insertion.InsertIntoOpenMacro(null!));
        }

        [AvaloniaFact]
        public void InsertSpecialAction_ShowsSection116sPicker_AndItsAnswerLandsOnTheMacro()
        {
            // The first of the two insert paths: a modal, because an insertion is a question with
            // one answer that has to come back here.
            var scene = new Scene();
            var macro = scene.GiveTheKeyAMacro();

            scene.SelectKey();
            scene.OpenMacroPanel();

            scene.Insertion.InsertSpecialActionCommand.Execute(null);

            var overlay = Assert.IsType<TokenPickerOverlayViewModel>(Assert.Single(scene.Host.ShownOverlays));

            Assert.Equal(TokenPickerOverlayViewModel.MacroTitle, overlay.Title);

            scene.Accept(overlay, "esc");

            var keystroke = Assert.Single(macro.Keystrokes);

            Assert.Equal("esc", keystroke.Key.Gen1Token);
        }

        [AvaloniaFact]
        public void ADismissedPicker_CanNeverWriteIntoTheMacroAfterwards()
        {
            // The hooks come off on the panel's own Closed, however it closed.
            var scene = new Scene();
            var macro = scene.GiveTheKeyAMacro();

            scene.SelectKey();
            scene.OpenMacroPanel();

            scene.Insertion.InsertSpecialActionCommand.Execute(null);

            var overlay = Assert.IsType<TokenPickerOverlayViewModel>(Assert.Single(scene.Host.ShownOverlays));

            overlay.Cancel();

            scene.Accept(overlay, "esc");

            Assert.Empty(macro.Keystrokes);
        }

        [AvaloniaFact]
        public void InsertSpecialAction_SharesTheEditorsOneRecentStore()
        {
            // An action inserted into a macro is offered by the rail's own `Recent` chip
            // afterwards, because there is one store per editor and this picker is over it.
            var scene = new Scene();

            scene.GiveTheKeyAMacro();
            scene.SelectKey();
            scene.OpenMacroPanel();

            scene.Insertion.InsertSpecialActionCommand.Execute(null);

            var overlay = Assert.IsType<TokenPickerOverlayViewModel>(Assert.Single(scene.Host.ShownOverlays));

            scene.Accept(overlay, "esc");

            Assert.Contains(scene.RecentTokens.Entries, key => key.Gen1Token == "esc");
        }

        [AvaloniaFact]
        public void OpenSearch_WithAMacroOpen_IsTheInsertionPicker()
        {
            var scene = new Scene();

            scene.GiveTheKeyAMacro();
            scene.SelectKey();
            scene.OpenMacroPanel();

            scene.Insertion.OpenSearchCommand.Execute(null);

            var overlay = Assert.IsType<TokenPickerOverlayViewModel>(Assert.Single(scene.Host.ShownOverlays));

            Assert.Equal(TokenPickerOverlayViewModel.MacroTitle, overlay.Title);
            Assert.Equal(0, scene.Host.FocusRemapSearchCalls);
        }

        [AvaloniaFact]
        public void OpenSearch_WithNoMacroOpen_OpensTheRailAndPutsTheCaretInItsOwnField()
        {
            // The rail is not modal, so nothing is opened over anything: the picker is already on
            // screen beside the board.
            var scene = new Scene();

            scene.SelectKey();

            scene.Insertion.OpenSearchCommand.Execute(null);

            Assert.Empty(scene.Host.ShownOverlays);
            Assert.Equal(1, scene.Host.FocusRemapSearchCalls);
            Assert.True(scene.Inspector.IsOpen);
        }

        [AvaloniaFact]
        public void OpenSearch_WithThePickerAlreadyUp_RefocusesItRatherThanOpeningASecond()
        {
            var scene = new Scene();
            var picker = new TokenPickerOverlayViewModel(TokenPickerOverlayViewModel.MacroTitle, TokenDialect.Gen1);
            var focusRequests = 0;

            picker.Picker.FocusRequested += (_, _) => focusRequests++;

            scene.Host.ActiveOverlay = picker;

            scene.Insertion.OpenSearchCommand.Execute(null);

            Assert.Empty(scene.Host.ShownOverlays);
            Assert.Equal(0, scene.Host.FocusRemapSearchCalls);
            Assert.Equal(1, focusRequests);
        }

        [AvaloniaFact]
        public void OpenSearch_WithSomeOtherPanelUp_ReachesNothingBehindTheScrim()
        {
            var scene = new Scene();

            scene.SelectKey();

            scene.Host.ActiveOverlay = new StubOverlay();

            scene.Insertion.OpenSearchCommand.Execute(null);

            Assert.Empty(scene.Host.ShownOverlays);
            Assert.Equal(0, scene.Host.FocusRemapSearchCalls);
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingCollaborator()
        {
            var host = new FakeMacroInsertionHost();

            Assert.Throws<ArgumentNullException>(() => new MacroInsertionHost(null!, new RecentTokenStore()));
            Assert.Throws<ArgumentNullException>(() => new MacroInsertionHost(host, null!));
        }

        /// <summary>An insertion host over a real RGB model and a real rail, with no editor at all.</summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public KeyboardLayerViewModel Layer { get; }

            public KeyInspectorViewModel Inspector { get; }

            public RecentTokenStore RecentTokens { get; }

            public FakeMacroInsertionHost Host { get; }

            public MacroInsertionHost Insertion { get; }

            public Scene()
            {
                Layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
                Layer = KeyboardLayerViewModel.BuildAll(Layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];

                Inspector = new KeyInspectorViewModel(
                    new RelayCommand(() => { }),
                    new RelayCommand(() => { }),
                    new RelayCommand(() => { }, () => false));

                RecentTokens = new RecentTokenStore();

                Host = new FakeMacroInsertionHost
                {
                    Layout = Layout,
                    SelectedLayer = Layer,
                    Inspector = Inspector
                };

                Insertion = new MacroInsertionHost(Host, RecentTokens);
            }

            /// <summary>The cap the rail is pointed at — the "1" of the left typing half.</summary>
            public KeyboardKeyViewModel Key => Layer.FindByIndex(TestLayouts.RgbDigitOneKeyIndex)!;

            public KeyInspectorTabViewModel Tab(KeyInspectorMode mode)
            {
                return Inspector.Tabs.Single(tab => tab.Mode == mode);
            }

            /// <summary>Puts a macro in the position's active slot and hands it back.</summary>
            public Macro GiveTheKeyAMacro()
            {
                var macro = Layout.CreateMacro();

                Key.Key.SetMacro(Key.Key.ActiveMacroIndex, macro);
                Key.RefreshFromModel();

                return macro;
            }

            public void SelectKey()
            {
                Host.SelectedKey = Key;

                Inspector.Refresh(Key, Layer, Layout, EditorAdvisories.Empty);
            }

            public void OpenMacroPanel()
            {
                Inspector.Open();
                Inspector.SelectModeCommand.Execute(Tab(KeyInspectorMode.Macro));
            }

            /// <summary>Picks <paramref name="token"/> in the panel's picker and accepts it.</summary>
            public void Accept(TokenPickerOverlayViewModel overlay, string token)
            {
                overlay.Picker.Query = token;
                overlay.Picker.SelectedRow = overlay.Picker.Rows.First(row => row.Definition.Gen1Token == token);

                overlay.AcceptCommand.Execute(null);
            }
        }

        /// <summary>
        /// The editor as <see cref="MacroInsertionHost"/> sees it: the state its two gates read, and
        /// a record of the three calls a picker makes.
        /// </summary>
        private sealed class FakeMacroInsertionHost : IMacroInsertionHost
        {
            public KeyboardLayout? Layout { get; set; }

            public KeyboardKeyViewModel? SelectedKey { get; set; }

            public KeyboardLayerViewModel? SelectedLayer { get; set; }

            public KeyInspectorViewModel Inspector { get; set; } = null!;

            public EditorOverlayViewModel? ActiveOverlay { get; set; }

            public bool IsLoading { get; set; }

            public bool IsBusy { get; set; }

            public List<EditorOverlayViewModel> ShownOverlays { get; } = [];

            public int FocusRemapSearchCalls { get; private set; }

            public int RefreshCountersCalls { get; private set; }

            public void ShowOverlay(EditorOverlayViewModel overlay)
            {
                ShownOverlays.Add(overlay);

                // The real editor hands the panel to EditorOverlayHost, which is what makes it the
                // active one — and ShowMacroInsertOverlay checks exactly that before it subscribes.
                ActiveOverlay = overlay;
            }

            public void FocusRemapSearch()
            {
                FocusRemapSearchCalls++;
            }

            public void RefreshCounters()
            {
                RefreshCountersCalls++;
            }
        }

        /// <summary>A feature panel that is neither a picker nor a keystroke sink.</summary>
        private sealed class StubOverlay : EditorOverlayViewModel
        {
            public StubOverlay()
                : base("Stub")
            {
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }
    }
}
