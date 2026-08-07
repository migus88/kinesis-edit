using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Tests.Headless;
using StrikePath = Avalonia.Controls.Shapes.Path;

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

        /// <summary>One `:not(...)` group of a selector, so a state can be told from its negation.</summary>
        private static readonly Regex _negation = new(@":not\([^)]*\)", RegexOptions.Compiled);

        /// <summary>Every control theme these two files declare, and the target it templates.</summary>
        public static TheoryData<string, string, string> ControlThemesAndVariants()
        {
            var cases = new TheoryData<string, string, string>();

            foreach (var (key, targetType) in new[]
            {
                ("SegmentedControl", nameof(ListBox)),
                ("SegmentedItem", nameof(ListBoxItem)),
                ("ToggleSegment", nameof(Button)),
                ("DirectionSegment", nameof(Button)),
                ("SpeedBar", nameof(Button)),
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

                // A tab has no press face of its own: it keeps the hover fill all the way down, and
                // answers the click with the underline that arrives a moment later. Unlike the key
                // cap, whose theme says so outright, Tabs.axaml is silent about it — so this row is
                // where the absence is written down.
                { "TabStripItem", "pressed", "SurfaceKeySelectedBrush" },

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

                // One arrow of the lighting tab's direction row: the toggle's ramp, because "this
                // is the direction the effect runs" is the same kind of statement "this co-trigger
                // is on" is. `.unavailable` is its own state and is not a face — see the tests
                // below, and the theme's header for why it is not `:disabled`.
                { "DirectionSegment", "rest", "SurfaceBarBrush" },
                { "DirectionSegment", "hover", "SurfaceRaisedBrush" },
                { "DirectionSegment", "pressed", "SurfaceLineBrush" },
                { "DirectionSegment", "selected", "AccentBrush" },
                { "DirectionSegment", "disabled", "SurfaceInsetBrush" },

                // `.unavailable` is deliberately NOT a row here: this matrix probes the middle of
                // the face, and the strike runs corner to corner straight through it. Its face is
                // asserted off-diagonal in AnUnavailableDirection_IsStruckThroughInPlace instead.

                // One bar of the speed control. Unfilled is the INSET surface and not a transparent
                // face: the row has to read as a track with a level in it at speed 1, where eight
                // of the nine bars are empty.
                { "SpeedBar", "rest", "SurfaceInsetBrush" },
                { "SpeedBar", "hover", "SurfaceRaisedBrush" },
                { "SpeedBar", "pressed", "SurfaceLineBrush" },
                { "SpeedBar", "filled", "AccentBrush" },
                { "SpeedBar", "disabled", "SurfaceInsetBrush" },

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
        public void APointerPressOnATab_DoesNotPaintTheHalo(string variantName)
        {
            // The strip's half of the suppression rule. It is worth asserting separately from the
            // segment's: a tab's ring competes with the active mark for the same edge, so a ring
            // that appeared on a click would be the exact regression the underline part exists to
            // prevent, only triggered by the pointer instead of by the keyboard.
            var variant = ToVariant(variantName);
            var strip = Tabs(variant);

            using var host = ThemedHost.Show(strip, variant, HostWidth, HostHeight);

            var container = ContainerAt(strip, 1);

            Assert.True(container.Focus(NavigationMethod.Pointer), "The tab refused pointer focus.");

            Assert.Contains(":focus", container.Classes);
            Assert.DoesNotContain(":focus-visible", container.Classes);
            Assert.Equal(0, RootOf(container).BoxShadow.Count);
            Assert.Equal(Brushes.Transparent.Color, ((ISolidColorBrush)UnderlineOf(container).Background!).Color);

            AssertClose(DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant), PixelBeside(host, container));
        }

        /// <summary>The two button-shaped themes in these files, in both variants.</summary>
        public static TheoryData<string, string> ButtonThemesAndVariants()
        {
            var cases = new TheoryData<string, string>();

            foreach (var key in new[] { "ToggleSegment", "ModeOption" })
            {
                cases.Add(key, "Dark");
                cases.Add(key, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(ButtonThemesAndVariants))]
        public void KeyboardFocus_PaintsTheHaloOnAToggleAndOnARailRow(string key, string variantName)
        {
            // Both derive from BaseButton and neither restates the ring, so what this proves is that
            // deriving was enough: each declares a `.selected` BorderBrush of its own, and either
            // one could have taken the border back off the ring by dropping its qualifier.
            var variant = ToVariant(variantName);
            var button = SizedButton(key, variant);

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.True(button.Focus(NavigationMethod.Tab), $"'{key}' refused keyboard focus.");

            Assert.Contains(":focus-visible", button.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), button.BorderBrush);

            var shadows = RootOf(button).BoxShadow;

            Assert.Equal(1, shadows.Count);
            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), shadows[0].Color);
            Assert.Equal(3, shadows[0].Spread);

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentFocusHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                PixelBeside(host, button));
        }

        [AvaloniaTheory]
        [MemberData(nameof(ButtonThemesAndVariants))]
        public void APointerPressOnAToggleOrARailRow_DoesNotPaintTheHalo(string key, string variantName)
        {
            var variant = ToVariant(variantName);
            var button = SizedButton(key, variant);

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.True(button.Focus(NavigationMethod.Pointer), $"'{key}' refused pointer focus.");

            Assert.Contains(":focus", button.Classes);
            Assert.DoesNotContain(":focus-visible", button.Classes);
            Assert.Equal(0, RootOf(button).BoxShadow.Count);

            AssertClose(DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant), PixelBeside(host, button));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ADisabledToggleThatIsStillOn_TakesTheDisabledFace(string variantName)
        {
            // Contract 5, and the trap Pills.axaml's header spells out: Avalonia walks BasedOn
            // first, so BaseButton's `:disabled` face is applied BEFORE anything ToggleSegment
            // declares, and an unqualified `.selected` wins over it. A co-trigger the firmware has
            // gated off, or the pedal's Single Action / Macro latch on a board in demo mode, then
            // kept the full accent fill and the on-accent label and carried no disabled signal at
            // all — both are states the app really reaches. NavPill and FilterChip are guarded the
            // same way in PillThemeTests.
            //
            // ModeOption is not in this theory. It reaches the same face by a different route —
            // its own `:disabled` is declared after `.selected` and overrides it — which the
            // ButtonStates matrix already covers.
            var variant = ToVariant(variantName);
            var toggle = SizedButton("ToggleSegment", variant);

            toggle.Classes.Add("selected");
            toggle.IsEnabled = false;

            using var host = ThemedHost.Show(toggle, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), toggle.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), toggle.BorderBrush);
            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), toggle.Foreground);

            var frame = host.Capture();

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnUnavailableDirection_IsStruckThroughInPlace(string variantName)
        {
            // The design's own exception to "absent features are not shown, not disabled", stated
            // verbatim in mockup 2f: "Directions a mode can't use stay in place, struck through —
            // the row never changes shape as you move down the list." So the three things this has
            // to prove are: the strike is drawn, the arrow is out of reach, and the SLOT IS THE
            // SAME SIZE as an available one — which is the whole reason the state exists.
            // Both are built at their NATURAL size — no Width or Height — so the "same slot" claim
            // is the theme's answer and not the fixture's.
            var variant = ToVariant(variantName);
            var available = new Button { Theme = Theme("DirectionSegment", variant), Content = new Border { Width = 16, Height = 16 } };
            var struck = new Button { Theme = Theme("DirectionSegment", variant), Content = new Border { Width = 16, Height = 16 } };

            struck.Classes.Add("unavailable");

            var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { available, struck } };

            using var host = ThemedHost.Show(row, variant, HostWidth, HostHeight);

            Assert.Equal(available.Bounds.Size, struck.Bounds.Size);

            Assert.Equal(
                DesignTokens.Resolve("TextDisabledBrush", variant),
                StrikeOf(struck).Stroke);
            Assert.Equal(Brushes.Transparent.Color, ((ISolidColorBrush)StrikeOf(available).Stroke!).Color);

            // The face goes with the interactivity — the slot is kept, the button is not. Probed
            // off the diagonal: the strike crosses the middle of the box, which is why this state
            // has no row in the face matrix.
            AssertClose(DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant), FaceOf(host, struck));

            // Not interactive and not a tab stop — set by the class rather than left to the view,
            // because an arrow that could still be tabbed to and pressed would be a lie the view
            // could forget to tell. It is NOT `:disabled`: the strike says "this mode does not run
            // that way", which is a fact about the mode and not about the app's willingness.
            Assert.False(struck.Focusable);
            Assert.False(struck.IsHitTestVisible);
            Assert.True(struck.IsEnabled, "The struck arrow was disabled; the strike is the statement, not a dim face.");
            Assert.False(struck.Focus(NavigationMethod.Tab), "A struck arrow took keyboard focus.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnUnavailableDirectionThatIsAlsoSelected_IsNeverDrawnAsTheChosenOne(string variantName)
        {
            // Contract 5. A view model that had not yet cleared a direction the newly picked mode
            // cannot honour would write both classes, and an unqualified `.selected` would leave a
            // struck arrow wearing the full accent fill — the worst of both statements.
            var variant = ToVariant(variantName);
            var button = SizedButton("DirectionSegment", variant);

            button.Classes.Add("selected");
            button.Classes.Add("unavailable");

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), StrikeOf(button).Stroke);
            AssertClose(DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant), FaceOf(host, button));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ARowOfSpeedBars_FillsUpToTheChosenLevelAndNoFurther(string variantName)
        {
            // "Speed as segmented bars (accent-filled)" (docs/design/handoff.md:150). The row's
            // meaning is cumulative — bars at or below the chosen speed are filled — which is a
            // fact about the row and arrives as a class per bar; the theme knows only "filled" and
            // "not", and this is that pair drawn side by side at the glass.
            var variant = ToVariant(variantName);
            // At the theme's own size, not the probe size the other fixtures use: nine 120px
            // buttons would not fit the host, and a bar IS its size — see the test below.
            var bars = Enumerable.Range(0, 9)
                .Select(_ => new Button { Theme = Theme("SpeedBar", variant) })
                .ToArray();

            for (var index = 0; index < 6; index++)
            {
                bars[index].Classes.Add("filled");
            }

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            foreach (var bar in bars)
            {
                row.Children.Add(bar);
            }

            using var host = ThemedHost.Show(row, variant, HostWidth, HostHeight);

            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant);

            AssertClose(
                Composite(DesignTokens.ResolveBrushColor("AccentBrush", variant), canvas),
                FaceOf(host, bars[5]));
            AssertClose(
                Composite(DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant), canvas),
                FaceOf(host, bars[6]));

            // Every bar is the same size whether it is filled or not, or the row would jump as the
            // speed changed.
            Assert.All(bars, bar => Assert.Equal(bars[0].Bounds.Size, bar.Bounds.Size));
        }

        [AvaloniaFact]
        public void ASpeedBar_IsABarRatherThanAButton()
        {
            // It holds no content at all, so the theme owns its size outright — a padding-sized
            // button would collapse to nothing.
            var bar = new Button { Theme = Theme("SpeedBar", ThemeVariant.Dark) };

            using var host = ThemedHost.Show(bar, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.Equal(10, bar.Bounds.Width);
            Assert.Equal(18, bar.Bounds.Height);
            Assert.Equal(default, bar.Padding);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AModeRailRow_CarriesItsNameOverItsParameterSummary(string variantName)
        {
            // Mockup 2f writes every row as a pair — "Wave — spd · L/R" — which is what turns the
            // rail from a list of names into the answer to "what will change if I pick this one".
            // The second line is CONTENT, not a template part (a Button has one content slot), so
            // the muted role is a descendant style in Styles/Editor.axaml; this is the pair of them
            // working together on a real button.
            var variant = ToVariant(variantName);
            var name = new TextBlock { Text = "Wave" };
            var summary = new TextBlock { Text = "spd · L/R", Classes = { "modeSummary" } };
            var row = new Button
            {
                Classes = { "modeOption" },
                Content = new StackPanel { Children = { name, summary } }
            };

            using var host = ThemedHost.Show(row, variant, HostWidth, HostHeight);

            Assert.Same(DesignTokens.Resolve("ModeOption", variant), row.Theme);

            Assert.True(
                summary.Bounds.Height > 0 && name.Bounds.Height > 0,
                "One of the two lines was not drawn at all.");
            Assert.True(
                summary.TranslatePoint(default, row)!.Value.Y > name.TranslatePoint(default, row)!.Value.Y,
                "The parameter summary is not under the mode's name.");

            // Muted rather than smaller: the type scale has no sans step below 11 — see the
            // deviation in docs/app/design-system.md — so colour is what separates the two lines,
            // and the weight step the row takes on selection belongs to the name alone.
            Assert.Equal(DesignTokens.Resolve("TextMutedBrush", variant), summary.Foreground);
            Assert.Equal(FontWeight.Normal, summary.FontWeight);

            row.Classes.Add("selected");

            // On the accent fill TextMuted is unreadable, so the summary joins the label colour that
            // rides a solid accent face. It keeps its 400 against the name's 500.
            Assert.Equal(DesignTokens.Resolve("AccentTextBrush", variant), summary.Foreground);
            Assert.Equal(FontWeight.Medium, row.FontWeight);
            Assert.Equal(FontWeight.Normal, summary.FontWeight);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ADisabledActiveTab_LosesItsUnderlineWithTheRestOfTheState(string variantName)
        {
            // The same hole in the other file, and the one the dim face cannot plug on its own: the
            // `:disabled` style below `:selected` takes the label back, but the mark lives on a
            // template part that nothing there touches — so a gated active tab wore a full accent
            // underline over a TextDisabled label. Latent today, because no device disables the
            // section it is showing; wrong all the same, and a tab is exactly the control an
            // editor would later gate.
            var variant = ToVariant(variantName);
            var strip = Tabs(variant);

            using var host = ThemedHost.Show(strip, variant, HostWidth, HostHeight);

            var active = ContainerAt(strip, 0);

            Assert.True(IsSelected(active), "The fixture's first tab is not the active one.");

            active.IsEnabled = false;

            Assert.True(IsSelected(active), "Disabling the tab cleared the selection; the fixture proves nothing.");

            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), active.Foreground);
            Assert.Equal(FontWeight.Normal, active.FontWeight);
            Assert.Equal(Brushes.Transparent.Color, ((ISolidColorBrush)UnderlineOf(active).Background!).Color);

            AssertClose(DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant), UnderlinePixel(host, active));
        }

        [AvaloniaTheory]
        [MemberData(nameof(ButtonThemesAndVariants))]
        public void SelectionAndFocusOnAToggleOrARailRow_CoexistAndStayDistinguishable(string key, string variantName)
        {
            // Contract 4 on the two themes that carry `.selected` as a class rather than as a
            // pseudo-class: the accent fill is selection's, the border and the halo are focus's, and
            // the selected border yields only because it is written `:not(:focus-visible)`.
            var variant = ToVariant(variantName);
            var button = SizedButton(key, variant);

            button.Classes.Add("selected");

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("AccentSelectedRingBrush", variant), button.BorderBrush);
            Assert.Equal(0, RootOf(button).BoxShadow.Count);

            Assert.True(button.Focus(NavigationMethod.Tab), $"'{key}' refused keyboard focus.");

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), button.Background);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), button.BorderBrush);
            Assert.Equal(1, RootOf(button).BoxShadow.Count);

            var frame = host.Capture();

            AssertClose(
                DesignTokens.ResolveBrushColor("AccentBrush", variant),
                FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APressedSegment_SinksPastItsHoverFace(string variantName)
        {
            // Avalonia raises `:pointerover` AND `:pressed` while the pointer is down, so both
            // fills match a segment under the finger and only the hover style's `:not(:pressed)`
            // decides between them. This is the outcome; the selector that guarantees it rather
            // than a file order that happens to produce it is
            // EveryHoverFaceInTheSegmentedFile_YieldsToThePressInItsSelector.
            var variant = ToVariant(variantName);
            var segmented = Segmented(variant);

            using var host = ThemedHost.Show(segmented, variant, HostWidth, HostHeight);

            var container = ContainerAt(segmented, 1);

            SetPseudoClasses(container, ":pointerover");

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("SurfaceKeySelectedBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant)),
                FaceOf(host, container));

            SetPseudoClasses(container, ":pressed");

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("SurfaceRaisedBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant)),
                FaceOf(host, container));
        }

        [AvaloniaFact]
        public void EveryHoverFaceInTheSegmentedFile_YieldsToThePressInItsSelector()
        {
            // Contract 5, and the one claim in this file no rendered pixel can reach. A hover fill
            // and a press fill both match while the pointer is down, so the winner is whichever
            // Avalonia applied last — the file's order — unless the hover selector says otherwise.
            // APressedSegment_SinksPastItsHoverFace pins today's outcome and would go on passing
            // after a reorder made that outcome accidental, which is what this reads the source for.
            //
            // Scoped to Segmented.axaml, because both themes in it declare a press face to compete
            // with. Tabs.axaml deliberately does not: a tab keeps its hover fill all the way down
            // (see the ContainerStates row that says so), and the qualifier there would strip the
            // face off a tab under the finger.
            var markup = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Themes/ControlThemes/Segmented.axaml"]);
            var guarded = 0;

            foreach (var selector in FaceSelectorsIn(markup).Where(IsHover))
            {
                Assert.True(
                    selector.Contains(":not(:pressed)", StringComparison.Ordinal),
                    $"The hover face '{selector}' beats the press face only by file order.");

                guarded++;
            }

            // A guard that matched nothing would pass for the wrong reason. Four: the segment inside
            // the trough, the standalone toggle, one direction arrow and one speed bar.
            Assert.Equal(4, guarded);
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
            var direction = new Button { Theme = Theme("DirectionSegment", ThemeVariant.Dark) };
            var speed = new Button { Theme = Theme("SpeedBar", ThemeVariant.Dark) };
            var panel = new StackPanel { Children = { segmented, strip, toggle, mode, direction, speed } };

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

                // Every `/template/` here targets Border#Root, Border#Underline or Path#Strike, all
                // declared by the ControlTheme that selects them.
                foreach (var selector in TemplateSelectorsIn(markup))
                {
                    found++;

                    Assert.True(
                        selector.Contains("Border#Root", StringComparison.Ordinal)
                            || selector.Contains("Border#Underline", StringComparison.Ordinal)
                            || selector.Contains("Path#Strike", StringComparison.Ordinal),
                        $"{path} reaches for '{selector}', which is not one of its own parts.");
                }
            }

            // A guard that matched nothing would pass for the wrong reason. Four: the two container
            // themes reach for their own halo, the tab additionally for its underline, and the
            // direction arrow for the strike that is its `.unavailable` state. The other
            // button-shaped themes reach for nothing, because BaseButton already carries the halo.
            Assert.Equal(4, found);
        }

        private static IEnumerable<string> TemplateSelectorsIn(string markup)
        {
            return markup
                .Split('"')
                .Where(fragment => fragment.Contains("/template/", StringComparison.Ordinal));
        }

        /// <summary>
        /// The selector of every style in <paramref name="markup"/> that paints a face. A style
        /// that sets no Background cannot disagree with another one about which face is showing.
        /// </summary>
        private static IEnumerable<string> FaceSelectorsIn(string markup)
        {
            foreach (var style in markup.Split("<Style Selector=\"", StringSplitOptions.None).Skip(1))
            {
                var selector = style[..style.IndexOf('"')];
                var end = style.IndexOf("</Style>", StringComparison.Ordinal);
                var body = end < 0 ? style : style[..end];

                if (body.Contains("Property=\"Background\"", StringComparison.Ordinal))
                {
                    yield return selector;
                }
            }
        }

        /// <summary>
        /// Whether <paramref name="selector"/> REQUIRES <c>:pointerover</c> rather than merely
        /// excluding it: `:not(:pointerover)` names the state and makes the opposite claim.
        /// </summary>
        private static bool IsHover(string selector)
        {
            return _negation.Replace(selector, string.Empty).Contains(":pointerover", StringComparison.Ordinal);
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

        /// <summary>A bare button on <paramref name="key"/>, sized so a probe has room beside it.</summary>
        private static Button SizedButton(string key, ThemeVariant variant)
        {
            return new Button
            {
                Theme = Theme(key, variant),
                Width = ButtonWidth,
                Height = ButtonHeight,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        }

        private static void AssertButtonFacePaints(string key, string state, string? expectedKey, ThemeVariant variant)
        {
            var button = SizedButton(key, variant);

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
                case "filled":
                    button.Classes.Add("filled");
                    break;
                case "unavailable":
                    button.Classes.Add("unavailable");
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

        /// <summary>The <c>Path#Strike</c> an unavailable direction is crossed out with.</summary>
        private static StrikePath StrikeOf(Control segment)
        {
            return segment.GetVisualDescendants()
                .OfType<StrikePath>()
                .Single(path => path.Name == "Strike");
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
