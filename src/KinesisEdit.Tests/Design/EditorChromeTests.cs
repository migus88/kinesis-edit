using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
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
        public async Task TheDemoModeBar_CarriesItsVerbatimCopyAndOnlyTheConnectAction()
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

            // ONE action, not the mockup's two. `Export layout to file…` is gone: demo mode opens
            // no session, so CanExport is false for exactly as long as this bar is on screen, and
            // "a feature the device lacks is not shown, not disabled" is the design law. Every
            // other CanExecute-disabled control in this editor can become live; that one could not.
            var action = Assert.Single(actions);

            Assert.Equal(KeyboardEditorViewModel.DemoModeConnectCaption, action.Content);
            Assert.Same(editor.Shell!.HomeCommand, action.Command);

            Assert.DoesNotContain(
                bar.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text is not null && block.Text.StartsWith("Export", StringComparison.Ordinal));

            // And it really is unavailable, which is why it is not drawn.
            Assert.False(editor.ExportCommand.CanExecute(null));
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
            Assert.Equal(
                editor.Layers.Select(layer => layer.ShortcutHint),
                chips.Select(chip => chip.Content as string));
        }

        [AvaloniaFact]
        public async Task TheProvisionalActionRow_ShrinksAsEachActionReachesItsRealHome()
        {
            // Issue #91 gave `Reset Layer` its home under the board, beside the counts it acts on,
            // and it took the mockups' sentence case with it (`Reset layer`). Issue #92 took two
            // more into the key inspector rail: `Reset Key` is its footer's `Revert key`, and
            // `Tap and Hold` is a rail panel rather than a modal. What is left is #93's — the
            // editor-level action bar.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var provisional = Assert.Single(view.GetVisualDescendants().OfType<WrapPanel>());
            var captions = provisional.Children.OfType<Button>().Select(button => button.Content as string).ToArray();

            Assert.Equal(
                new[] { "Reset Layout", "Insert Delay", "Special Action", "Export", "Import" },
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
    }
}
