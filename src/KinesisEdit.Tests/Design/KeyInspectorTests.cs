using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The key inspector rail and the locked-key panel at the glass, in both theme variants: the
    /// header and its mono token, the exclusivity sentence, the 2×2 mode grid on the real
    /// <c>TabStripItem</c> containers, the footer's two actions bound to the editor's own commands,
    /// and the locked panel's dead tabs with their reasons.
    /// <para>
    /// The view models are built by hand rather than pulled off a loaded editor: the rail is
    /// deliberately decoupled from <c>KeyboardEditorViewModel</c>, and a test that could only reach
    /// it through the editor would quietly be asserting the opposite.
    /// </para>
    /// </summary>
    public class KeyInspectorTests
    {
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheRail_NamesThePositionAndSaysWhatItDoesNow(string variantName)
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(KeyboardKeyViewModel.LeftHalfDescription, texts);
            Assert.Contains("[1]", texts);
            Assert.Contains(KeyInspectorViewModel.PositionSuffix, texts);
            Assert.Contains(KeyInspectorViewModel.FactoryLabel, texts);
            Assert.Contains(KeyInspectorViewModel.CurrentLabel, texts);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheExclusivitySentence_IsOnScreenVerbatim(string variantName)
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            Assert.Contains(KeyInspectorViewModel.ExclusivitySentence, VisibleTextsOf(view));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheModeTabs_AreRealTabStripItemsInATwoColumnGrid(string variantName)
        {
            // The 2×2 grid of docs/design/handoff.md:137, drawn on the section strip's own control
            // themes rather than on a hand-rolled row of buttons.
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var strip = Assert.Single(view.GetVisualDescendants().OfType<TabStrip>());
            var items = strip.GetVisualDescendants().OfType<TabStripItem>().ToArray();

            Assert.Equal(3, items.Length);
            Assert.Equal(2, Assert.Single(strip.GetVisualDescendants().OfType<UniformGrid>()).Columns);

            // Two rows, so the third tab really is below the first.
            var first = ((Visual)items[0]).TranslatePoint(default, host.Window)!.Value;
            var third = ((Visual)items[2]).TranslatePoint(default, host.Window)!.Value;

            Assert.True(third.Y > first.Y, "The mode tabs are on one row; the design lays them out 2\u00D72.");
        }

        [AvaloniaFact]
        public void MultiMod_IsNotDrawnAtAllOnThisBoard()
        {
            // handoff.md:137 — where the firmware lacks it, the tab is absent, not dim. Mockup 1e
            // draws it beside an "Advantage 360 only" note; the handoff wins.
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.DoesNotContain(KeyInspectorTabViewModel.MultiModifierCaption, VisibleTextsOf(view));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheFooter_CarriesRevertKeyAndCopyTo_BoundToTheEditorsOwnCommands(string variantName)
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var buttons = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible)
                .ToArray();

            var revert = Assert.Single(buttons, button => Equals(button.Content, KeyInspectorViewModel.RevertKeyCaption));
            var copy = Assert.Single(buttons, button => Equals(button.Content, KeyInspectorViewModel.CopyToCaption));

            Assert.Same(scene.ResetKeyCommand, revert.Command);
            Assert.Same(scene.CopyKeyCommand, copy.Command);
        }

        [AvaloniaFact]
        public void TheAssignmentLineIsMono_AndTheProseAroundItIsNot()
        {
            // "Mono means this is literally a value in a config file" — `[1]` is, "position" is not.
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var blocks = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .ToArray();

            Assert.All(
                blocks.Where(block => block.Text == "[1]"),
                block => Assert.Contains("Mono", block.FontFamily.Name, StringComparison.Ordinal));

            var suffix = Assert.Single(blocks, block => block.Text == KeyInspectorViewModel.PositionSuffix);

            Assert.DoesNotContain("Mono", suffix.FontFamily.Name, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void TheModeSwitchWarning_IsAmberAndAppearsOnlyWhenSomethingWouldBeReplaced()
        {
            // Amber is the only warning colour the app has, and the warning never blocks.
            var scene = new Scene();

            scene.Remap();

            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.DoesNotContain(
                KeyInspectorViewModel.BuildModeSwitchWarning(KeyInspectorMode.Remap),
                VisibleTextsOf(view));

            scene.Inspector.SelectModeCommand.Execute(
                scene.Inspector.Tabs.Single(tab => tab.Mode == KeyInspectorMode.TapAndHold));

            host.Capture();

            var warning = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == KeyInspectorViewModel.BuildModeSwitchWarning(KeyInspectorMode.Remap));

            Assert.True(warning.IsEffectivelyVisible);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTextBrush", ThemeVariant.Dark), warning.Foreground);
            Assert.NotEqual(DesignTokens.Resolve("StatusErrorBrush", ThemeVariant.Dark), warning.Foreground);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void WithNothingSelected_TheRailStaysOnScreenAndSaysWhyItIsEmpty(string variantName)
        {
            // Issue #119 inverts what this used to assert. The rail collapsed itself when nothing
            // was selected, so its Auto column measured zero and the board jumped sideways to
            // re-centre — the very defect the issue is about. It is a permanent column now.
            var scene = new Scene();
            var view = scene.CreateView();

            scene.Inspector.Refresh(null, scene.Layer, scene.Layout, EditorAdvisories.Empty);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var frame = Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("inspectorRail"));

            Assert.True(frame.IsEffectivelyVisible);
            Assert.True(frame.Bounds.Width > 0);

            // One sentence, and nothing the rail says about a position it does not have.
            var texts = VisibleTextsOf(view);

            Assert.Contains(KeyInspectorViewModel.NoSelectionMessage, texts);
            Assert.DoesNotContain(KeyInspectorViewModel.PositionSuffix, texts);
            Assert.DoesNotContain(KeyInspectorViewModel.ExclusivitySentence, texts);
            Assert.DoesNotContain(KeyInspectorViewModel.RevertKeyCaption, texts);
            Assert.DoesNotContain(KeyInspectorViewModel.CopyToCaption, texts);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TabStripItem>(), item => item.IsEffectivelyVisible);
        }

        [AvaloniaFact]
        public void TheRail_HasNoCloseButtonAnyMore()
        {
            // The header's ghost close mark went with issue #119: a permanent column has nothing to
            // dismiss, and the button was one of the two ways the board could be made to jump.
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("ghost"));
            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, scene.Inspector.CloseCommand));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheRail_RestsOnTheInsetSurfaceAndCarriesNoWidthOfItsOwn(string variantName)
        {
            // The 268 this used to assert lives on the grid COLUMN now (issue #119): the rail is
            // drag-adjustable, a GridSplitter resizes columns, and a Width here would have outranked
            // whatever the column said. EditorChromeTests measures the column on the real editor;
            // what is left to this file is the surface and the hairline.
            var variant = ToVariant(variantName);
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var frame = Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("inspectorRail"));

            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), frame.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), frame.BorderBrush);
            Assert.Equal(new Thickness(1, 0, 0, 0), frame.BorderThickness);
            Assert.True(double.IsNaN(frame.Width), "The rail still carries a Width; the column owns it now.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheExclusivitySentence_IsAHintBlockAndDeliberatelyNotAmber(string variantName)
        {
            // Issue #119. The sentence read as stray prose between the mode tabs and the amber
            // mode-switch warning; framed, the two stop reading as one block. Neutral, because amber
            // is the advisory colour in the four-status vocabulary and this sentence warns of
            // nothing.
            var variant = ToVariant(variantName);
            var scene = new Scene();

            scene.Remap();

            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, variant);

            // Put the rail on a mode the position is not carrying, so the amber warning is drawn
            // directly beneath the hint and the two can actually be compared.
            scene.Inspector.SelectModeCommand.Execute(
                scene.Inspector.Tabs.Single(tab => tab.Mode == KeyInspectorMode.TapAndHold));

            host.Capture();

            var hint = Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("hintBlock"));

            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), hint.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), hint.BorderBrush);
            Assert.Equal(new Thickness(1), hint.BorderThickness);
            Assert.Equal(DesignTokens.Resolve("RadiusControl", variant), hint.CornerRadius);

            // Not amber, and the amber block below it still is — so the two are visibly different
            // things rather than one undifferentiated column of prose.
            Assert.NotEqual(DesignTokens.Resolve("StatusAdvisoryTintBrush", variant), hint.Background);
            Assert.NotEqual(DesignTokens.Resolve("StatusAdvisoryTintBorderBrush", variant), hint.BorderBrush);

            var warning = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == KeyInspectorViewModel.BuildModeSwitchWarning(KeyInspectorMode.Remap));

            Assert.True(warning.IsEffectivelyVisible);

            // The sentence itself is inside the block, not beside it.
            Assert.Contains(
                hint.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == KeyInspectorViewModel.ExclusivitySentence);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheLockedPanel_DrawsThreeDeadTabsEachWithItsReason(string variantName)
        {
            // The design's own exception to "absent features are not shown, not disabled" — and the
            // reason beside each one is what makes it legal.
            var panel = new LockedKeyPanelViewModel(new RelayCommand(() => { }));

            panel.Refresh(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            var view = new LockedKeyPanelView { DataContext = panel };

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var items = view.GetVisualDescendants().OfType<TabStripItem>().ToArray();

            Assert.Equal(3, items.Length);
            Assert.All(items, item => Assert.False(item.IsEnabled));

            // Mockup 2h draws the mode on the left and its reason on the right, so both halves are
            // on screen — a truncated reason is the same as no reason, which is what would make a
            // dead tab illegal.
            var texts = VisibleTextsOf(view);

            Assert.Contains(KeyInspectorTabViewModel.RemapCaption, texts);
            Assert.Contains(KeyInspectorTabViewModel.TapAndHoldCaption, texts);
            Assert.Contains(KeyInspectorTabViewModel.MacroCaption, texts);
            Assert.Equal(3, texts.Count(text => text == KeyInspectorTabViewModel.NotWritableReason));

            var reasons = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.Text == KeyInspectorTabViewModel.NotWritableReason)
                .ToArray();

            Assert.All(reasons, block => Assert.Equal(TextTrimming.None, block.TextTrimming));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheLockedPanel_ExplainsItselfAndSaysWhatTheKeyDoesOnTheBoard(string variantName)
        {
            var panel = new LockedKeyPanelViewModel(new RelayCommand(() => { }));

            panel.Refresh(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            var view = new LockedKeyPanelView { DataContext = panel };

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(LockedKeyPanelViewModel.StateLine, texts);
            Assert.Contains(LockedKeyPanelViewModel.Explanation, texts);
            Assert.Contains(LockedKeyPanelViewModel.HotkeySectionTitle, texts);
            Assert.Contains("hold + F8 mount the v-Drive", texts);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheLockedPanel_RefusesOneCopyDirectionAndNotTheWholeAction(string variantName)
        {
            var copy = new RelayCommand(() => { });
            var panel = new LockedKeyPanelViewModel(copy);

            panel.Refresh(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            var view = new LockedKeyPanelView { DataContext = panel };

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();

            var from = Assert.Single(buttons, button => Equals(button.Content, LockedKeyPanelViewModel.CopyFromCaption));
            var to = Assert.Single(buttons, button => Equals(button.Content, LockedKeyPanelViewModel.CopyToCaption));

            Assert.False(from.IsEnabled);
            Assert.True(to.IsEnabled);
            Assert.Same(copy, to.Command);
            Assert.Contains(LockedKeyPanelViewModel.CopyDirectionReason, VisibleTextsOf(view));
        }

        [AvaloniaFact]
        public void ALockedPosition_ReplacesTheRailsModesWithTheLockedPanel()
        {
            var scene = Scene.Locked();
            var view = scene.CreateView();

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.Single(
                view.GetVisualDescendants().OfType<LockedKeyPanelView>(),
                panel => panel.IsEffectivelyVisible);
            Assert.DoesNotContain(KeyInspectorViewModel.ExclusivitySentence, VisibleTextsOf(view));
            Assert.DoesNotContain(KeyInspectorViewModel.RevertKeyCaption, VisibleTextsOf(view));
        }

        private static IReadOnlyList<string> VisibleTextsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty)
                .ToArray();
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>A rail over a real board, built without an editor anywhere near it.</summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public KeyboardLayerViewModel Layer { get; }

            public KeyInspectorViewModel Inspector { get; }

            public IRelayCommand ResetKeyCommand { get; }

            public IRelayCommand CopyKeyCommand { get; }

            private readonly int _keyIndex;

            public Scene()
                : this(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb), VisualCatalog.FreestyleEdgeRgb, TestLayouts.RgbDigitOneKeyIndex)
            {
            }

            private Scene(KeyboardLayout layout, KeyboardVisual visual, int keyIndex)
            {
                Layout = layout;
                Layer = KeyboardLayerViewModel.BuildAll(layout, visual, lighting: null)[0];

                ResetKeyCommand = new RelayCommand(() => { });
                CopyKeyCommand = new RelayCommand(() => { });

                Inspector = new KeyInspectorViewModel(ResetKeyCommand, CopyKeyCommand, new RelayCommand(() => { }, () => false));

                _keyIndex = keyIndex;
            }

            public static Scene Locked()
            {
                return new Scene(TestLayouts.CreateLockedKeyLayout(), TestLayouts.CreateVisual(0, 1, 2), 1);
            }

            /// <summary>Remaps the selected position, so the rail has something to warn about.</summary>
            public void Remap()
            {
                var key = Layer.FindByIndex(_keyIndex)!;

                key.Key.Remap(TestLayouts.Gen1Key("esc"));
                key.RefreshFromModel();
            }

            public KeyInspectorView CreateView()
            {
                Inspector.Refresh(Layer.FindByIndex(_keyIndex), Layer, Layout, EditorAdvisories.Empty);

                return new KeyInspectorView { DataContext = Inspector };
            }
        }
    }
}
