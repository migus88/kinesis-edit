using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The wiring between the control-theme layer and the app: the bridge styles in <c>Styles/</c>
    /// that point a class at a <see cref="ControlTheme"/>, and the call sites that name a theme
    /// outright.
    /// <para>
    /// The theme suites beside this one prove that each theme <i>paints</i> what the design asks
    /// for; this one proves the themes are actually <b>reached</b>, on the real views, and that the
    /// two call sites that stopped being rows of buttons still drive the same commands. A bridge
    /// that is missing fails silently — the control keeps Fluent's template and merely looks a
    /// little wrong — which is exactly what no rendering test can see.
    /// </para>
    /// </summary>
    public partial class ControlThemeBridgeTests
    {
        /// <summary>
        /// Directories whose XAML is allowed to name a template part. Only the layer that declares
        /// a template may select into one; see Themes/ControlThemes/Shared.axaml, contract 2.
        /// </summary>
        private const string ControlThemeDirectory = "Themes/ControlThemes/";

        /// <summary>Each authoring class, a view that writes it, and the theme it has to land on.</summary>
        public static TheoryData<string, string, string> BridgedClasses()
        {
            var cases = new TheoryData<string, string, string>();

            foreach (var (viewTypeName, className, themeKey) in new[]
            {
                (typeof(KeyCapView).FullName!, "keyCap", "KeyCapButton"),
                (typeof(StatusChipView).FullName!, "statusChip", "StatusPill"),

                // The app bar. The nav pills were unclassed Fluent buttons until the dashboard
                // rebuild; its status chip is now the shared StatusChipView above, which the
                // editor's toolbar draws too.
                (typeof(MainWindow).FullName!, "navPill", "NavPill"),

                // The dashboard. Three button roles that had a bridge and no call site at all
                // until the cards were rebuilt on them.
                (typeof(DeviceCardView).FullName!, "primaryAction", "PrimaryActionButton"),
                (typeof(DeviceCardView).FullName!, "secondary", "SecondaryButton"),
                (typeof(DeviceCardView).FullName!, "eject", "EjectButton"),
                (typeof(DashboardView).FullName!, "secondary", "SecondaryButton"),

                // The dashboard's empty state (docs/design/mockups.md §1d) — the three weights of
                // its action row, and the first call site `link` has ever had.
                (typeof(NoDeviceView).FullName!, "primaryAction", "PrimaryActionButton"),
                (typeof(NoDeviceView).FullName!, "secondary", "SecondaryButton"),
                (typeof(NoDeviceView).FullName!, "link", "LinkButton"),

                // The editor shell: the ⌥n legend on each layer segment of the tab bar, its two
                // button roles, and the advisory strip's bordered `Review N`.
                (typeof(KeyboardEditorView).FullName!, "kbd", "KbdChip"),
                (typeof(KeyboardEditorView).FullName!, "primaryAction", "PrimaryActionButton"),
                (typeof(KeyboardEditorView).FullName!, "secondary", "SecondaryButton"),

                // The rail's drag seam (issue #119) — the app's one resizable edge, and its one
                // GridSplitter. Without the bridge it keeps Fluent's own splitter template, which
                // paints a grey band the width of the whole hit target: visible, plausible, and
                // nothing else in the suite would notice.
                (typeof(KeyboardEditorView).FullName!, "railSplitter", "RailSplitter"),
                (typeof(AdvisoryStripView).FullName!, "secondary", "SecondaryButton"),
                // The Settings tab. `settingSwitch` was the 27th control theme and is the only bridge
                // in the app that goes onto a ToggleButton — the switch cannot be a ToggleSwitch
                // (Themes/ControlThemes/Fields.axaml says why), so nothing but this row would
                // notice the bridge going missing.
                (typeof(KeyboardSettingsView).FullName!, "settingSwitch", "SettingSwitch"),
                (typeof(KeyboardSettingsView).FullName!, "primaryAction", "PrimaryActionButton"),

                // Its second section. `link` here is the "+N more" disclosure; the swatch strip's
                // Replace/Clear and its hex box are only rendered while the store is writable,
                // which the editor scene's session makes it.
                (typeof(AppPreferencesView).FullName!, "link", "LinkButton"),
                (typeof(AppPreferencesView).FullName!, "secondary", "SecondaryButton"),
                (typeof(AppPreferencesView).FullName!, "monoValue", "MonoValueField"),

                // The Lighting tab (issue #94). The tab is the BOARD COLUMN alone since issue #128 —
                // the mode rail is the editor shell's own full-height rail now, not a child of this
                // view — so only what the board itself draws is reachable through this scene.
                //
                // `modeOption` left with the mode list: the mode is a ComboBox, and its rows are
                // ComboBoxItems that a Button theme cannot reach. `Button.modeOption`, the
                // `ModeOption` ControlTheme and its bridge in Styles/Editor.axaml now have no call
                // site at all; `SegmentedAndTabThemeTests` still builds the button by hand, so the
                // theme is covered but unused, and retiring it is its own decision.
                //
                // `colorSlot` and `directionSegment` are the rail's, and both are now gated on the
                // selected mode's parameters (#128 — the rail shows only what the mode uses), so
                // neither has a container to assert on until a mode that has one is picked. They
                // are in the speed bar's company below rather than here, for exactly that reason.
                (typeof(LightingTabView).FullName!, "secondary", "SecondaryButton"),

                // Issue #124's. `zoneButton` had NO theme at all until then — it set padding, a
                // margin and a cursor over Fluent's own template — which was survivable only while a
                // zone painted on the spot and had no selected state to show. A zone selects now, so
                // it wears the same independent-latch face the co-triggers do; a missing bridge here
                // is the exact failure this suite exists for, since the chip would still draw.
                //
                // #124's OTHER two are gone with issue #128: `primaryAction` was the rail footer's
                // Apply, and there is no Apply any more (every control commits as it is touched), and
                // `railSplitter` was the Lighting tab's own seam, which is the editor shell's one
                // seam now — already covered by the KeyboardEditorView row above.
                (typeof(LightingTabView).FullName!, "zoneButton", "ToggleSegment"),
                // MacroDelayOverlayView's `monoValue` row went with the view in #93; the delay is
                // edited in the rail's composer now (#139), and that field is covered below.

                // The key inspector rail (issue #92), which is where the two deleted overlays'
                // roles now live — and where `FilterChip` finally got the call site #86 wrote it
                // for. `recordAction` is the pair of red Record buttons; a missing bridge on either
                // is invisible to every rendering test, because the control simply keeps Fluent's.
                (typeof(TapAndHoldPanelView).FullName!, "actionField", "TokenField"),
                (typeof(TapAndHoldPanelView).FullName!, "recordAction", "DiscardButton"),
                (typeof(TapAndHoldPanelView).FullName!, "link", "LinkButton"),
                (typeof(RemapPanelView).FullName!, "actionField", "TokenField"),
                (typeof(RemapPanelView).FullName!, "recordAction", "DiscardButton"),
                (typeof(TokenPickerView).FullName!, "searchField", "SearchField"),
                (typeof(TokenPickerView).FullName!, "filterChip", "FilterChip"),
                // `ghost` left this list with issue #119: the rail's only ghost button was the
                // header's close mark, and a permanent column has nothing to close. The class still
                // has call sites — MacroInspectorPanelView's, below — so the bridge itself is not
                // orphaned.
                (typeof(KeyInspectorView).FullName!, "secondary", "SecondaryButton"),
                (typeof(LockedKeyPanelView).FullName!, "secondary", "SecondaryButton"),

                // The rail's Macro panel (issue #93). `monoValue` moved here from the deleted Macro
                // Timing Delays modal — §11.3's millisecond field is the same typed value in the
                // same mono face, and since #139 it sits in the composer, which edits whichever step
                // is selected rather than opening over one row — and `macroStepRow` is the
                // step list's own row, which is a RowButton so hover, press and the selected face
                // are the theme's rather than this panel's.
                (typeof(MacroInspectorPanelView).FullName!, "recordAction", "DiscardButton"),
                (typeof(MacroInspectorPanelView).FullName!, "macroStepRow", "RowButton"),
                (typeof(MacroInspectorPanelView).FullName!, "monoValue", "MonoValueField"),
                (typeof(MacroInspectorPanelView).FullName!, "toggleSegment", "ToggleSegment"),
                (typeof(MacroInspectorPanelView).FullName!, "ghost", "GhostButton")
            })
            {
                cases.Add(viewTypeName, className, themeKey);
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(BridgedClasses))]
        public async Task EveryBridgedClass_ReachesItsThemeOnARealView(
            string viewTypeName,
            string className,
            string themeKey)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(viewTypeName);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            AssertClassCarriesTheme(view, className, themeKey);
        }

        [AvaloniaFact]
        public void TheUnsavedChangesModal_BridgesTheDiscardAnswerAndLeavesSaveOnTheAccent()
        {
            // `discard` is bridged for exactly one card and reached from no other: the answer that
            // loses data in the leave-with-unsaved modal (mockup 1f). It cannot join the table
            // above, because which button wears it depends on the request — an ordinary box has no
            // destructive answer at all, and its No is `secondary`.
            using var scenes = new ViewSceneFactory();

            var view = new MessageBoxView
            {
                DataContext = scenes.CreateUnsavedChangesMessageBox()
            };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            AssertClassCarriesTheme(view, "discard", "DiscardButton");

            // The other half of the claim: naming one answer destructive must not pull the
            // affirmative off the accent with it. Both faces exist on this one card.
            AssertClassCarriesTheme(view, "primaryAction", "PrimaryActionButton");
            AssertClassCarriesTheme(view, "secondary", "SecondaryButton");
        }

        [AvaloniaFact]
        public async Task ThePedalsEntryPanel_BridgesItsToggleAndItsTokenField()
        {
            // Both live inside the panel that opens under the input being programmed, so they exist
            // only once an edit has started (specs/12-savant-elite.md §5).
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(SavantElitePedalView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var pedal = (SavantElitePedalViewModel)view.DataContext!;

            pedal.BeginEditCommand.Execute(pedal.Inputs[0]);

            host.Capture();

            AssertClassCarriesTheme(view, "toggleSegment", "ToggleSegment");
            AssertClassCarriesTheme(view, "entryBox", "TokenField");

            // The box is open for exactly as long as it is armed, so the class is authored rather
            // than bound — and it is what gives it the advisory border of a field waiting for a key.
            var entryBox = Descendants<Border>(view).Single(border => border.Classes.Contains("entryBox"));

            Assert.Contains("armed", entryBox.Classes);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryBrush", ThemeVariant.Dark), entryBox.BorderBrush);
        }

        /// <summary>
        /// The three field types with exactly one appearance in the app, bridged by type rather than
        /// by a class nobody would ever have to choose between.
        /// </summary>
        public static TheoryData<string, string, string> BridgedTypes()
        {
            return new TheoryData<string, string, string>
            {
                // The settings panel draws the slider rows of specs/08-settings.md §5 and the
                // "Disable" box beside them. The drop-down is NOT here: the Freestyle Edge RGB
                // this scene builds carries no choice row, and the troubleshoot panel's device
                // picker — which used to be the app's only other ComboBox — is a ListBox of
                // silhouettes since the §1d rebuild. TheSettingsChoiceRow_BridgesItsDropDown below
                // reaches the one that is left, on the board that actually has it.
                { typeof(KeyboardSettingsView).FullName!, nameof(Slider), "Slider" },
                { typeof(KeyboardSettingsView).FullName!, nameof(CheckBox), "CheckBox" },
                { typeof(ExportOverlayView).FullName!, nameof(CheckBox), "CheckBox" }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(BridgedTypes))]
        public async Task EveryBridgedType_ReachesItsThemeOnARealView(
            string viewTypeName,
            string typeName,
            string themeKey)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(viewTypeName);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var theme = Theme(themeKey);

            // Exact type, never a subclass: an Avalonia type selector matches the exact type, which
            // is what keeps `Slider` off the colour picker's own ColorSlider.
            var controls = Descendants<Control>(view)
                .Where(control => control.GetType().Name == typeName)
                .ToArray();

            Assert.NotEmpty(controls);
            Assert.All(controls, control => Assert.Same(theme, control.Theme));
        }

        [AvaloniaFact]
        public async Task TheSettingsChoiceRow_BridgesItsDropDown()
        {
            // The app's one remaining ComboBox: `led_mode` on a board whose capability spells it as
            // a mode string (specs/08-settings.md §5.3) — the Freestyle Edge. The shared settings
            // scene is the Freestyle Edge RGB, which has no choice row at all, so this builds the
            // board that does rather than asserting on a control that is never realised.
            using var scenes = new ViewSceneFactory();

            var editor = scenes.CreateEditorFor(DeviceId.FreestyleEdge);
            var view = new KeyboardSettingsView { DataContext = editor.Settings };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var comboBox = Assert.Single(Descendants<Control>(view), control => control.GetType() == typeof(ComboBox));

            Assert.Same(Theme("ComboBox"), comboBox.Theme);
        }

        [AvaloniaFact]
        public async Task TheDevicePicker_ThemesItsOwnRows()
        {
            // The empty state's "Which device do you have?" list (docs/design/mockups.md §1d). It
            // replaced a ComboBox: a drop-down hid six of the seven silhouettes behind a click.
            // Like the token picker, it names its container theme rather than relying on a blanket
            // ListBoxItem style — there is deliberately no such style any more.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(NoDeviceView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var list = Descendants<ListBox>(view).Single();
            var picker = (NoDeviceViewModel)view.DataContext!;

            Assert.Same(Theme("SelectableListRow"), list.ItemContainerTheme);
            Assert.Equal(picker.Devices.Count, list.ItemCount);

            foreach (var index in Enumerable.Range(0, picker.Devices.Count))
            {
                Assert.Same(Theme("SelectableListRow"), ContainerAt(list, index).Theme);
            }
        }

        [AvaloniaFact]
        public async Task ChoosingADeviceRow_MovesTheEmptyStatesWholeDescription()
        {
            // One pick drives the steps, the demo target and the support link; the list is the only
            // thing that moves it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(NoDeviceView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var list = Descendants<ListBox>(view).Single();
            var picker = (NoDeviceViewModel)view.DataContext!;
            var tkoIndex = picker.Devices.Select((option, index) => (option, index))
                .Single(entry => entry.option.Id == DeviceId.Tko).index;

            list.SelectedIndex = tkoIndex;

            Assert.Equal(DeviceId.Tko, picker.SelectedDevice.Id);
            Assert.Contains("SmartSet + Right Shift + V", picker.Steps.Select(step => step.ValueText));
            Assert.Equal("Launch TKO in Demo Mode", picker.DemoModeCaption);

            // ...and the other direction, which is what the two-way binding is for.
            picker.SelectedDevice = DeviceCatalog.GetById(DeviceId.Advantage2);

            Assert.Same(picker.SelectedOption, list.SelectedItem);
        }

        [AvaloniaFact]
        public async Task TheTokenPicker_ThemesItsOwnRows()
        {
            // The blanket `ListBoxItem` style that used to do this leaked onto every list in the
            // app, including the segmented control's — and, being an app style, outranked their
            // container themes. The picker names its rows instead.
            //
            // It names them ONE BY ONE now, on ListBoxItems laid out by an ItemsControl, rather
            // than through a ListBox.ItemContainerTheme: the result list is GROUPED, and a ListBox
            // per group would be one independent selection per group. So there is no ListBox here
            // at all, and the container theme is written on the row itself.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(TokenPickerView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var rows = Descendants<ListBoxItem>(view).ToArray();

            Assert.Empty(Descendants<ListBox>(view));
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Same(Theme("SelectableListRow"), row.Theme));
        }

        [AvaloniaFact]
        public async Task TheEditorsLayerSwitch_IsASegmentedControlWithSegmentedContainers()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var switcher = LayerSwitchOf(view);

            Assert.Same(Theme("SegmentedControl"), switcher.Theme);
            Assert.True(editor.Layers.Count >= 2, "The Freestyle Edge RGB scene rendered fewer than two layers.");

            foreach (var index in Enumerable.Range(0, editor.Layers.Count))
            {
                Assert.Same(Theme("SegmentedItem"), ContainerAt(switcher, index).Theme);
            }
        }

        [AvaloniaFact]
        public async Task TheEditorsSectionStrip_IsATabStripWithTabContainers()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var strip = Descendants<TabStrip>(view).Single();

            Assert.Same(Theme("TabStrip"), strip.Theme);
            Assert.Equal(editor.Tabs.Count, strip.ItemCount);

            foreach (var index in Enumerable.Range(0, editor.Tabs.Count))
            {
                var container = ContainerAt(strip, index);

                Assert.Same(Theme("TabStripItem"), container.Theme);

                // Every tab the strip carries works: a section this app cannot open for this board
                // is not rendered at all, so nothing in the strip is ever disabled and the scoped
                // TabStripItem style that used to bind IsEnabled is gone.
                Assert.True(container.IsEnabled, $"The {editor.Tabs[index].Caption} tab rendered disabled.");
            }
        }

        [AvaloniaFact]
        public async Task TheLightingSpeedBarsAndDirectionArrows_BridgeTheirThemes()
        {
            // Both rows are drawn only for a mode that HAS them, and both are generated by an
            // ItemsControl — so on a layer whose mode is Off there are no containers to assert on at
            // all. The mode is therefore picked first, through the very command the mode dropdown
            // runs, which is also what proves each row appears when the mode gains its parameter.
            //
            // Wave carries both: WritesSpeed and all four KeyBacklightDirections
            // (KinesisEdit.Core.Lighting.LightingModeCatalog).
            //
            // Direction used to sit in the always-drawn table above, because #86 drew all four
            // arrows for every mode and struck the unusable ones through. Issue #128 gated the block
            // on Parameters.AcceptsDirection, so it is now in exactly the same position as speed.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var lighting = (LightingTabViewModel)view.DataContext!;
            var wave = lighting.Modes.Single(mode => mode.Mode == LightingMode.Wave);

            lighting.SelectModeCommand.Execute(wave);

            host.Capture();

            AssertClassCarriesTheme(view, "speedBar", "SpeedBar");
            AssertClassCarriesTheme(view, "directionSegment", "DirectionSegment");
        }

        [AvaloniaFact]
        public async Task TheLightingColourSlots_BridgeTheirTheme()
        {
            // The colour block is gated on Parameters.AcceptsAnyColor (#128), so it needs a mode
            // that writes a colour. Monochrome writes an effect colour and nothing else — no speed,
            // no direction — which makes it the cleanest witness that the rail really does render
            // one mode's parameters rather than a fixed set.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var lighting = (LightingTabViewModel)view.DataContext!;
            var mono = lighting.Modes.Single(mode => mode.Mode == LightingMode.Monochrome);

            lighting.SelectModeCommand.Execute(mono);

            host.Capture();

            AssertClassCarriesTheme(view, "colorSlot", "SecondaryButton");
        }

        [AvaloniaFact]
        public async Task TheLightingTabsLayerSwitch_IsASegmentedControlAndKeepsItsFirmwareGate()
        {
            // The switch is the editor shell's since issue #128 — it sits in the 38px tab-bar row
            // beside the layout one, not on the lighting board — so the scene is the whole editor
            // with Lighting open, which is how a user reaches it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            editor.SelectedTab = EditorTab.Lighting;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var lighting = editor.Lighting;
            var switcher = LightingLayerSwitchOf(view);

            Assert.Same(Theme("SegmentedControl"), switcher.Theme);
            Assert.NotEmpty(lighting.Layers);

            foreach (var index in Enumerable.Range(0, lighting.Layers.Count))
            {
                var container = ContainerAt(switcher, index);

                Assert.Same(Theme("SegmentedItem"), container.Theme);

                // The Fn segment below the LED 1.0.44 gate stays visible and dimmed (§3).
                Assert.Equal(lighting.Layers[index].IsEnabled, container.IsEnabled);
            }
        }

        [AvaloniaFact]
        public async Task ChoosingALayerSegment_RunsTheSameCommandTheLayerPillsDid()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var switcher = LayerSwitchOf(view);

            switcher.SelectedIndex = 1;

            Assert.Same(editor.Layers[1], editor.SelectedLayer);
            Assert.True(editor.Layers[1].IsSelected, "The chosen layer's own IsSelected did not follow.");
            Assert.False(editor.Layers[0].IsSelected);
        }

        [AvaloniaFact]
        public async Task AnEditorDrivenLayerChange_MovesTheSegmentBack()
        {
            // The other direction, and the reason `SelectedItem` is bound rather than left to the
            // control: an import re-selects layer 0, and the switch has to follow it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var switcher = LayerSwitchOf(view);

            switcher.SelectedIndex = 1;

            editor.SelectLayerCommand.Execute(editor.Layers[0]);

            Assert.Same(editor.Layers[0], switcher.SelectedItem);
            Assert.Equal(0, switcher.SelectedIndex);
        }

        [AvaloniaFact]
        public async Task ChoosingATab_OpensThatSectionAndTheEditorMovesTheStripBack()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var strip = Descendants<TabStrip>(view).Single();

            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.Equal(0, strip.SelectedIndex);

            // The strip is positional, so the section it opens is looked up rather than written
            // out: issue #140 deleted the tab that used to sit at 1, and a renumbered literal would
            // have gone on passing against a different section.
            var lighting = editor.Tabs.Single(tab => tab.Tab == EditorTab.Lighting);

            strip.SelectedIndex = editor.Tabs.ToList().IndexOf(lighting);

            Assert.Equal(EditorTab.Lighting, editor.SelectedTab);
            Assert.True(lighting.IsSelected, "The chosen tab's own IsSelected did not follow.");

            editor.SelectTabCommand.Execute(editor.Tabs.Single(tab => tab.Tab == EditorTab.Keys));

            Assert.Equal(0, strip.SelectedIndex);
        }

        [AvaloniaFact]
        public async Task AGatedLightingLayerSegment_IsRefusedAndTheSegmentSnapsBack()
        {
            // The Fn layer below the LED 1.0.44 gate (§3): the panel refuses it, so the segment must
            // not be left showing a layer nothing switched to. A pointer cannot reach a disabled
            // segment at all — this is the programmatic path, and it is the one that could desync.
            //
            // It builds its own editor, because the shared scene's device now carries a version
            // file and therefore MEETS this gate: a scene that needs a refusal has to ask for one.
            //
            // The whole editor, not the lighting board: since issue #128 the switch is in the
            // tab-bar row, so the board alone no longer contains the control under test.
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateGatedEditorAsync();
            var lighting = editor.Lighting;
            var view = new KeyboardEditorView { DataContext = editor };

            editor.SelectedTab = EditorTab.Lighting;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var switcher = LightingLayerSwitchOf(view);

            Assert.True(lighting.Layers.Count >= 2, "The lighting scene rendered fewer than two layers.");
            Assert.False(lighting.Layers[1].IsEnabled, "The scene's Fn layer is not gated; it cannot show the refusal.");

            switcher.SelectedIndex = 1;

            Assert.Same(lighting.Layers[0], lighting.SelectedLayer);
            Assert.Same(lighting.Layers[0], switcher.SelectedItem);
        }

        /// <summary>
        /// The rail's drag seam, at the glass (issue #119): what it draws at rest, what it lifts to
        /// under the pointer, and the fact that the 6px hit target is not the 1px it paints.
        /// <para>
        /// The states are raised <b>after</b> the host has shown, never before: a pseudo-class set
        /// on a control that has not had its first layout pass is wiped by it.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRailSplitter_DrawsAHairlineAndLiftsItUnderThePointer(string variantName)
        {
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var splitter = Assert.Single(Descendants<GridSplitter>(view));

            // The hairline rides Foreground, not BorderBrush: the focus ring owns BorderBrush
            // (contract 3), so a hover ramp written on the same property would fight it.
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), splitter.Foreground);

            var line = Descendants<Border>(splitter).Single(border => border.Width == 1);

            Assert.Same(splitter.Foreground, line.Background);
            Assert.Equal(6, splitter.Bounds.Width);
            Assert.Equal(1, line.Bounds.Width);

            SetPseudoClasses(splitter, ":pointerover");

            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", variant), splitter.Foreground);

            // A seam being dragged stays lit: Avalonia raises :pointerover AND :pressed while the
            // pointer is down, and the two are spelled separately rather than left to file order.
            SetPseudoClasses(splitter, ":pressed");

            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", variant), splitter.Foreground);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRailSplitter_CarriesTheStandardFocusRing(string variantName)
        {
            // It is a real tab stop — the arrows move it by KeyboardIncrement — so it has to say
            // where it is, with the same 1px accent border plus 3px halo every other control uses.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var splitter = Assert.Single(Descendants<GridSplitter>(view));

            Assert.Null(splitter.FocusAdorner);
            Assert.True(splitter.Focus(NavigationMethod.Tab), "The seam refused keyboard focus.");

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), splitter.BorderBrush);

            var root = Descendants<Border>(splitter).First();

            Assert.Equal(1, root.BoxShadow.Count);
            Assert.False(root.ClipToBounds, "A clip anywhere on the seam erases the halo.");
        }

        private static void SetPseudoClasses(StyledElement element, params string[] pseudoClasses)
        {
            var classes = (IPseudoClasses)element.Classes;

            foreach (var pseudoClass in pseudoClasses)
            {
                classes.Set(pseudoClass, true);
            }
        }

        [AvaloniaFact]
        public void NoMarkupOutsideTheControlThemeLayer_NamesATemplatePart()
        {
            // The issue's headline rule. A `/template/` selector is only legitimate inside the
            // ControlTheme that declares the part, and `PART_` is Fluent's naming — reaching for one
            // means reaching into a template we do not own, which breaks the moment Fluent moves it.
            // Comments count: the convention has to stop being described as current, too.
            //
            // The scan is over every embedded .axaml, and the project embeds them by glob
            // (KinesisEdit.Tests.csproj), so a view added to a folder that does not exist yet is
            // covered the day it is written — there is no list here to forget to extend.
            var offenders = new List<string>();

            foreach (var (path, xaml) in AuthoredXaml.Files())
            {
                if (IsControlThemeLayer(path))
                {
                    continue;
                }

                if (xaml.Contains("/template/", StringComparison.Ordinal))
                {
                    offenders.Add($"{path}: /template/");
                }

                if (xaml.Contains("PART_", StringComparison.Ordinal))
                {
                    offenders.Add($"{path}: PART_");
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));

            AssertTheLayerWasScanned();
        }

        [AvaloniaFact]
        public void NoAuthoredMarkupAnywhere_NamesAPartWithFluentsPrefix()
        {
            // The half the exemption above cannot carry. `/template/` is allowed inside
            // Themes/ControlThemes/ — targeting a part your own ControlTheme declares is idiomatic
            // — but `PART_` is banned there too: contract 2 says "name parts `Root`, `Halo`,
            // `Underline`, ... never with a `PART_` prefix", because that prefix is what re-registers
            // a presenter with its ContentControl and puts the template back on the hook Styles/
            // used to reach into. Skipping those files wholesale left the ban unenforced in the one
            // directory that writes templates.
            //
            // Comments are stripped for this scan and only for this scan: Shared.axaml's contract
            // and Fields.axaml's BasedOn table both quote the very name they forbid, and a raw
            // search finds the explanation as readily as an offence.
            var offenders = AuthoredXaml.Files()
                .Where(file => AuthoredXaml.WithoutComments(file.Value).Contains("PART_", StringComparison.Ordinal))
                .Select(file => file.Key)
                .ToArray();

            Assert.True(offenders.Length == 0, $"These name a PART_: {string.Join(", ", offenders)}.");

            AssertTheLayerWasScanned();
        }

        [AvaloniaFact]
        public void NoControlThemeInTheLayer_HardcodesAColour()
        {
            // Contract 7: "A hex literal outside Themes/Tokens.axaml is a bug and fails a test."
            // ResourceReferenceTests.NoView_HardcodesAHexColour skips everything under `Themes/`,
            // because that is where the token definitions live — which leaves this whole directory
            // outside it, and a control theme is where the temptation is strongest: an inset
            // selection ring and a focus halo both read naturally as literals.
            //
            // Over the directory rather than over a list of the eight files it currently holds, so
            // the ninth is covered the day it is written.
            var offenders = new List<string>();

            foreach (var (path, xaml) in AuthoredXaml.Files().Where(file => IsControlThemeLayer(file.Key)))
            {
                foreach (Match literal in HexLiteralPattern().Matches(AuthoredXaml.WithoutComments(xaml)))
                {
                    offenders.Add($"{path}: {literal.Value}");
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));

            AssertTheLayerWasScanned();
        }

        /// <summary>A colour literal in an attribute value: <c>Foo="#RRGGBB"</c>.</summary>
        [GeneratedRegex("=\"#[0-9A-Fa-f]{3,8}\"")]
        private static partial Regex HexLiteralPattern();

        /// <summary>
        /// The scans above are only worth anything if the layer's own files reached the test
        /// assembly: an exemption keyed on a directory that no longer exists, or a glob that stopped
        /// matching, would leave either guard passing over nothing.
        /// </summary>
        private static void AssertTheLayerWasScanned()
        {
            var scanned = AuthoredXaml.Files()
                .Keys
                .Where(IsControlThemeLayer)
                .ToArray();

            Assert.Contains($"{ControlThemeDirectory}Shared.axaml", scanned);
            Assert.True(scanned.Length >= 8, $"Only {scanned.Length} control-theme files were scanned.");
        }

        private static bool IsControlThemeLayer(string path)
        {
            return path.StartsWith(ControlThemeDirectory, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void NoViewStillWrites_AClassTheControlThemeLayerReplaced()
        {
            // `layerTab` and `editorTab` were the pre-redesign spellings of the segmented control and
            // the tab strip. Both classes are gone from Styles/, so a view left holding one would
            // render an unstyled Fluent button and nothing would fail.
            var offenders = new List<string>();

            foreach (var (path, xaml) in AuthoredXaml.Files())
            {
                var markup = AuthoredXaml.WithoutComments(xaml);

                foreach (var className in new[] { "layerTab", "editorTab" })
                {
                    if (markup.Contains(className, StringComparison.Ordinal))
                    {
                        offenders.Add($"{path}: {className}");
                    }
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        private static void AssertClassCarriesTheme(Control view, string className, string themeKey)
        {
            var theme = Theme(themeKey);
            var targetType = theme.TargetType
                ?? throw new InvalidOperationException($"'{themeKey}' declares no TargetType.");

            var carriers = view.GetVisualDescendants()
                .OfType<StyledElement>()
                .Where(element => element.Classes.Contains(className) && targetType.IsInstanceOfType(element))
                .ToArray();

            Assert.True(carriers.Length > 0, $"No {targetType.Name}.{className} was rendered at all.");
            Assert.All(carriers, element => Assert.Same(theme, element.Theme));
        }

        private static ControlTheme Theme(string key)
        {
            return (ControlTheme)DesignTokens.Resolve(key, ThemeVariant.Dark);
        }

        /// <summary>The editor's layer switch: the one list over <see cref="KeyboardLayerViewModel"/>.</summary>
        private static ListBox LayerSwitchOf(Control view)
        {
            return Descendants<ListBox>(view).Single(list => list.ItemsSource is IEnumerable<KeyboardLayerViewModel>);
        }

        /// <inheritdoc cref="LayerSwitchOf" />
        private static ListBox LightingLayerSwitchOf(Control view)
        {
            return Descendants<ListBox>(view).Single(list => list.ItemsSource is IEnumerable<LightingLayerViewModel>);
        }

        private static IEnumerable<T> Descendants<T>(Control view) where T : Visual
        {
            return view.GetVisualDescendants().OfType<T>();
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private static Control ContainerAt(ItemsControl items, int index)
        {
            return items.ContainerFromIndex(index)
                ?? throw new InvalidOperationException($"No container was generated for item {index}.");
        }
    }
}
