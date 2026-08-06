using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Tests.Headless;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The segmented and tab control themes — <c>Themes/ControlThemes/Segmented.axaml</c> and
    /// <c>Tabs.axaml</c>: the layer switcher and the lighting speed bars, the two-state toggles,
    /// the editor's section strip and the lighting mode rail.
    /// <para>
    /// These themes fail in ways a resource test cannot see. Avalonia has no selector specificity,
    /// so a state can be declared and still lose to the one below it; the active tab's underline
    /// and the focus ring were the same property until this layer split them, so a merely focused
    /// tab could read as the active one; and a container that clips its children erases exactly the
    /// halo the design insists on. All three are only visible at the glass, so most of what follows
    /// renders a real frame and reads the pixel back.
    /// </para>
    /// </summary>
    public class SegmentedAndTabThemeTests
    {
        private const double HostWidth = 320;

        private const double HostHeight = 140;

        private const double ButtonWidth = 120;

        private const double ButtonHeight = 32;

        /// <summary>
        /// How far in from a segment's left edge a probe lands. Inside the face, outside the corner
        /// rounding at mid-height, and clear of the label: the tightest padding any theme here
        /// declares is 4px horizontally, so a glyph never reaches this column.
        /// </summary>
        private const int FaceProbeInset = 3;

        /// <summary>The captions the segmented and tab fixtures are built from.</summary>
        private static readonly string[] _items = ["Top", "Fn", "Edge"];

        /// <summary>Every control theme these two files declare, and the target it templates.</summary>
        public static TheoryData<string, string, string> ControlThemesAndVariants()
        {
            var cases = new TheoryData<string, string, string>();

            foreach (var (key, targetType) in new[]
            {
                ("SegmentedControl", nameof(ListBox)),
                ("SegmentedItem", nameof(ListBoxItem)),
                ("ToggleSegment", nameof(Button)),
                ("TabStrip", nameof(TabStrip)),
                ("TabStripItem", nameof(TabStripItem)),
                ("ModeOption", nameof(Button))
            })
            {
                cases.Add(key, targetType, "Dark");
                cases.Add(key, targetType, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(ControlThemesAndVariants))]
        public void EveryControlTheme_ResolvesInBothVariants(string key, string targetType, string variantName)
        {
            var theme = Assert.IsType<ControlTheme>(DesignTokens.Resolve(key, ToVariant(variantName)));

            Assert.Equal(targetType, theme.TargetType?.Name);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheSegmentedControl_ThemesItsOwnContainers(string variantName)
        {
            // A consumer names the outer key only. If the ItemContainerTheme is missing the segments
            // silently fall back to Fluent's ListBoxItem, which resolves, renders, and is simply the
            // wrong control.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            foreach (var index in Enumerable.Range(0, _items.Length))
            {
                Assert.Same(DesignTokens.Resolve("SegmentedItem", variant), ContainerAt(segmented, index).Theme);
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheTabStrip_ThemesItsOwnContainers(string variantName)
        {
            var variant = ToVariant(variantName);
            var strip = Tabs(variant);

            using var host = ThemedHost.Show(strip, variant, HostWidth, HostHeight);

            foreach (var index in Enumerable.Range(0, _items.Length))
            {
                Assert.Same(DesignTokens.Resolve("TabStripItem", variant), ContainerAt(strip, index).Theme);
            }
        }

        /// <summary>
        /// The state matrix of the two container-generated themes at the glass: theme, state, and
        /// the token the face is supposed to end up painted with. A <c>null</c> expectation means
        /// "no face of its own" — the surface behind it shows through.
        /// </summary>
        public static TheoryData<string, string, string?> ContainerStates()
        {
            return new TheoryData<string, string, string?>
            {
                { "SegmentedItem", "rest", null },
                { "SegmentedItem", "hover", "SurfaceKeySelectedBrush" },
                { "SegmentedItem", "pressed", "SurfaceRaisedBrush" },
                { "SegmentedItem", "selected", "AccentBrush" },
                { "SegmentedItem", "disabled", null },

                { "TabStripItem", "rest", null },
                { "TabStripItem", "hover", "SurfaceKeySelectedBrush" },
                { "TabStripItem", "selected", null },
                { "TabStripItem", "disabled", null }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(ContainerStates))]
        public void TheContainerStateMatrix_PaintsItsTokenInDark(string key, string state, string? expectedKey)
        {
            AssertContainerFacePaints(key, state, expectedKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(ContainerStates))]
        public void TheContainerStateMatrix_PaintsItsTokenInLight(string key, string state, string? expectedKey)
        {
            AssertContainerFacePaints(key, state, expectedKey, ThemeVariant.Light);
        }

        /// <summary>The state matrix of the two button-shaped themes, read the same way.</summary>
        public static TheoryData<string, string, string?> ButtonStates()
        {
            return new TheoryData<string, string, string?>
            {
                { "ToggleSegment", "rest", "SurfaceBarBrush" },
                { "ToggleSegment", "hover", "SurfaceRaisedBrush" },
                { "ToggleSegment", "pressed", "SurfaceLineBrush" },
                { "ToggleSegment", "selected", "AccentBrush" },
                { "ToggleSegment", "disabled", "SurfaceInsetBrush" },

                { "ModeOption", "rest", null },
                { "ModeOption", "hover", "SurfaceKeySelectedBrush" },
                { "ModeOption", "pressed", "SurfaceRaisedBrush" },
                { "ModeOption", "selected", "AccentBrush" },
                { "ModeOption", "disabled", null }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(ButtonStates))]
        public void TheButtonStateMatrix_PaintsItsTokenInDark(string key, string state, string? expectedKey)
        {
            AssertButtonFacePaints(key, state, expectedKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(ButtonStates))]
        public void TheButtonStateMatrix_PaintsItsTokenInLight(string key, string state, string? expectedKey)
        {
            AssertButtonFacePaints(key, state, expectedKey, ThemeVariant.Light);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APressedSegment_KeepsItsSelectedFace(string variantName)
        {
            // Contract 5, the half a matrix row cannot express: the pointer states carry
            // `:not(:selected)`, so pressing the segment that is already chosen does not flash the
            // neutral press fill on the way to doing nothing.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            var container = ContainerAt(segmented, 0);

            Assert.True(IsSelected(container), "The fixture's first segment is not the selected one.");

            SetPseudoClasses(container, ":pointerover", ":pressed");

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant)),
                FaceOf(host, container));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheActiveTab_IsUnderlinedInAccent(string variantName)
        {
            // "active tab = 500 weight + `inset 0 -2px 0 #5B9DF9` underline" (docs/design/handoff.md).
            var variant = ToVariant(variantName);
            var strip = Tabs(variant);

            using var host = ThemedHost.Show(strip, variant, HostWidth, HostHeight);

            var active = ContainerAt(strip, 0);
            var underline = UnderlineOf(active);

            Assert.Equal(FontWeight.Medium, active.FontWeight);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), underline.Background);

            // Two device-independent pixels, along the tab's whole width, flush with its bottom
            // edge: the mark is an inset shadow in the design, not a pipe under the label.
            Assert.Equal(2, underline.Bounds.Height);
            Assert.Equal(active.Bounds.Width, underline.Bounds.Width);

            AssertClose(DesignTokens.ResolveBrushColor("AccentBrush", variant), UnderlinePixel(host, active));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnInactiveTab_CarriesNoUnderlineAtAll(string variantName)
        {
            var variant = ToVariant(variantName);
            var strip = Tabs(variant);

            using var host = ThemedHost.Show(strip, variant, HostWidth, HostHeight);

            var inactive = ContainerAt(strip, 1);

            Assert.False(IsSelected(inactive), "The fixture's second tab is selected.");
            Assert.Equal(FontWeight.Normal, inactive.FontWeight);

            // The strip's own inset surface, undisturbed: the mark is transparent rather than
            // absent, so the tab does not reflow when it becomes the active one.
            AssertClose(DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant), UnderlinePixel(host, inactive));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AFocusedInactiveTab_IsNotUnderlined(string variantName)
        {
            // The regression that made the underline its own template part. Both the mark and the
            // focus ring used to be BorderBrush, so a tab that was merely focused painted itself an
            // accent underline and read as the active one.
            var variant = ToVariant(variantName);
            var strip = Tabs(variant);

            using var host = ThemedHost.Show(strip, variant, HostWidth, HostHeight);

            var inactive = ContainerAt(strip, 1);

            Assert.True(inactive.Focus(NavigationMethod.Tab), "The tab refused keyboard focus.");
            Assert.False(IsSelected(inactive), "Focusing the tab selected it; the fixture cannot tell the two apart.");

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), inactive.BorderBrush);
            Assert.Equal(1, RootOf(inactive).BoxShadow.Count);

            // ...and the mark stayed off.
            Assert.Equal(Brushes.Transparent.Color, ((ISolidColorBrush)UnderlineOf(inactive).Background!).Color);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnActiveTabThatIsAlsoFocused_ShowsBoth(string variantName)
        {
            // Contract 4. The mark is selection's, the border and the halo are focus's, and they are
            // different shapes: one is 2px along the bottom, the other is 1px all the way round.
            var variant = ToVariant(variantName);
            var strip = Tabs(variant);

            using var host = ThemedHost.Show(strip, variant, HostWidth, HostHeight);

            var active = ContainerAt(strip, 0);

            Assert.True(active.Focus(NavigationMethod.Tab), "The tab refused keyboard focus.");
            Assert.True(IsSelected(active));

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), UnderlineOf(active).Background);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), active.BorderBrush);
            Assert.Equal(1, RootOf(active).BoxShadow.Count);
            AssertClose(DesignTokens.ResolveBrushColor("AccentBrush", variant), UnderlinePixel(host, active));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void KeyboardFocus_PaintsTheHaloOnASegment(string variantName)
        {
            // "Ring is 1px accent border + 3px 28% halo, never an outline offset" (2b) — and the
            // trough must not clip it, which is what the pixel two columns outside the segment's own
            // edge proves. Contract 6: a ClipToBounds anywhere up the chain erases it.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            var container = ContainerAt(segmented, 1);

            Assert.True(container.Focus(NavigationMethod.Tab), "The segment refused keyboard focus.");

            Assert.Contains(":focus-visible", container.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), container.BorderBrush);

            var shadows = RootOf(container).BoxShadow;

            Assert.Equal(1, shadows.Count);

            var shadow = shadows[0];

            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), shadow.Color);
            Assert.Equal(3, shadow.Spread);
            Assert.Equal(0, shadow.OffsetX);
            Assert.Equal(0, shadow.OffsetY);

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentFocusHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant)),
                PixelBeside(host, container));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APointerPress_DoesNotPaintTheHalo(string variantName)
        {
            // The other half of the rule: "mouse clicks suppress it". Nothing tracks the input
            // source by hand — NavigationMethod.Pointer simply does not raise :focus-visible.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            var container = ContainerAt(segmented, 1);

            Assert.True(container.Focus(NavigationMethod.Pointer), "The segment refused pointer focus.");

            Assert.Contains(":focus", container.Classes);
            Assert.DoesNotContain(":focus-visible", container.Classes);
            Assert.Equal(0, RootOf(container).BoxShadow.Count);

            AssertClose(DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant), PixelBeside(host, container));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ASelectedSegment_KeepsItsOwnRing(string variantName)
        {
            // Selection has its own border role, so a chosen segment is legible with no focus
            // anywhere — and it is NOT the ring focus draws, which is what keeps the two apart.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            var container = ContainerAt(segmented, 0);

            Assert.True(IsSelected(container));
            Assert.Equal(DesignTokens.Resolve("AccentSelectedRingBrush", variant), container.BorderBrush);
            Assert.Equal(0, RootOf(container).BoxShadow.Count);

            // One weight step, as the mockups draw the switcher: 400 at rest, 500 when chosen.
            Assert.Equal(FontWeight.Medium, container.FontWeight);
            Assert.Equal(FontWeight.Normal, ContainerAt(segmented, 1).FontWeight);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void SelectionAndFocus_CoexistAndStayDistinguishable(string variantName)
        {
            // Contract 4 on the segment: the filled face is selection's, the border and the halo are
            // focus's, and neither erases the other.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            var container = ContainerAt(segmented, 0);

            Assert.True(container.Focus(NavigationMethod.Tab), "The segment refused keyboard focus.");
            Assert.True(IsSelected(container));

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), container.Background);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), container.BorderBrush);
            Assert.Equal(1, RootOf(container).BoxShadow.Count);

            AssertClose(DesignTokens.ResolveBrushColor("AccentBrush", variant), FaceOf(host, container));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ThePointerStates_LoseToTheFocusRing(string variantName)
        {
            // Avalonia has no selector specificity, so "focus owns the border" is only true because
            // every pointer-state BorderBrush setter is written `:not(:focus-visible)`. Reorder the
            // file and this is what notices.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            var container = ContainerAt(segmented, 1);

            Assert.True(container.Focus(NavigationMethod.Tab), "The segment refused keyboard focus.");

            SetPseudoClasses(container, ":pointerover");

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), container.BorderBrush);

            // The fill is still the hover fill: focus takes the border, not the face.
            Assert.Equal(DesignTokens.Resolve("SurfaceKeySelectedBrush", variant), container.Background);
        }

        [AvaloniaFact]
        public void NoSegmentOrTabContainer_CarriesATransitions()
        {
            // Contract 8, and the budget's two deliberate zeros: "tab / layer swap - 0 ms - instant,
            // no slide". Writing our own template drops the 75 ms press animation Fluent's Button
            // ships, and the explicit clear keeps it dropped for anything a later style might set.
            var segmented = Segmented(ThemeVariant.Dark);
            var strip = Tabs(ThemeVariant.Dark);
            var toggle = new Button { Theme = Theme("ToggleSegment", ThemeVariant.Dark) };
            var mode = new Button { Theme = Theme("ModeOption", ThemeVariant.Dark) };
            var panel = new StackPanel { Children = { segmented, strip, toggle, mode } };

            using var host = ThemedHost.Show(panel, ThemeVariant.Dark, HostWidth, HostHeight);

            var animated = panel.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.Transitions is { Count: > 0 })
                .Select(control => control.GetType().Name)
                .ToArray();

            Assert.True(animated.Length == 0, $"These carry a Transitions: {string.Join(", ", animated)}.");
        }

        [AvaloniaFact]
        public void TheseThemes_NameNoTemplatePartOutsideTheirOwnControlTheme()
        {
            // The point of the whole layer: a `/template/` selector is only legitimate inside the
            // ControlTheme that declares the part, and no part is ever named with a PART_ prefix —
            // that name is what Fluent's own templates use and what Styles/ used to reach into.
            var found = 0;

            foreach (var path in new[] { "Themes/ControlThemes/Segmented.axaml", "Themes/ControlThemes/Tabs.axaml" })
            {
                var markup = AuthoredXaml.WithoutComments(AuthoredXaml.Files()[path]);

                Assert.DoesNotContain("PART_", markup, StringComparison.Ordinal);

                // Every `/template/` here targets Border#Root or Border#Underline, both declared by
                // the ControlTheme that selects them.
                foreach (var selector in TemplateSelectorsIn(markup))
                {
                    found++;

                    Assert.True(
                        selector.Contains("Border#Root", StringComparison.Ordinal)
                            || selector.Contains("Border#Underline", StringComparison.Ordinal),
                        $"{path} reaches for '{selector}', which is not one of its own parts.");
                }
            }

            // A guard that matched nothing would pass for the wrong reason. Three: the two container
            // themes reach for their own halo, and the tab additionally for its underline. The
            // button-shaped themes reach for nothing, because BaseButton already carries the halo.
            Assert.Equal(3, found);
        }

        private static IEnumerable<string> TemplateSelectorsIn(string markup)
        {
            return markup
                .Split('"')
                .Where(fragment => fragment.Contains("/template/", StringComparison.Ordinal));
        }

        private static void AssertContainerFacePaints(string key, string state, string? expectedKey, ThemeVariant variant)
        {
            var isTab = key == "TabStripItem";
            var host = isTab
                ? (SelectingItemsControl)Tabs(variant)
                : Segmented(variant);

            using var themed = ThemedHost.Show(host, variant, HostWidth, HostHeight);

            // The second item, so "selected" can be switched on without the fixture starting there.
            var container = ContainerAt(host, 1);

            // After Show, not before: a container re-syncs its pointer pseudo-classes when its
            // template is applied, so a state raised beforehand is wiped by the first layout pass.
            switch (state)
            {
                case "rest":
                    break;
                case "hover":
                    SetPseudoClasses(container, ":pointerover");
                    break;
                case "pressed":
                    SetPseudoClasses(container, ":pointerover", ":pressed");
                    break;
                case "selected":
                    host.SelectedIndex = 1;
                    Assert.True(IsSelected(container), "The container did not take the selection.");
                    break;
                case "disabled":
                    container.IsEnabled = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown container state.");
            }

            // Both containers are transparent at rest and sit on their container's inset surface.
            var behind = DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant);
            var expected = expectedKey is null
                ? behind
                : Composite(DesignTokens.ResolveBrushColor(expectedKey, variant), behind);

            AssertClose(expected, FaceOf(themed, container));
        }

        private static void AssertButtonFacePaints(string key, string state, string? expectedKey, ThemeVariant variant)
        {
            var button = new Button
            {
                Theme = Theme(key, variant),
                Width = ButtonWidth,
                Height = ButtonHeight,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            switch (state)
            {
                case "rest":
                    break;
                case "hover":
                    SetPseudoClasses(button, ":pointerover");
                    break;
                case "pressed":
                    SetPseudoClasses(button, ":pointerover", ":pressed");
                    break;
                case "selected":
                    button.Classes.Add("selected");
                    break;
                case "disabled":
                    button.IsEnabled = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown button state.");
            }

            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant);
            var expected = expectedKey is null
                ? canvas
                : Composite(DesignTokens.ResolveBrushColor(expectedKey, variant), canvas);

            var frame = host.Capture();

            AssertClose(expected, FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
        }

        /// <summary>The layer-switcher fixture: three segments in a trough, the first one chosen.</summary>
        private static ListBox Segmented(ThemeVariant variant)
        {
            return new ListBox
            {
                Theme = Theme("SegmentedControl", variant),
                ItemsSource = _items,
                SelectedIndex = 0
            };
        }

        /// <summary>The section-strip fixture: three tabs, the first one active.</summary>
        private static TabStrip Tabs(ThemeVariant variant)
        {
            return new TabStrip
            {
                Theme = Theme("TabStrip", variant),
                ItemsSource = _items,
                SelectedIndex = 0
            };
        }

        private static ControlTheme Theme(string key, ThemeVariant variant)
        {
            return (ControlTheme)DesignTokens.Resolve(key, variant);
        }

        /// <summary>
        /// The container the strip or the trough generated for item <paramref name="index"/>. Typed
        /// as <see cref="ContentControl"/> because that is the whole of what a ListBoxItem and a
        /// TabStripItem share as classes; selection is read through <see cref="ISelectable"/>.
        /// </summary>
        private static ContentControl ContainerAt(ItemsControl items, int index)
        {
            return (ContentControl?)items.ContainerFromIndex(index)
                ?? throw new InvalidOperationException($"No container was generated for item {index}.");
        }

        /// <inheritdoc cref="ContainerAt" />
        private static bool IsSelected(ContentControl container)
        {
            return ((ISelectable)container).IsSelected;
        }

        /// <summary>The <c>Border#Root</c> of <paramref name="control"/>'s own template.</summary>
        private static Border RootOf(Control control)
        {
            return control.GetVisualDescendants()
                .OfType<Border>()
                .First(border => border.Name == "Root");
        }

        /// <summary>The <c>Border#Underline</c> a tab draws its active mark with.</summary>
        private static Border UnderlineOf(Control tab)
        {
            return tab.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "Underline");
        }

        /// <summary>
        /// The colour a few pixels in from <paramref name="control"/>'s left edge, at mid-height:
        /// on its face, clear of its corner rounding and of its label.
        /// </summary>
        private static Color FaceOf(ThemedHost host, Control control)
        {
            return PixelOf(host, control, new Point(FaceProbeInset, control.Bounds.Height / 2));
        }

        /// <summary>
        /// The colour two pixels outside <paramref name="control"/>'s left edge, which is inside the
        /// 3px halo when there is one and the surface behind it when there is not.
        /// </summary>
        private static Color PixelBeside(ThemedHost host, Control control)
        {
            return PixelOf(host, control, new Point(-2, control.Bounds.Height / 2));
        }

        /// <summary>The colour on the bottom row of a tab, where its active mark is drawn.</summary>
        private static Color UnderlinePixel(ThemedHost host, Control tab)
        {
            return PixelOf(host, tab, new Point(tab.Bounds.Width / 2, tab.Bounds.Height - 1));
        }

        private static Color PixelOf(ThemedHost host, Control control, Point point)
        {
            var frame = host.Capture();
            var probe = control.TranslatePoint(point, host.Window)
                ?? throw new InvalidOperationException("The control is not in the window's visual tree.");

            return FramePixels.At(frame, (int)probe.X, (int)probe.Y);
        }

        /// <summary>
        /// Raises pseudo-classes by hand. The headless session has no pointer, so <c>:pointerover</c>
        /// and <c>:pressed</c> are set through the same interface Avalonia's own input code uses.
        /// </summary>
        private static void SetPseudoClasses(Control control, params string[] pseudoClasses)
        {
            var classes = (IPseudoClasses)control.Classes;

            foreach (var pseudoClass in pseudoClasses)
            {
                classes.Set(pseudoClass, true);
            }
        }

        private static ThemeVariant ToVariant(string variantName)
        {
            return variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>Source-over composite of a possibly translucent colour onto an opaque one.</summary>
        private static Color Composite(Color source, Color background)
        {
            var alpha = source.A / 255d;

            return Color.FromRgb(
                (byte)Math.Round((source.R * alpha) + (background.R * (1 - alpha))),
                (byte)Math.Round((source.G * alpha) + (background.G * (1 - alpha))),
                (byte)Math.Round((source.B * alpha) + (background.B * (1 - alpha))));
        }

        /// <summary>Channel-sum distance, so a rounding difference is not a failure.</summary>
        private static int Distance(Color left, Color right)
        {
            return Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B);
        }

        private static void AssertClose(Color expected, Color actual)
        {
            Assert.True(Distance(expected, actual) <= 3, $"Expected about {expected}, painted {actual}.");
        }
    }
}
