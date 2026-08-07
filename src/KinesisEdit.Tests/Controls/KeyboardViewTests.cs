using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Design;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Controls
{
    /// <summary>
    /// The keyboard picture as a <b>composition</b>: the board it draws — one bordered panel per
    /// section with the design's gutter between them, grown as a single picture — and what it
    /// decides on behalf of the caps it builds.
    /// <para>
    /// Two of those decisions exist, and both are here rather than on the layer view model because
    /// the editor's Keys tab and the Lighting tab render the <i>same</i>
    /// <see cref="KeyboardLayerViewModel"/> instance (<c>KeyboardEditorViewModel.Apply</c> hands its
    /// <c>Layers</c> straight to <c>LightingTabViewModel.Attach</c>). No state on the layer or on a
    /// cap could ever separate the two pictures.
    /// </para>
    /// </summary>
    public class KeyboardViewTests
    {
        private const double HostWidth = 900;

        private const double HostHeight = 400;

        /// <summary>The "s" cap of the left half — a cap that is certainly in section 0.</summary>
        private const int LeftHalfKey = 55;

        [AvaloniaFact]
        public void ASplitBoard_IsDrawnAsOnePanelPerSection()
        {
            // Mockup 1e: the hotkey column and the left typing half in one bordered box, the right
            // half in another. The roster is the board's, so a one-piece device is one panel and
            // the same markup.
            var layer = CreateLayer();
            var board = new KeyboardView { DataContext = layer };

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var panels = PanelsOf(board);

            Assert.Equal(2, layer.Sections.Count);
            Assert.Equal(layer.Sections.Count, panels.Count);

            for (var index = 0; index < panels.Count; index++)
            {
                var section = layer.Sections[index];

                Assert.Equal(section.BoardWidth, panels[index].BoardWidth);
                Assert.Equal(section.BoardHeight, panels[index].BoardHeight);
                Assert.Equal(section.OriginX, panels[index].UnitOriginX);
                Assert.Equal(section.OriginY, panels[index].UnitOriginY);
            }
        }

        [AvaloniaFact]
        public void EverySectionPanel_IsABorderedBoxOnTheDesignsOwnSurface()
        {
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var sections = SectionBoxesOf(board);

            Assert.Equal(2, sections.Count);
            Assert.All(sections, section =>
            {
                Assert.Equal(new Thickness(1), section.BorderThickness);
                Assert.Equal(DesignTokens.Resolve("PaddingBoardSection", ThemeVariant.Dark), section.Padding);
                Assert.Equal(
                    DesignTokens.ResolveBrushColor("SurfacePanelBrush", ThemeVariant.Dark),
                    ColorOf(section.Background));
                Assert.Equal(
                    DesignTokens.ResolveBrushColor("SurfaceLineBrush", ThemeVariant.Dark),
                    ColorOf(section.BorderBrush));
            });
        }

        [AvaloniaFact]
        public void TheTwoPanels_AreSeparatedByTheSplitGutter()
        {
            // The authored 1U split gap stays in Core so an arrow can cross it (KeyAdjacency); the
            // picture replaces it with the design's 26px gutter between two boxes.
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var sections = SectionBoxesOf(board);
            var stack = sections[0].GetVisualAncestors().OfType<StackPanel>().First();

            Assert.Equal(Orientation.Horizontal, stack.Orientation);
            Assert.Equal((double)DesignTokens.Resolve("GutterSplit", ThemeVariant.Dark), stack.Spacing);

            var left = sections[0].TranslatePoint(default, stack)!.Value;
            var right = sections[1].TranslatePoint(default, stack)!.Value;

            Assert.Equal(
                (double)DesignTokens.Resolve("GutterSplit", ThemeVariant.Dark),
                right.X - (left.X + sections[0].Bounds.Width),
                6);
        }

        [AvaloniaFact]
        public void EveryPanel_IsGivenTheCellPitchAndGapTokens()
        {
            // The link that makes Themes/Geometry.axaml the single source of the board's scale: a
            // panel drawing on its own C# fallbacks would ignore a token change outright.
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var pitchX = (double)DesignTokens.Resolve("KeycapPitchX", ThemeVariant.Dark);
            var pitchY = (double)DesignTokens.Resolve("KeycapPitchY", ThemeVariant.Dark);
            var gap = (double)DesignTokens.Resolve("KeycapGap", ThemeVariant.Dark);

            Assert.All(PanelsOf(board), panel =>
            {
                Assert.Equal(pitchX, panel.PitchX);
                Assert.Equal(pitchY, panel.PitchY);
                Assert.Equal(gap, panel.Gap);
            });
        }

        [AvaloniaFact]
        public void ASectionsCaps_AreLaidOutRelativeToThatSectionsOwnOrigin()
        {
            // The right half's keys carry board-absolute units in the tens; inside its own panel the
            // left-most of them starts at zero, because the panel subtracts the section origin.
            var layer = CreateLayer();
            var board = new KeyboardView { DataContext = layer };

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var rightSection = layer.Sections[1];
            var leftMostUnit = rightSection.Keys.Min(key => key.X);

            Assert.True(leftMostUnit > 0, "The right half is not board-absolute; the fixture is wrong.");

            var caps = CapsOf(board)
                .Where(cap => cap.DataContext is KeyboardKeyViewModel key
                    && key.Section == rightSection.Index
                    && Math.Abs(key.X - leftMostUnit) < 0.001)
                .ToArray();

            Assert.NotEmpty(caps);
            Assert.All(caps, cap => Assert.Equal(0, cap.Bounds.X, 6));
        }

        [AvaloniaFact]
        public void ThePicture_GrowsAsOneThroughASingleScaleHost()
        {
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var scaleHost = Assert.Single(board.GetVisualDescendants().OfType<BoardScaleHost>());

            Assert.True(scaleHost.Scale > 0, "The board was never scaled into the space it was given.");
        }

        [AvaloniaFact]
        public void NothingBetweenACapAndThePicture_Clips()
        {
            // The single most-repeated trap in this control: a focused cap casts a 3px halo past its
            // own bounds into a grid whose caps are 4px apart, and a clip anywhere on the chain
            // erases it.
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var cap = CapsOf(board)[0];

            foreach (var ancestor in cap.GetSelfAndVisualAncestors().OfType<Visual>())
            {
                Assert.False(
                    ancestor.ClipToBounds,
                    $"{ancestor.GetType().Name} clips, which erases the focused cap's halo.");

                if (ReferenceEquals(ancestor, board))
                {
                    break;
                }
            }
        }

        [AvaloniaFact]
        public void APicture_DrawsNoLedStripsUntilItAsksForThem()
        {
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.False(board.ShowsLedStrips, "The LED row is opt-in; the Keys tab never asks.");
            Assert.All(CapsOf(board), cap => Assert.False(cap.ShowsLedStrip));
        }

        [AvaloniaFact]
        public void APictureThatAsksForLedStrips_HandsThatDownToEveryCapItBuilds()
        {
            var board = CreateBoard();

            board.ShowsLedStrips = true;

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var caps = CapsOf(board);

            Assert.NotEmpty(caps);
            Assert.All(caps, cap => Assert.True(cap.ShowsLedStrip));
        }

        [AvaloniaFact]
        public void APicture_DrawsTheStateBadgesUnlessItSaysOtherwise()
        {
            // The mirror image of the LED row: the Layout tab is the surface the badge vocabulary
            // belongs to, so a board opts out rather than in.
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var caps = CapsOf(board);

            Assert.True(board.ShowsStateBadges);
            Assert.NotEmpty(caps);
            Assert.All(caps, cap => Assert.True(cap.ShowsStateBadge));
        }

        [AvaloniaFact]
        public void APictureThatWantsNoStateBadges_HandsThatDownToEveryCapItBuilds()
        {
            var board = CreateBoard();

            board.ShowsStateBadges = false;

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var caps = CapsOf(board);

            Assert.NotEmpty(caps);
            Assert.All(caps, cap => Assert.False(cap.ShowsStateBadge));
        }

        [AvaloniaFact]
        public void TwoPicturesOfTheSameLayer_DisagreeAboutTheStateBadges()
        {
            // Same argument as the LED row, and the same fixture: one layer view model, one set of
            // cap view models, two pictures. A lighting board is a readout of colour and says
            // nothing about what a key is assigned.
            var layer = CreateLayer();
            var keys = new KeyboardView { DataContext = layer };
            var lighting = new KeyboardView { DataContext = layer, ShowsStateBadges = false };
            var panel = new StackPanel { Children = { keys, lighting } };

            using var host = ThemedHost.Show(panel, ThemeVariant.Dark, HostWidth, HostHeight);

            var keysCap = CapsOf(keys)[0];
            var lightingCap = CapsOf(lighting)[0];

            Assert.Same(keysCap.DataContext, lightingCap.DataContext);
            Assert.True(keysCap.ShowsStateBadge);
            Assert.False(lightingCap.ShowsStateBadge);
        }

        [AvaloniaFact]
        public void TwoPicturesOfTheSameLayer_DisagreeAboutTheLedRow()
        {
            // The whole reason the flag is on the picture. These are the editor's two tabs: one
            // layer view model, one set of cap view models, two pictures — and the LED row is one of
            // the two things they must not agree about.
            var layer = CreateLayer();
            var keys = new KeyboardView { DataContext = layer };
            var lighting = new KeyboardView { DataContext = layer, ShowsLedStrips = true };
            var panel = new StackPanel { Children = { keys, lighting } };

            using var host = ThemedHost.Show(panel, ThemeVariant.Dark, HostWidth, HostHeight);

            var keysCap = CapsOf(keys)[0];
            var lightingCap = CapsOf(lighting)[0];

            Assert.Same(keysCap.DataContext, lightingCap.DataContext);
            Assert.False(keysCap.ShowsLedStrip);
            Assert.True(lightingCap.ShowsLedStrip);
        }

        [AvaloniaFact]
        public void TryFocusKey_StillReachesACapThroughTheSectionPanels()
        {
            // The extra nesting the sections introduced is exactly what a visual-descendant walk
            // could have been broken by.
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            host.Capture();

            Assert.True(board.TryFocusKey(LeftHalfKey), "The picture could not focus a cap it drew.");

            var focused = Assert.IsAssignableFrom<Control>(TopLevel.GetTopLevel(board)!.FocusManager!.GetFocusedElement());
            var cap = Assert.Single(focused.GetSelfAndVisualAncestors().OfType<KeyCapView>());
            var key = Assert.IsType<KeyboardKeyViewModel>(cap.DataContext);

            Assert.Equal(LeftHalfKey, key.Index);
        }

        [AvaloniaFact]
        public void TryFocusKey_WithNoSuchCap_SaysSoRatherThanThrowing()
        {
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            host.Capture();

            Assert.False(board.TryFocusKey(int.MaxValue));
        }

        /// <summary>
        /// A picture of one layer, wired the way the editor wires it. The command is not decoration:
        /// a cap is a <see cref="Button"/>, and Avalonia leaves a button whose <c>Command</c> is
        /// null <b>disabled</b> — so a board built without one has 95 dead caps and could never take
        /// focus.
        /// </summary>
        private static KeyboardView CreateBoard()
        {
            return new KeyboardView
            {
                DataContext = CreateLayer(),
                KeySelectedCommand = new RelayCommand<KeyboardKeyViewModel>(_ => { })
            };
        }

        private static KeyboardLayerViewModel CreateLayer()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            return KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];
        }

        private static IReadOnlyList<KeyCapView> CapsOf(KeyboardView board)
        {
            return board.GetVisualDescendants().OfType<KeyCapView>().ToArray();
        }

        private static IReadOnlyList<KeyboardPanel> PanelsOf(KeyboardView board)
        {
            return board.GetVisualDescendants().OfType<KeyboardPanel>().ToArray();
        }

        /// <summary>
        /// The bordered box each section is drawn in, found by the radius it is the only thing in
        /// the picture to carry. Identifying it by the token rather than by position also asserts
        /// that it carries it.
        /// </summary>
        private static IReadOnlyList<Border> SectionBoxesOf(KeyboardView board)
        {
            var radius = (CornerRadius)DesignTokens.Resolve("RadiusBoardSection", ThemeVariant.Dark);

            return board.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.CornerRadius == radius)
                .ToArray();
        }

        private static Color? ColorOf(IBrush? brush)
        {
            return (brush as ISolidColorBrush)?.Color;
        }
    }
}
