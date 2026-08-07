using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
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
                (typeof(WebToolCardView).FullName!, "secondary", "SecondaryButton"),
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
                (typeof(AdvisoryStripView).FullName!, "secondary", "SecondaryButton"),
                // The Settings tab. `settingSwitch` is the 27th control theme and the only bridge
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

                (typeof(LightingTabView).FullName!, "modeOption", "ModeOption"),
                (typeof(LightingTabView).FullName!, "colorSlot", "SecondaryButton"),
                (typeof(MacroDelayOverlayView).FullName!, "monoValue", "MonoValueField"),

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
                (typeof(KeyInspectorView).FullName!, "secondary", "SecondaryButton"),
                (typeof(KeyInspectorView).FullName!, "ghost", "GhostButton"),
                (typeof(LockedKeyPanelView).FullName!, "secondary", "SecondaryButton")
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
        public async Task TheMacroPanel_BridgesItsSlotPillsAndItsCoTriggerToggles()
        {
            // The panel is the Macros tab's, hosted in a ContentControl's template, so none of it
            // exists until that section is open.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var editor = (KeyboardEditorViewModel)view.DataContext!;

            editor.SelectedTab = EditorTab.Macros;

            host.Capture();

            AssertClassCarriesTheme(view, "navPill", "NavPill");
            AssertClassCarriesTheme(view, "toggleSegment", "ToggleSegment");
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
        public async Task TheLightingTabsLayerSwitch_IsASegmentedControlAndKeepsItsFirmwareGate()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var lighting = (LightingTabViewModel)view.DataContext!;
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

            strip.SelectedIndex = 1;

            Assert.Equal(EditorTab.Macros, editor.SelectedTab);
            Assert.True(editor.Tabs[1].IsSelected, "The chosen tab's own IsSelected did not follow.");

            editor.SelectTabCommand.Execute(editor.Tabs[0]);

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
            using var scenes = new ViewSceneFactory();

            var lighting = await scenes.CreateGatedLightingAsync();
            var view = new LightingTabView { DataContext = lighting };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var switcher = LightingLayerSwitchOf(view);

            Assert.True(lighting.Layers.Count >= 2, "The lighting scene rendered fewer than two layers.");
            Assert.False(lighting.Layers[1].IsEnabled, "The scene's Fn layer is not gated; it cannot show the refusal.");

            switcher.SelectedIndex = 1;

            Assert.Same(lighting.Layers[0], lighting.SelectedLayer);
            Assert.Same(lighting.Layers[0], switcher.SelectedItem);
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
            // Over the directory rather than over a list of the seven files it currently holds, so
            // the eighth is covered the day it is written.
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
            Assert.True(scanned.Length >= 7, $"Only {scanned.Length} control-theme files were scanned.");
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

        private static Control ContainerAt(ItemsControl items, int index)
        {
            return items.ContainerFromIndex(index)
                ?? throw new InvalidOperationException($"No container was generated for item {index}.");
        }
    }
}
