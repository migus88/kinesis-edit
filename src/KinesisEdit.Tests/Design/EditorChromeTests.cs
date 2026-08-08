using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The editor shell of docs/design/handoff.md § 2 (mockups 1e, 1f, 2a), rendered: a 46 px
    /// toolbar over a 38 px tab bar, the amber Save, the capability-driven tab set and the Demo
    /// Mode bar.
    /// <para>
    /// It asserts on <b>arranged sizes and named pixels</b>, never on a golden image — the
    /// documented refusal in design-system.md § "Rendered-frame capture". The heights are the
    /// interesting half: they are fixed by design ("a device with a long name may not push the
    /// board down"), and a bar that grows with its content is invisible to any view-model test.
    /// </para>
    /// </summary>
    public class EditorChromeTests
    {
        /// <summary>The design's toolbar height (Themes/Geometry.axaml, <c>HeightToolbar</c>).</summary>
        private const double ToolbarHeight = 46;

        /// <summary>The design's tab-bar height (<c>HeightTabBar</c>).</summary>
        private const double TabBarHeight = 38;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheEditorsToolbar_RendersAtFortySix(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var toolbar = ToolbarOf(view);

            Assert.Equal(ToolbarHeight, toolbar.Bounds.Height);
            Assert.Equal(
                DesignTokens.Resolve("SurfaceBarBrush", ToVariant(variantName)),
                toolbar.Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheEditorsTabBar_RendersAtThirtyEight(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            // The strip paints the whole bar and the layer switcher is laid over its right end, so
            // the strip's own arranged height IS the bar's.
            var strip = view.GetVisualDescendants().OfType<TabStrip>().Single();

            Assert.Equal(TabBarHeight, strip.Bounds.Height);
            Assert.True(
                strip.Bounds.Width > ThemedHost.DefaultWidth / 2,
                $"The tab strip only spanned {strip.Bounds.Width} of the window.");
        }

        /// <summary>
        /// The Layout tab's content row, after issue #119: board, seam, rail — and the rail is a
        /// <b>permanent</b> column. It used to collapse itself when nothing was selected, so its
        /// Auto column measured zero and the board jumped sideways to re-centre the moment a cap was
        /// clicked. Both states are measured here, on one editor, because the defect is the
        /// difference between them and no single-state assertion can see it.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheKeyInspectorRail_HoldsItsColumnWhetherOrNotAKeyIsSelected(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var rail = RailOf(view);
            var board = SectionColumnOf(view);

            Assert.False(editor.Inspector.HasSelection);
            Assert.True(rail.IsEffectivelyVisible);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, rail.Bounds.Width);

            var boardWidth = board.Bounds.Width;

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[0]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(editor.Inspector.HasSelection);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, rail.Bounds.Width);
            Assert.Equal(boardWidth, board.Bounds.Width);
        }

        [AvaloniaFact]
        public async Task TheRailsColumn_IsBoundedByTheGeometryTokens()
        {
            // The drag's own band. The column carries it, so the seam cannot be pulled outside it in
            // the first place; the view model clamps the stored number to the same two, and
            // ShapeAndTypeTokenTests pins the tokens to the values.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var column = RailColumnOf(view);

            Assert.Equal((double)DesignTokens.Resolve("WidthInspectorRailMin", ThemeVariant.Dark), column.MinWidth);
            Assert.Equal((double)DesignTokens.Resolve("WidthInspectorRailMax", ThemeVariant.Dark), column.MaxWidth);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, column.Width.Value);
            Assert.True(column.Width.IsAbsolute, "The rail's column is not a fixed width; the splitter has nothing to resize.");
        }

        [AvaloniaFact]
        public async Task DraggingTheSeam_WidensTheRailAndStoresWhatTheUserChose()
        {
            // The whole mechanism end to end, through Avalonia's own input pipeline: a real press on
            // the seam, a real move, a real release. Nothing here pokes the view model — that is the
            // point, because the failure this catches is a splitter that resizes a column the rail
            // does not live in, which every property-level assertion would miss.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var rail = RailOf(view);
            var seam = Descendants<GridSplitter>(view).Single();

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, rail.Bounds.Width);

            const double pull = 60;

            var grip = seam.TranslatePoint(new Point(seam.Bounds.Width / 2, seam.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The seam is not in the window's visual tree.");

            host.Window.MouseMove(grip);
            host.Window.MouseDown(grip, MouseButton.Left);
            host.Window.MouseMove(grip - new Point(pull, 0), RawInputModifiers.LeftMouseButton);
            host.Window.MouseUp(grip - new Point(pull, 0), MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // Left widens it: the rail is the right-hand column, so the seam moving left gives it
            // room and the board's star column gives the room up.
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, editor.InspectorRailWidth);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, editor.EffectiveInspectorRailWidth);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, rail.Bounds.Width);
        }

        [AvaloniaFact]
        public async Task AStoredWidth_ReachesTheRailsColumn()
        {
            // The other direction, and the half a drag cannot prove: a width that arrives from the
            // preference store has to land on the column, or a remembered rail would open at 268
            // every time.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            editor.InspectorRailWidth = 420;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(420, RailOf(view).Bounds.Width);

            // ...and a width outside the band is pushed back at the column too, not merely inside
            // the view model.
            editor.InspectorRailWidth = 9999;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(HostPreferences.MaximumInspectorRailWidth, RailOf(view).Bounds.Width);
        }

        [AvaloniaFact]
        public async Task TheRailsWidth_IsOneNumberForBothTabs()
        {
            // Issue #124 gave the key inspector and the Lighting tab's mode rail one stored width;
            // issue #128 made them two IsVisible-gated contents of ONE ColumnDefinition, so the two
            // can no longer disagree even in principle. Both halves are asserted: the column is
            // literally the same object for both rails, and a width dragged on the Lighting tab is
            // the width the Keys tab opens at.
            //
            // Driven through the real seam — the editor has exactly one — rather than by poking the
            // view model, because the failure this catches is a splitter resizing a column the rail
            // does not live in, which every property-level assertion would miss.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var keysColumn = RailColumnOf(view);

            editor.SelectedTab = EditorTab.Lighting;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var modeRail = ModeRailOf(view);

            // ONE column, not two that agree: the same ColumnDefinition instance carries both rails.
            Assert.Same(keysColumn, BodyGridOf(view).ColumnDefinitions[Grid.GetColumn(RailHostOf(view))]);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, modeRail.Bounds.Width);

            const double pull = 60;

            var seam = Descendants<GridSplitter>(view).Single();
            var grip = seam.TranslatePoint(new Point(seam.Bounds.Width / 2, seam.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The lighting seam is not in the window's visual tree.");

            host.Window.MouseMove(grip);
            host.Window.MouseDown(grip, MouseButton.Left);
            host.Window.MouseMove(grip - new Point(pull, 0), RawInputModifiers.LeftMouseButton);
            host.Window.MouseUp(grip - new Point(pull, 0), MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, modeRail.Bounds.Width);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, editor.InspectorRailWidth);

            editor.SelectedTab = EditorTab.Keys;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, RailOf(view).Bounds.Width);
        }

        [AvaloniaTheory]
        [InlineData(EditorTab.Keys)]
        [InlineData(EditorTab.Lighting)]
        public async Task TheRail_RunsTheWholeHeightOfTheBody(EditorTab tab)
        {
            // Issue #128's first complaint, as a measurement. The rail used to sit INSIDE the padded
            // content area — a 24,16 inset — so it started 16px below the advisory strip and stopped
            // 16px above the bottom of the window, with the section's action row running on under it.
            // It is a column of the body now: it begins where the strip begins and ends where the
            // window ends, on both tabs that have one.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            editor.SelectedTab = tab;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var rail = RectOf(tab == EditorTab.Keys ? RailOf(view) : ModeRailOf(view), host);
            var strip = RectOf(Descendants<AdvisoryStripView>(view).Single(), host);

            Assert.Equal(strip.Top, rail.Top, precision: 3);
            Assert.Equal(host.Window.ClientSize.Height, rail.Bottom, precision: 3);

            // ...and it is beside the strip, not under it: the two share the body's top edge.
            Assert.True(
                strip.Right <= rail.Left + 0.5,
                $"The advisory strip runs to {strip.Right}, past the rail's left edge at {rail.Left}.");
        }

        [AvaloniaTheory]
        [InlineData(EditorTab.Macros)]
        [InlineData(EditorTab.Settings)]
        public async Task OnASectionWithNoRail_TheColumnAndItsSeam_CollapseToNothing(EditorTab tab)
        {
            // The Macros library and the Settings panel take the whole content width and always did.
            // A fixed-pixel column keeps its width however invisible its contents are, so "no rail
            // here" has to be spelled on the column — this is what says it was.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var full = SectionColumnOf(view).Bounds.Width;

            Assert.True(RailColumnOf(view).Width.Value > 0, "The rail column measured zero on the Keys tab.");

            editor.SelectedTab = tab;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.False(RailHostOf(view).IsVisible);
            Assert.Equal(0, RailColumnOf(view).Width.Value);
            Assert.Equal(0, RailColumnOf(view).MinWidth);

            // The seam goes with it. Its own Bounds are NOT the assertion — Avalonia does not
            // re-arrange an invisible control, so the splitter keeps the 6px rectangle it had when
            // it was last on screen. What the column really measured is the line below.
            Assert.False(Descendants<GridSplitter>(view).Single().IsEffectivelyVisible);

            // The section really got the room, rather than the column merely reporting zero.
            Assert.Equal(host.Window.ClientSize.Width, SectionColumnOf(view).Bounds.Width, precision: 3);
            Assert.True(
                SectionColumnOf(view).Bounds.Width > full,
                $"The section is {SectionColumnOf(view).Bounds.Width} wide with no rail, against {full} with one.");

            // ...and coming back restores the column rather than leaving it collapsed.
            editor.SelectedTab = EditorTab.Keys;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, RailOf(view).Bounds.Width);
            Assert.Equal(full, SectionColumnOf(view).Bounds.Width, precision: 3);
        }

        [AvaloniaFact]
        public async Task TheTabBarRow_CarriesTheLayoutSwitchOnThreeTabsAndTheLightingSwitchOnTheFourth()
        {
            // Issue #128. There are two "layers" in this app and they are not the same object —
            // ledN.txt's against layoutN.txt's — so the editor has two switchers; but the tab bar has
            // one slot for them, and exactly one is ever in it. The Lighting tab's used to be drawn
            // by LightingTabView, above the board, which put the editor's switcher in two different
            // places depending on the section.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var tabBar = TabBarRowOf(view);

            // Both switchers live in the tab-bar row, whichever one is showing.
            Assert.Contains(LayerSwitchOf(view), tabBar.GetVisualDescendants());
            Assert.Contains(LightingLayerSwitchOf(view), tabBar.GetVisualDescendants());

            foreach (var tab in new[] { EditorTab.Keys, EditorTab.Macros, EditorTab.Settings })
            {
                editor.SelectedTab = tab;

                Dispatcher.UIThread.RunJobs();
                host.Capture();

                Assert.True(LayerSwitchOf(view).IsEffectivelyVisible, $"The layout switch is hidden on {tab}.");
                Assert.False(LightingLayerSwitchOf(view).IsEffectivelyVisible, $"The lighting switch is shown on {tab}.");
                Assert.Contains(KeyboardEditorView.LayerSwitchLabel, VisibleTextsOf(tabBar));
            }

            editor.SelectedTab = EditorTab.Lighting;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.False(LayerSwitchOf(view).IsEffectivelyVisible);
            Assert.True(LightingLayerSwitchOf(view).IsEffectivelyVisible);
            Assert.Contains(KeyboardEditorView.LightingLayerSwitchLabel, VisibleTextsOf(tabBar));
            Assert.DoesNotContain(KeyboardEditorView.LayerSwitchLabel, VisibleTextsOf(tabBar));

            // It is the lighting panel's own layers it moves, not the layout's — the whole reason
            // the label still says which.
            Assert.Same(editor.Lighting.Layers, LightingLayerSwitchOf(view).ItemsSource);
            Assert.Same(editor.Lighting.SelectedLayer, LightingLayerSwitchOf(view).SelectedItem);

            // And the Lighting tab no longer draws one of its own.
            Assert.DoesNotContain(
                Descendants<LightingTabView>(view).SelectMany(tab => tab.GetVisualDescendants().OfType<ListBox>()),
                list => list.ItemsSource is IEnumerable<LightingLayerViewModel>);
        }

        [AvaloniaFact]
        public async Task TheLightingLayerSwitch_KeepsTheFirmwareGateAndItsReason()
        {
            // The Fn segment below the LED 1.0.44 gate stays visible and dimmed (specs/07 §3) —
            // the layer exists — and the sentence LightingTabView used to print beside it is the
            // switch's tooltip now, because a 38px bar has no room for it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            editor.SelectedTab = EditorTab.Lighting;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var switcher = LightingLayerSwitchOf(view);

            Assert.True(editor.Lighting.Layers.Count >= 2, "The lighting scene rendered fewer than two layers.");

            foreach (var index in Enumerable.Range(0, editor.Lighting.Layers.Count))
            {
                Assert.Equal(
                    editor.Lighting.Layers[index].IsEnabled,
                    switcher.ContainerFromIndex(index)!.IsEnabled);
            }

            Assert.Equal(editor.Lighting.LayerLockHint, ToolTip.GetTip(switcher));
            Assert.Equal(!editor.Lighting.IsLayerCustomizationAvailable, ToolTip.GetServiceEnabled(switcher));
        }

        [AvaloniaFact]
        public async Task TheProfilePicker_IsInTheTabBarRowAndOnEveryTab()
        {
            // A profile is not a layer: switching it reloads the layout, the led file and the
            // settings alike, so no section is unaffected by it and the picker is on all four. It
            // replaced the toolbar's read-only `Profile 1` label, which was the one thing on this
            // screen the user most wanted to change and could not.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var picker = Descendants<ComboBox>(view).Single(box => box.ItemsSource is IEnumerable<ProfileOptionViewModel>);

            Assert.Contains(picker, TabBarRowOf(view).GetVisualDescendants());
            Assert.True(editor.HasProfiles, "The scene's editor reported no profiles at all.");
            Assert.Same(editor.Profiles, picker.ItemsSource);
            Assert.Same(editor.SelectedProfile, picker.SelectedItem);

            foreach (var tab in Enum.GetValues<EditorTab>())
            {
                editor.SelectedTab = tab;

                Dispatcher.UIThread.RunJobs();
                host.Capture();

                Assert.True(picker.IsEffectivelyVisible, $"The profile picker is hidden on {tab}.");
            }

            // ...and the toolbar's label is gone: the picker already says which profile is open, and
            // a second copy of the word two rows above it was the third place the app said it.
            Assert.DoesNotContain(
                ToolbarOf(view).GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text is { } text
                    && text.StartsWith(KeyboardEditorViewModel.ProfileCaptionPrefix, StringComparison.Ordinal));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task BothTabsDrawTheBoardAtTheSameScale(string variantName)
        {
            // THE ACCEPTANCE CRITERION of issue #124's first defect. The Lighting board used to be
            // drawn smaller than the Keys board for two reasons that had nothing to do with the
            // picture: its column carried a 16 px right gutter the Keys column does not, and its
            // rail was a fixed 300 where the key inspector's was 268. BoardScaleHost resolves the
            // scale from the width its slot offers, so both differences came out as a smaller board.
            //
            // It is measured as the resolved SCALE and the arranged size, on one editor at one
            // window size, because the defect is the difference between the two tabs and no
            // single-tab assertion can see it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var keysScale = ShownBoardScale(view);
            var keysBoard = ShownBoard(view).Bounds.Size;

            editor.SelectedTab = EditorTab.Lighting;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(keysScale > 0, "The Keys board resolved no scale at all.");
            Assert.Equal(keysScale, ShownBoardScale(view));
            Assert.Equal(keysBoard, ShownBoard(view).Bounds.Size);

            // ...and they stay equal at a dragged rail, which is the whole point of the two tabs
            // sharing one width: a board that only matched at the default would drift apart the
            // first time the seam was moved.
            editor.InspectorRailWidth = 380;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var wideLightingScale = ShownBoardScale(view);

            Assert.True(wideLightingScale < keysScale, "A wider rail did not shrink the board at all.");

            editor.SelectedTab = EditorTab.Keys;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(wideLightingScale, ShownBoardScale(view));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheToolbarCarriesHomeSaveAndTheStatusChip(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var toolbar = ToolbarOf(view);
            var buttons = toolbar.GetVisualDescendants().OfType<Button>().ToArray();

            Assert.Contains(buttons, button => Equals(button.Content, "Home"));
            Assert.Contains(buttons, button => Equals(button.Content, "Save"));

            // No eject, though mockup 1e draws one: nothing ejects implicitly, and eject is the
            // dashboard card's own deliberate action (issue #88). Recorded in design-system.md.
            Assert.DoesNotContain(buttons, button => Equals(button.Content, "Eject"));

            Assert.Single(toolbar.GetVisualDescendants().OfType<StatusChipView>());
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheToolbarsHomeButton_RunsTheShellsHomeCommand(string variantName)
        {
            // The reason IShellChrome exists: this view is hosted standalone here, so a
            // $parent[MainWindow] binding would leave the button with no command at all.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var home = ToolbarOf(view)
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, "Home"));

            Assert.Same(chrome.HomeCommand, home.Command);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ADirtySave_PaintsTheAdvisoryStrongRampAndACleanOneDoesNot(string variantName)
        {
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var save = SaveButtonOf(view);

            Assert.DoesNotContain("dirty", save.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), save.Background);

            // The class is what the view writes; the amber lives in the control theme.
            save.Classes.Add("dirty");

            host.Capture();

            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryStrongBrush", variant), save.Background);

            save.Classes.Remove("dirty");

            host.Capture();

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), save.Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ADirtySave_StaysOnTheAdvisoryRampUnderThePointerAndUnderAPress(string variantName)
        {
            // Contract 5: `.dirty` and `:pressed` can both be true, and the accent ramp above
            // claims `:pressed` too. Before this was spelled out in the selectors, only declaration
            // order kept a pressed dirty Save amber — reorder Buttons.axaml and it silently went
            // back to the accent. All three states are asserted, so the rules are pinned as
            // mutually exclusive rather than as a file order.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var save = SaveButtonOf(view);
            var accent = DesignTokens.Resolve("AccentBrush", variant);
            var resting = DesignTokens.Resolve("StatusAdvisoryStrongBrush", variant);
            var hover = DesignTokens.Resolve("StatusAdvisoryBrush", variant);

            save.Classes.Add("dirty");

            host.Capture();

            Assert.Equal(resting, save.Background);

            // The pseudo-classes are raised AFTER Show: a control styled before it is in a themed
            // tree resolves against nothing.
            SetPseudoClasses(save, ":pointerover");

            host.Capture();

            Assert.Equal(hover, save.Background);

            // Pointer down raises BOTH, and a press falls back to the resting strength.
            SetPseudoClasses(save, ":pressed");

            host.Capture();

            Assert.Equal(resting, save.Background);
            Assert.NotEqual(accent, save.Background);

            // A keyboard press — :pressed with no :pointerover — lands in the same rest rule.
            SetPseudoClasses(save, false, ":pointerover");

            host.Capture();

            Assert.Equal(resting, save.Background);
            Assert.NotEqual(accent, save.Background);
        }

        [AvaloniaFact]
        public void TheDirtyRampAndTheAccentRamp_AreDisjointBySelectorRatherThanByFileOrder()
        {
            // Contract 5, and the one claim about the amber Save that no rendered pixel can reach.
            // `.dirty` and `:pressed` are both true on a Save under the finger, so the winner is
            // whichever style Avalonia applied last — the file's order — unless the selectors say
            // otherwise. ADirtySave_StaysOnTheAdvisoryRampUnderThePointerAndUnderAPress pins
            // today's outcome and would go on passing after a reorder made it accidental; this
            // reads the source instead.
            var theme = PrimaryActionButtonMarkup();
            var checkedSelectors = 0;

            foreach (var selector in StateSelectorsIn(theme))
            {
                Assert.True(
                    selector.Contains(".dirty", StringComparison.Ordinal),
                    $"The state '{selector}' can be true at the same time as `.dirty` and does not say "
                    + "which wins; it beats — or loses to — the amber only by file order.");

                checkedSelectors++;
            }

            // A guard that matched nothing would pass for the wrong reason: four accent-ramp
            // states, four amber ones.
            Assert.Equal(8, checkedSelectors);
        }

        [AvaloniaFact]
        public async Task ADirtySave_ReachesTheGlassInTheAmberTheDesignNames()
        {
            // Through the whole system, not just the resolved property: the token, under a variant,
            // applied by the control theme, drawn by Skia, read back as a pixel.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var save = SaveButtonOf(view);

            save.Classes.Add("dirty");

            var frame = host.Capture();

            // Inside the fill but clear of both the 1px border and the centred label: the sample
            // point has to be somewhere flat, never on a glyph or an anti-aliased edge.
            var sample = save.TranslatePoint(new Point(5, save.Bounds.Height / 2), host.Window);

            Assert.NotNull(sample);

            var expected = DesignTokens.ResolveBrushColor("StatusAdvisoryStrongBrush", ThemeVariant.Dark);

            Assert.Equal(expected, FramePixels.At(frame, (int)sample.Value.X, (int)sample.Value.Y));
        }

        [AvaloniaFact]
        public async Task TheEditorsSaveButton_TracksTheSessionsDirtyState()
        {
            // The binding itself, so the class the test above drives by hand is the class the
            // running app writes.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var save = SaveButtonOf(view);

            Assert.False(editor.IsDirty);
            Assert.DoesNotContain("dirty", save.Classes);

            scenes.MarkSessionDirty();

            // Any path that ends in RefreshCounters would do; this one now confirms first
            // (NotificationKeys.ResetLayer), and the scene's fake answers Ok unless told otherwise.
            ((FakeNotificationService)scenes.Notifications).OutcomeToReturn = new MessageBoxOutcome
            {
                Result = MessageBoxResult.Yes
            };

            editor.ResetLayoutCommand.Execute(null);

            host.Capture();

            Assert.True(editor.IsDirty);
            Assert.Contains("dirty", save.Classes);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ThePedalEditorsSave_TakesTheSameAmberRampWhenItsSessionIsDirty(string variantName)
        {
            // handoff.md § 2 makes the dirty Save a property of the **editor shell**, not of one
            // board, so the rule has to hold on the Savant Elite2 as well as on the keyboard. Its
            // toolbar is still the pre-redesign one — issue #53 rebuilds it — but the button obeys.
            //
            // "Accent when clean" is the keyboard's half of this and is asserted above
            // (ADirtySave_PaintsTheAdvisoryStrongRampAndACleanOneDoesNot). It is not assertable
            // here: this board's CanSave requires something to write, so a clean pedal Save is
            // *disabled* and wears BaseButton's disabled face. What matters — and what `.dirty`
            // itself carries `:not(:disabled)` for — is that the amber appears only with unsaved
            // work and leaves again the moment the file is written.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(SavantElitePedalView).FullName!);
            var pedal = (SavantElitePedalViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var save = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, "Save"));

            var amber = DesignTokens.Resolve("StatusAdvisoryStrongBrush", variant);

            Assert.False(pedal.HasUnsavedChanges);
            Assert.DoesNotContain("dirty", save.Classes);
            Assert.NotEqual(amber, save.Background);

            // Through the binding the app uses, not by hand: programming an input is what makes
            // the session dirty (12 §5).
            pedal.BeginEditCommand.Execute(pedal.Inputs[0]);

            scenes.Capture.RaiseKeystroke(PedalTokenMap.Resolve("a")!);

            pedal.DoneCommand.Execute(null);

            host.Capture();

            Assert.True(pedal.HasUnsavedChanges);
            Assert.Contains("dirty", save.Classes);
            Assert.True(save.IsEffectivelyEnabled, "A disabled Save cannot wear the amber (`:not(:disabled)`).");
            Assert.Equal(amber, save.Background);

            await pedal.SaveCommand.ExecuteAsync(null);

            host.Capture();

            Assert.False(pedal.HasUnsavedChanges);
            Assert.DoesNotContain("dirty", save.Classes);
            Assert.NotEqual(amber, save.Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ADeviceWithoutLighting_RendersNoLightingTab(string variantName)
        {
            // "Absent features are not shown, not disabled." The TKO's led file carries an edge
            // section this app cannot edit yet (#40), so the tab is gone rather than dark.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            view.DataContext = scenes.CreateEditorFor(DeviceId.Tko);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var strip = view.GetVisualDescendants().OfType<TabStrip>().Single();
            var captions = CaptionsOf(strip);

            Assert.Equal(new[] { "Layout", "Macros", "Settings" }, captions);
            Assert.All(
                Enumerable.Range(0, strip.ItemCount),
                index => Assert.True(strip.ContainerFromIndex(index)!.IsEnabled));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheLightingBoard_RendersAllFourTabs(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var strip = view.GetVisualDescendants().OfType<TabStrip>().Single();

            Assert.Equal(new[] { "Layout", "Macros", "Lighting", "Settings" }, CaptionsOf(strip));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheDemoModeBar_AppearsOnlyInDemoModeAndPaintsTheDemoRamp(string variantName)
        {
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var connected = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using (var host = ThemedHost.Show(connected, variant))
            {
                host.Capture();

                Assert.Null(DemoBarOf(connected));
            }

            using var demoScenes = new ViewSceneFactory();

            var demo = await demoScenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            demo.DataContext = await demoScenes.CreateDemoModeContextAsync(typeof(KeyboardEditorView));

            using var demoHost = ThemedHost.Show(demo, variant);

            demoHost.Capture();

            var bar = DemoBarOf(demo);

            Assert.NotNull(bar);

            // Purple, not amber: demo mode is its own state and amber is reserved for advisories.
            Assert.Equal(DesignTokens.Resolve("StatusDemoTintBrush", variant), bar.Background);
            Assert.NotEqual(DesignTokens.Resolve("StatusAdvisoryTintBrush", variant), bar.Background);
        }

        [AvaloniaFact]
        public async Task TheDemoModeBar_CarriesItsVerbatimCopyAndBothOfTheMockupsActions()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            view.DataContext = await scenes.CreateDemoModeContextAsync(typeof(KeyboardEditorView));

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var bar = DemoBarOf(view);

            Assert.NotNull(bar);

            // The bar's own sentence, not the two buttons' labels (which are TextBlocks too).
            var text = Assert.Single(
                bar.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Classes.Contains("body"));

            Assert.Equal(
                "Demo Mode — no keyboard attached. Nothing you change here is written anywhere.",
                text.Text);
            Assert.Equal(KeyboardEditorViewModel.DemoModeBarMessage, text.Text);

            var actions = bar.GetVisualDescendants().OfType<Button>().ToArray();
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            // BOTH of the mockup's actions, in its order. `Export layout to file…` is drawn because
            // it is genuinely live: demo mode opens a real session over the fixture v-Drive, and an
            // export writes to a folder the user picked rather than to the drive (§11.5), so 03
            // §3.5 is untouched by it. It was withheld for exactly as long as demo mode had nothing
            // to serialize, which is no longer true.
            Assert.Equal(2, actions.Length);

            Assert.Equal(KeyboardEditorViewModel.DemoModeExportCaption, actions[0].Content);
            Assert.Equal("Export layout to file…", actions[0].Content);
            Assert.Same(editor.ExportCommand, actions[0].Command);

            Assert.Equal(KeyboardEditorViewModel.DemoModeConnectCaption, actions[1].Content);
            Assert.Same(editor.Shell!.HomeCommand, actions[1].Command);

            // Drawn *and* enabled — a dead control on this bar is what the earlier deviation
            // refused, and the reason it is legal now.
            Assert.True(editor.ExportCommand.CanExecute(null));
            Assert.True(actions[0].IsEffectivelyEnabled);

            // Save and Import are the two that stay refused, and neither is on this bar.
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.ImportCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public async Task TheDemoPill_IsGoneFromTheEditorHeader()
        {
            // The toolbar's status chip and the Demo Mode bar both say it now; a third badge for
            // the same fact was the header the redesign replaced. The chip's own label is still a
            // demo pill, and legitimately so — it is the one this replaced the header's with.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            view.DataContext = await scenes.CreateDemoModeContextAsync(typeof(KeyboardEditorView));

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var demoPills = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.Classes.Contains("pill") && block.Classes.Contains("statusDemo"))
                .ToArray();

            Assert.All(
                demoPills,
                pill => Assert.NotEmpty(pill.GetVisualAncestors().OfType<StatusChipView>()));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheLayerSwitcher_CarriesAKbdChipPerSegment(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chips = view.GetVisualDescendants()
                .OfType<ContentControl>()
                .Where(chip => chip.Classes.Contains("kbd"))
                .ToArray();

            Assert.Equal(editor.Layers.Count, chips.Length);

            // The chip hosts a mark-plus-text panel, not a string: U+2325 is in neither embedded
            // IBM Plex family, so the ⌥ is `IconOption` geometry and only the number is type. The
            // legend is therefore read out of the chip in two pieces.
            Assert.Equal(
                editor.Layers.Select(layer => layer.ShortcutHint),
                chips.Select(chip => Assert.Single(chip.GetVisualDescendants().OfType<TextBlock>()).Text));

            var marks = chips
                .Select(chip => Assert.Single(chip.GetVisualDescendants().OfType<Icon>()))
                .ToArray();

            Assert.All(marks, mark => Assert.Equal(DesignTokens.Resolve("IconOption", ToVariant(variantName)), mark.Data));

            // Shown on macOS, where the legend reads `⌥1`; hidden on Windows and Linux, where it
            // reads `Alt+1` and there is no Option key to name. Both branches are asserted against
            // the platform the suite is running on, because `KeyCaption.IsMacOs` is resolved once
            // per process and the binding is what is under test here.
            Assert.All(marks, mark => Assert.Equal(KeyCaption.IsMacOs, mark.IsVisible));
            Assert.All(
                editor.Layers,
                layer => Assert.Equal(KeyCaption.IsMacOs, layer.ShowsOptionMark));

            if (KeyCaption.IsMacOs)
            {
                Assert.All(editor.Layers, layer => Assert.DoesNotContain("Alt", layer.ShortcutHint, StringComparison.Ordinal));
            }
            else
            {
                Assert.All(editor.Layers, layer => Assert.StartsWith("Alt+", layer.ShortcutHint, StringComparison.Ordinal));
            }
        }

        [AvaloniaFact]
        public async Task TheProvisionalActionRow_ShrinksAsEachActionReachesItsRealHome()
        {
            // Issue #91 gave `Reset Layer` its home under the board, beside the counts it acts on,
            // and it took the mockups' sentence case with it (`Reset layer`). Issue #92 took two
            // more into the key inspector rail: `Reset Key` is its footer's `Revert key`, and
            // `Tap and Hold` is a rail panel rather than a modal. Issue #93 took `Insert Delay`:
            // §11.3 is edited in place on the rail's Macro panel, so no command is left for a
            // button here to run. What is left is the rest of #93's — the editor-level action bar.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var provisional = Assert.Single(view.GetVisualDescendants().OfType<WrapPanel>());
            var captions = provisional.Children.OfType<Button>().Select(button => button.Content as string).ToArray();

            Assert.Equal(
                new[] { "Reset Layout", "Special Action", "Export", "Import" },
                captions);

            // And no command moved owners with it: each new home runs the editor's own instance.
            var row = Assert.Single(view.GetVisualDescendants().OfType<BoardLegendView>());
            var moved = Assert.Single(
                row.GetVisualDescendants().OfType<Button>(),
                button => Equals(button.Content, BoardLegendViewModel.ResetLayerCaption));

            Assert.Same(editor.ResetLayerCommand, moved.Command);
            Assert.Same(editor.ResetKeyCommand, editor.Inspector.ResetKeyCommand);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task MainWindow_WithTheEditorOpen_ShowsExactlyOneFortySixHighBar(string variantName)
        {
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var shell = scenes.CreateShell();
            var window = new MainWindow(shell, scenes.Notifications);

            using var host = ThemedHost.Show(window, variant);

            host.Capture();

            // On the dashboard the shell's own bar is the one.
            Assert.Equal(1, BarCount(window));

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            host.Capture();

            Assert.IsType<KeyboardEditorViewModel>(shell.Editor);
            Assert.False(shell.IsShellChromeVisible);

            // Still one — the editor's, not the shell's — and never two status chips. The shell's
            // bar is still IN the tree (IsVisible only stops it being rendered), so both counts go
            // through IsEffectivelyVisible, which is what the user's eye counts.
            Assert.Equal(1, BarCount(window));
            Assert.Single(window.GetVisualDescendants().OfType<StatusChipView>(), chip => chip.IsEffectivelyVisible);
        }

        /// <summary>Every visible 46-high bar on screen, whoever drew it.</summary>
        private static int BarCount(Visual root)
        {
            return root.GetVisualDescendants()
                .OfType<Border>()
                .Count(border => border.IsVisible
                    && border.IsEffectivelyVisible
                    && Math.Abs(border.Bounds.Height - ToolbarHeight) < 0.5
                    && border.Bounds.Width > ThemedHost.DefaultWidth / 2);
        }

        /// <summary>The editor's own toolbar: the 46-high bar that carries the Save button.</summary>
        private static Border ToolbarOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => Math.Abs(border.Bounds.Height - ToolbarHeight) < 0.5
                    && border.GetVisualDescendants().OfType<Button>().Any(button => Equals(button.Content, "Save")));
        }

        private static Button SaveButtonOf(Control view)
        {
            return ToolbarOf(view)
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, "Save"));
        }

        /// <summary>The Demo Mode bar, or null when it is not on screen.</summary>
        private static Border? DemoBarOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .FirstOrDefault(border => border.IsVisible
                    && border.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Any(block => block.Text == KeyboardEditorViewModel.DemoModeBarMessage));
        }

        private static string?[] CaptionsOf(TabStrip strip)
        {
            return Enumerable.Range(0, strip.ItemCount)
                .Select(index => strip.ContainerFromIndex(index)!)
                .SelectMany(container => container.GetVisualDescendants().OfType<TextBlock>())
                .Select(block => block.Text)
                .ToArray();
        }

        /// <summary>
        /// The <c>PrimaryActionButton</c> control theme's own markup, comments stripped — the theme
        /// the amber Save lives in, and the only one in <c>Buttons.axaml</c> that carries two ramps.
        /// </summary>
        private static string PrimaryActionButtonMarkup()
        {
            var markup = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Themes/ControlThemes/Buttons.axaml"]);
            var start = markup.IndexOf("x:Key=\"PrimaryActionButton\"", StringComparison.Ordinal);

            Assert.True(start >= 0, "Buttons.axaml no longer declares a PrimaryActionButton theme.");

            var end = markup.IndexOf("</ControlTheme>", start, StringComparison.Ordinal);

            Assert.True(end > start, "The PrimaryActionButton theme is not closed.");

            return markup[start..end];
        }

        /// <summary>
        /// The pointer-state selectors of <paramref name="theme"/> — every one that <b>requires</b>
        /// <c>:pointerover</c> or <c>:pressed</c> rather than merely excluding it, which is the set
        /// that can collide with the dirty class on one button.
        /// </summary>
        private static IEnumerable<string> StateSelectorsIn(string theme)
        {
            foreach (Match match in Regex.Matches(theme, "Selector=\"(?<selector>[^\"]+)\""))
            {
                foreach (var selector in match.Groups["selector"].Value.Split(','))
                {
                    var trimmed = selector.Trim();
                    var required = Regex.Replace(trimmed, @":not\([^)]*\)", string.Empty);

                    if (required.Contains(":pointerover", StringComparison.Ordinal)
                        || required.Contains(":pressed", StringComparison.Ordinal))
                    {
                        yield return trimmed;
                    }
                }
            }
        }

        /// <summary>
        /// Raises (or clears) pseudo-classes by hand. The headless session has no pointer, so
        /// <c>:pointerover</c> and <c>:pressed</c> are set through the same interface Avalonia's own
        /// input code uses — and only ever <b>after</b> <see cref="ThemedHost.Show"/>, because a
        /// control styled before it is in a themed tree resolves against nothing.
        /// </summary>
        private static void SetPseudoClasses(Control control, bool isSet, params string[] pseudoClasses)
        {
            var classes = (IPseudoClasses)control.Classes;

            foreach (var pseudoClass in pseudoClasses)
            {
                classes.Set(pseudoClass, isSet);
            }
        }

        /// <inheritdoc cref="SetPseudoClasses(Control, bool, string[])" />
        private static void SetPseudoClasses(Control control, params string[] pseudoClasses)
        {
            SetPseudoClasses(control, true, pseudoClasses);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>The key inspector rail's own frame, wherever it is on screen.</summary>
        private static Control RailOf(Control view)
        {
            return Descendants<KeyInspectorView>(view).Single();
        }

        /// <summary>The Lighting tab's mode rail — the other face of the same column.</summary>
        private static Control ModeRailOf(Control view)
        {
            return Descendants<LightingModeRailView>(view).Single();
        }

        /// <summary>
        /// The open section's column — the seam's left-hand neighbour, which since issue #128 holds
        /// the advisory strip as well as the padded content.
        /// </summary>
        private static Control SectionColumnOf(Control view)
        {
            var grid = BodyGridOf(view);

            return grid.Children.OfType<Control>().Single(child => Grid.GetColumn(child) == 0);
        }

        /// <summary>The editor's body grid: section | seam | rail.</summary>
        private static Grid BodyGridOf(Control view)
        {
            return (Grid)Descendants<GridSplitter>(view).Single().GetVisualParent()!;
        }

        /// <summary>
        /// The rail column's host — the direct child of the body grid that both rails live in. It is
        /// found by walking up from a rail rather than by name, so it survives the host being
        /// rebuilt into something else.
        /// </summary>
        private static Control RailHostOf(Control view)
        {
            var grid = BodyGridOf(view);

            return RailOf(view)
                .GetSelfAndVisualAncestors()
                .OfType<Control>()
                .First(control => ReferenceEquals(control.GetVisualParent(), grid));
        }

        /// <summary>The rectangle <paramref name="control"/> occupies, in the window's coordinates.</summary>
        private static Rect RectOf(Visual control, ThemedHost host)
        {
            return new Rect(control.TranslatePoint(default, host.Window)!.Value, control.Bounds.Size);
        }

        /// <summary>
        /// The tab-bar row: the section strip's own container, which since issue #128 also carries
        /// the profile picker and both layer switches.
        /// <para>
        /// The strip is named by what it lists rather than by being the only <see cref="TabStrip"/>
        /// on screen — the key inspector's mode tabs are one too, the moment a key is selected.
        /// </para>
        /// </summary>
        private static Control TabBarRowOf(Control view)
        {
            return (Control)Descendants<TabStrip>(view)
                .Single(strip => strip.ItemsSource is IEnumerable<EditorTabViewModel>)
                .GetVisualParent()!;
        }

        /// <summary>The LAYOUT layer switch — layoutN.txt's layer, what a key does.</summary>
        private static ListBox LayerSwitchOf(Control view)
        {
            return Descendants<ListBox>(view).Single(list => list.ItemsSource is IEnumerable<KeyboardLayerViewModel>);
        }

        /// <summary>The LIGHTING layer switch — ledN.txt's layer, what a key looks like.</summary>
        private static ListBox LightingLayerSwitchOf(Control view)
        {
            return Descendants<ListBox>(view).Single(list => list.ItemsSource is IEnumerable<LightingLayerViewModel>);
        }

        private static IReadOnlyList<string> VisibleTextsOf(Visual root)
        {
            return root.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty)
                .ToArray();
        }

        /// <summary>
        /// The keyboard picture of whichever tab is open. Both tabs draw one, over the same layer,
        /// and only one of them is ever on screen.
        /// </summary>
        private static KeyboardView ShownBoard(Control view)
        {
            return Descendants<KeyboardView>(view).Single(board => board.IsEffectivelyVisible);
        }

        /// <summary>
        /// The factor the shown picture is drawn at. It is read off the arranged host rather than
        /// computed, because the question is what the user is looking at.
        /// </summary>
        private static double ShownBoardScale(Control view)
        {
            return Descendants<BoardScaleHost>(view).Single(host => host.IsEffectivelyVisible).Scale;
        }

        /// <summary>The rail's <see cref="ColumnDefinition"/> — where its width lives since #119.</summary>
        private static ColumnDefinition RailColumnOf(Control view)
        {
            return BodyGridOf(view).ColumnDefinitions[Grid.GetColumn(RailHostOf(view))];
        }

        private static IEnumerable<T> Descendants<T>(Control view) where T : Visual
        {
            return view.GetVisualDescendants().OfType<T>();
        }
    }
}
