using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Tests.Headless;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The control-theme layer — <c>Themes/ControlThemes/Shared.axaml</c> and
    /// <c>Buttons.axaml</c> — and the class-to-theme bridge in <c>Styles/Buttons.axaml</c>.
    /// <para>
    /// A control theme fails in two ways a resource test cannot see. It can resolve and never
    /// reach the control, because the class that was supposed to point at it is missing or misspelt
    /// — a <c>Theme</c> setter naming nothing simply leaves the control on Fluent's own template.
    /// And a state can be declared and still lose, because Avalonia has no selector specificity and
    /// the last matching setter wins. Both are only visible at the glass, so most of what follows
    /// renders a real frame and reads the pixel back.
    /// </para>
    /// </summary>
    public class ControlThemeFoundationTests
    {
        private const double HostWidth = 240;

        private const double HostHeight = 120;

        private const double ButtonWidth = 120;

        private const double ButtonHeight = 40;

        /// <summary>Side of the square the hatch tile is rendered into; twelve 4px tiles wide.</summary>
        private const int HatchProbeSize = 48;

        /// <summary>Every control theme this layer declares, and the target it templates.</summary>
        public static TheoryData<string, string> ControlThemesAndVariants()
        {
            var cases = new TheoryData<string, string>();

            foreach (var key in new[]
            {
                "BaseButton",
                "PrimaryActionButton",
                "SecondaryButton",
                "EjectButton",
                "DiscardButton",
                "GhostButton",
                "LinkButton",
                "RowButton"
            })
            {
                cases.Add(key, "Dark");
                cases.Add(key, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(ControlThemesAndVariants))]
        public void EveryControlTheme_ResolvesInBothVariants(string key, string variantName)
        {
            var variant = ToVariant(variantName);
            var theme = DesignTokens.Resolve(key, variant);

            var controlTheme = Assert.IsType<ControlTheme>(theme);

            Assert.Equal(typeof(Button), controlTheme.TargetType);
        }

        /// <summary>Each authoring class and the theme <c>Styles/Buttons.axaml</c> bridges it to.</summary>
        public static TheoryData<string, string, string> BridgedClasses()
        {
            var cases = new TheoryData<string, string, string>();

            foreach (var (className, key) in new[]
            {
                ("primaryAction", "PrimaryActionButton"),
                ("secondary", "SecondaryButton"),
                ("eject", "EjectButton"),
                ("discard", "DiscardButton"),
                ("ghost", "GhostButton"),
                ("link", "LinkButton"),
                ("rowButton", "RowButton")
            })
            {
                cases.Add(className, key, "Dark");
                cases.Add(className, key, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(BridgedClasses))]
        public void EveryAuthoringClass_BridgesToItsControlTheme(string className, string key, string variantName)
        {
            // The bridge is the app's authoring API: a view writes Classes="primaryAction" and never
            // names a theme. A class with no bridge behind it fails silently — the button keeps
            // Fluent's template and merely looks wrong.
            var variant = ToVariant(variantName);
            var button = new Button { Classes = { className } };

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.Same(DesignTokens.Resolve(key, variant), button.Theme);

            // Contract 3: our ring replaces Fluent's adorner rather than doubling up with it.
            Assert.Null(button.FocusAdorner);
        }

        /// <summary>
        /// The state matrix at the glass: theme, state, and the token the face is supposed to end up
        /// painted with. A <c>null</c> expectation means "no face of its own" — the window's canvas
        /// shows through.
        /// </summary>
        public static TheoryData<string, string, string?> ButtonStates()
        {
            return new TheoryData<string, string, string?>
            {
                { "primaryAction", "rest", "AccentBrush" },
                { "primaryAction", "hover", "AccentButtonBackgroundPointerOver" },
                { "primaryAction", "pressed", "AccentButtonBackgroundPressed" },
                { "primaryAction", "disabled", "SurfaceInsetBrush" },

                { "secondary", "rest", "SurfaceBarBrush" },
                { "secondary", "hover", "SurfaceRaisedBrush" },
                { "secondary", "pressed", "SurfaceLineBrush" },
                { "secondary", "disabled", "SurfaceInsetBrush" },

                { "eject", "rest", "StatusOkTintBrush" },
                { "eject", "hover", "StatusOkTintBorderBrush" },
                { "eject", "pressed", "StatusOkTintBorderBrush" },
                { "eject", "disabled", "SurfaceInsetBrush" },

                { "discard", "rest", "StatusErrorTintBrush" },
                { "discard", "hover", "StatusErrorTintBorderBrush" },
                { "discard", "pressed", "StatusErrorTintBorderBrush" },
                { "discard", "disabled", "SurfaceInsetBrush" },

                { "ghost", "rest", null },
                { "ghost", "hover", "SurfaceRaisedBrush" },
                { "ghost", "pressed", "SurfaceLineBrush" },
                { "ghost", "disabled", null },

                // A link is text, so every one of its states is a foreground move and the face
                // stays the canvas throughout; the ramp itself is pinned by
                // ALinksStates_MoveItsLabelBecauseItHasNoFaceToMove below.
                { "link", "rest", null },
                { "link", "hover", null },
                { "link", "pressed", null },
                { "link", "disabled", null },

                { "rowButton", "rest", null },
                { "rowButton", "hover", "SurfaceKeySelectedBrush" },
                { "rowButton", "pressed", "SurfaceRaisedBrush" },
                { "rowButton", "selected", "AccentSelectionFillBrush" },
                { "rowButton", "disabled", null }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(ButtonStates))]
        public void TheButtonStateMatrix_PaintsItsTokenInDark(string className, string state, string? expectedKey)
        {
            AssertFacePaints(className, state, expectedKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(ButtonStates))]
        public void TheButtonStateMatrix_PaintsItsTokenInLight(string className, string state, string? expectedKey)
        {
            AssertFacePaints(className, state, expectedKey, ThemeVariant.Light);
        }

        [AvaloniaTheory]
        [MemberData(nameof(BridgedClasses))]
        public void KeyboardFocus_PaintsTheHalo(string className, string key, string variantName)
        {
            // "Ring is 1px accent border + 3px 28% halo, never an outline offset" (2b). Tab and the
            // arrow keys summon it, which is exactly Avalonia's :focus-visible.
            //
            // Every one of the seven, not a representative: the ring is BaseButton's and each theme
            // is free to declare a BorderBrush of its own, so "deriving was enough" is a claim about
            // each ramp separately. The cases are shared with the bridge theory above, so the two
            // can never disagree about what the app's button roles are.
            var variant = ToVariant(variantName);
            var button = Sized(new Button { Classes = { className }, Content = "Home" });

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.Same(DesignTokens.Resolve(key, variant), button.Theme);
            Assert.True(button.Focus(NavigationMethod.Tab), "The button refused keyboard focus.");

            Assert.Contains(":focus-visible", button.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), button.BorderBrush);

            var shadows = RootOf(button).BoxShadow;

            Assert.Equal(1, shadows.Count);

            var shadow = shadows[0];

            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), shadow.Color);
            Assert.Equal(3, shadow.Spread);
            Assert.Equal(0, shadow.OffsetX);
            Assert.Equal(0, shadow.OffsetY);

            // And at the glass, two pixels outside the button's own edge: the halo has to be there,
            // not merely configured. A BoxShadow is clipped away by a ClipToBounds anywhere up the
            // chain, which is the failure this catches.
            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentFocusHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                PixelBesideTheButton(host, button));
        }

        [AvaloniaTheory]
        [MemberData(nameof(BridgedClasses))]
        public void APointerPress_DoesNotPaintTheHalo(string className, string key, string variantName)
        {
            // The other half of the rule: "mouse clicks suppress it". Nothing tracks the input
            // source by hand — NavigationMethod.Pointer simply does not raise :focus-visible.
            var variant = ToVariant(variantName);
            var button = Sized(new Button { Classes = { className }, Content = "Home" });

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.Same(DesignTokens.Resolve(key, variant), button.Theme);
            Assert.True(button.Focus(NavigationMethod.Pointer), "The button refused pointer focus.");

            Assert.Contains(":focus", button.Classes);
            Assert.DoesNotContain(":focus-visible", button.Classes);
            Assert.Equal(0, RootOf(button).BoxShadow.Count);

            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                PixelBesideTheButton(host, button));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ARealPointerPress_LeavesTheRingOffWhereARealTabKeyTurnsItOn(string variantName)
        {
            // The suppression rule driven through Avalonia's own input pipeline rather than through
            // a NavigationMethod hint. The theory above asserts the *consequence* of a press — focus
            // acquired with NavigationMethod.Pointer paints no halo — and takes on trust that a
            // press is what produces that navigation method. This asserts the whole chain: a real
            // button-down at the button's own coordinates, and a real Tab key, on one window.
            //
            // It is the acceptance criterion's exact wording ("suppressed on pointer press"), and it
            // is the half no amount of pseudo-class poking can reach.
            var variant = ToVariant(variantName);
            var clicked = Sized(new Button { Classes = { "secondary" }, Content = "Home" });
            var tabbed = Sized(new Button { Classes = { "secondary" }, Content = "Scan" });
            var stack = new StackPanel { Children = { clicked, tabbed } };

            using var host = ThemedHost.Show(stack, variant, HostWidth, HostHeight * 2);

            var centre = clicked.TranslatePoint(new Point(clicked.Bounds.Width / 2, clicked.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The button is not in the window's visual tree.");

            host.Window.MouseMove(centre);
            host.Window.MouseDown(centre, MouseButton.Left);
            host.Window.MouseUp(centre, MouseButton.Left);

            Dispatcher.UIThread.RunJobs();

            Assert.True(clicked.IsFocused, "The press did not focus the button it landed on.");
            Assert.Contains(":focus", clicked.Classes);
            Assert.DoesNotContain(":focus-visible", clicked.Classes);
            Assert.Equal(0, RootOf(clicked).BoxShadow.Count);

            // ...and the very next Tab, from the same window, brings the ring back on the button it
            // moves to. Same session, so this cannot pass by the focus system never having worked.
            host.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

            Dispatcher.UIThread.RunJobs();

            Assert.True(tabbed.IsFocused, "Tab did not move focus to the next button.");
            Assert.Contains(":focus-visible", tabbed.Classes);
            Assert.Equal(1, RootOf(tabbed).BoxShadow.Count);

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentFocusHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                PixelBesideTheButton(host, tabbed));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ALinksStates_MoveItsLabelBecauseItHasNoFaceToMove(string variantName)
        {
            // The one ramp the face matrix cannot describe: a link is text on the canvas in every
            // state, so all four of its rows in ButtonStates() expect the same nothing. The accent
            // walk is the whole of what the theme does, and this is the only place it is pinned.
            var variant = ToVariant(variantName);

            AssertLabelPaints("rest", "AccentBrush", variant);
            AssertLabelPaints("hover", "AccentLinkHoverBrush", variant);
            AssertLabelPaints("pressed", "AccentSelectedRingBrush", variant);
            AssertLabelPaints("disabled", "TextDisabledBrush", variant);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ASelectedRow_KeepsItsOwnRing(string variantName)
        {
            // Selection has its own border role — the design calls AccentSelectedRing the
            // "selected-row inset ring" — so a selected row is legible without any focus at all.
            var variant = ToVariant(variantName);
            var button = Sized(new Button { Classes = { "rowButton", "selected" } });

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("AccentSelectedRingBrush", variant), button.BorderBrush);
            Assert.Equal(0, RootOf(button).BoxShadow.Count);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void SelectionAndFocus_CoexistAndStayDistinguishable(string variantName)
        {
            // Contract 4, and the one place the precedence rule earns its keep: the filled face is
            // selection's, the border and the halo are focus's, and neither erases the other.
            var variant = ToVariant(variantName);
            var button = Sized(new Button { Classes = { "rowButton", "selected" } });

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.True(button.Focus(NavigationMethod.Tab), "The row refused keyboard focus.");

            Assert.Equal(DesignTokens.Resolve("AccentSelectionFillBrush", variant), button.Background);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), button.BorderBrush);
            Assert.Equal(1, RootOf(button).BoxShadow.Count);

            var frame = host.Capture();

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentSelectionFillBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
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
            var button = Sized(new Button { Classes = { "secondary" }, Content = "Home" });

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            Assert.True(button.Focus(NavigationMethod.Tab), "The button refused keyboard focus.");

            SetPseudoClasses(button, ":pointerover");

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), button.BorderBrush);

            // The fill is still the hover fill: focus takes the border, not the face.
            Assert.Equal(DesignTokens.Resolve("SurfaceRaisedBrush", variant), button.Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheHatchBrush_IsATileDrawnInATokenColour(string variantName)
        {
            var variant = ToVariant(variantName);
            var brush = Assert.IsType<DrawingBrush>(DesignTokens.Resolve("HatchBrush", variant));

            Assert.Equal(TileMode.Tile, brush.TileMode);

            var drawing = Assert.IsType<GeometryDrawing>(brush.Drawing);
            var painted = Assert.IsAssignableFrom<ISolidColorBrush>(drawing.Brush);

            // A role, not a literal — and the role has to be the one for this variant, which is why
            // the brush is declared inside the theme dictionaries rather than once at the top.
            Assert.Equal(DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", variant), painted.Color);

            // The tile is the design's 4px hatch pitch, and the source and destination rects match,
            // so the drawing maps onto it one to one at any control size.
            Assert.Equal(new Rect(0, 0, 4, 4), brush.SourceRect.Rect);
            Assert.Equal(new Rect(0, 0, 4, 4), brush.DestinationRect.Rect);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheHatchBrush_TilesRatherThanFillingFlat(string variantName)
        {
            // "Off is hatched, never black" — so the fill has to actually be stripes. A DrawingBrush
            // whose geometry misses its tile draws one stripe and leaves the rest flat, which reads
            // as a plain fill and would pass every resource test.
            var variant = ToVariant(variantName);
            var hatched = new Border
            {
                Background = (IBrush)DesignTokens.Resolve("HatchBrush", variant),
                Width = HatchProbeSize,
                Height = HatchProbeSize,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            using var host = ThemedHost.Show(hatched, variant, HostWidth, HostHeight);

            var frame = host.Capture();
            var origin = hatched.TranslatePoint(new Point(0, 0), host.Window)
                ?? throw new InvalidOperationException("The hatched border is not in the window's tree.");

            var stripe = DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", variant);
            var gap = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant);
            var row = (int)origin.Y + (HatchProbeSize / 2);
            var onStripe = new List<bool>();

            // One pixel in from each edge, so no sample lands on the border's own rounding.
            for (var offset = 1; offset < HatchProbeSize - 1; offset++)
            {
                var painted = FramePixels.At(frame, (int)origin.X + offset, row);

                onStripe.Add(Distance(painted, stripe) < Distance(painted, gap));
            }

            Assert.Contains(true, onStripe);
            Assert.Contains(false, onStripe);

            var alternations = onStripe.Zip(onStripe.Skip(1)).Count(pair => pair.First != pair.Second);

            // 46 sampled pixels at a 4px pitch is a dozen stripes; anything under eight edges is a
            // brush that drew once and stopped.
            Assert.True(alternations >= 8, $"The hatch crossed a stripe edge only {alternations} times; it is not tiling.");
        }

        [AvaloniaFact]
        public void AThemedButtonsContent_IsStillStyled()
        {
            // Our template names its presenter `Label` rather than `PART_ContentPresenter`, so the
            // ContentControl no longer registers it and the content hangs off the presenter in the
            // logical tree. That is deliberate, and this is the thing it could plausibly have broken:
            // the app's own type scale still has to reach a row's nested labels.
            var label = new TextBlock { Classes = { "monoValue" }, Text = "{esc}" };
            var button = new Button { Classes = { "rowButton" }, Content = label };

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("FontSizeMonoValue", ThemeVariant.Dark), label.FontSize);
            Assert.True(label.Bounds.Width > 0, "The row's content never got laid out.");
        }

        [AvaloniaFact]
        public void TheBridgeStyles_NameNoTemplatePart()
        {
            // The point of the whole layer: Styles/ describes roles, Themes/ControlThemes/ owns
            // templates. A `/template/` selector here would mean somebody reached into a theme they
            // do not own again.
            var bridge = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Styles/Buttons.axaml"]);

            Assert.DoesNotContain("/template/", bridge, StringComparison.Ordinal);
            Assert.DoesNotContain("PART_", bridge, StringComparison.Ordinal);
        }

        /// <summary>The label colour a <c>link</c> ends up with in <paramref name="state"/>.</summary>
        private static void AssertLabelPaints(string state, string expectedKey, ThemeVariant variant)
        {
            var button = Sized(new Button { Classes = { "link" }, Content = "Which board do I have?" });

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            RaiseState(button, state);

            Assert.Equal(DesignTokens.Resolve(expectedKey, variant), button.Foreground);
        }

        private static void AssertFacePaints(string className, string state, string? expectedKey, ThemeVariant variant)
        {
            var button = Sized(new Button { Classes = { className } });

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            RaiseState(button, state);

            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant);
            var expected = expectedKey is null
                ? canvas
                : Composite(DesignTokens.ResolveBrushColor(expectedKey, variant), canvas);

            var frame = host.Capture();
            var painted = FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2);

            AssertClose(expected, painted);
        }

        /// <summary>
        /// Puts <paramref name="button"/> into <paramref name="state"/>. Called after
        /// <see cref="ThemedHost.Show"/>, never before: Button re-syncs <c>:pressed</c> from
        /// IsPressed when its template is applied, so a pressed state raised beforehand is wiped by
        /// the first layout pass.
        /// </summary>
        private static void RaiseState(Button button, string state)
        {
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
        }

        /// <summary>The <c>Border#Root</c> of a themed button's own template.</summary>
        private static Border RootOf(Button button)
        {
            return button.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "Root");
        }

        /// <summary>
        /// The colour two pixels outside the button's left edge, which is inside the 3px halo when
        /// there is one and the window's canvas when there is not.
        /// </summary>
        private static Color PixelBesideTheButton(ThemedHost host, Button button)
        {
            var frame = host.Capture();
            var probe = button.TranslatePoint(new Point(-2, button.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The button is not in the window's visual tree.");

            return FramePixels.At(frame, (int)probe.X, (int)probe.Y);
        }

        /// <summary>
        /// Raises pseudo-classes by hand. The headless session has no pointer, so <c>:pointerover</c>
        /// and <c>:pressed</c> are set through the same interface Avalonia's own input code uses.
        /// </summary>
        private static void SetPseudoClasses(Button button, params string[] pseudoClasses)
        {
            var classes = (IPseudoClasses)button.Classes;

            foreach (var pseudoClass in pseudoClasses)
            {
                classes.Set(pseudoClass, true);
            }
        }

        private static Button Sized(Button button)
        {
            button.Width = ButtonWidth;
            button.Height = ButtonHeight;
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            return button;
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
