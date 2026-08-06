using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Tests.Headless;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The pill family of <c>Themes/ControlThemes/Pills.axaml</c> — the status pill, the nav pill,
    /// the filter chip and the kbd chip.
    /// <para>
    /// Nothing in the app consumes three of the four yet, so every test here names the theme
    /// directly rather than going through an authoring class; the class-to-theme bridge is a later
    /// commit's, and asserting it now would only pin markup that does not exist. What is asserted
    /// is the part a bridge cannot fix: that each theme resolves under both variants, that its
    /// state matrix reaches the glass in the token the design names, and that focus and selection
    /// stay separable when both are true at once.
    /// </para>
    /// </summary>
    public class PillThemeTests
    {
        private const double HostWidth = 240;

        private const double HostHeight = 120;

        private const double PillWidth = 120;

        private const double PillHeight = 40;

        /// <summary>The transition members <see cref="ITransition"/> hides behind explicit implementation.</summary>
        private const string AnimatedPropertyName = "Property";

        /// <summary>Every theme this file declares, and the type it templates.</summary>
        public static TheoryData<string, string> PillThemesAndVariants()
        {
            var cases = new TheoryData<string, string>();

            foreach (var key in new[] { "StatusPill", "NavPill", "FilterChip", "KbdChip" })
            {
                cases.Add(key, "Dark");
                cases.Add(key, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(PillThemesAndVariants))]
        public void EveryPillTheme_ResolvesInBothVariants(string key, string variantName)
        {
            var variant = ToVariant(variantName);

            var theme = Assert.IsType<ControlTheme>(DesignTokens.Resolve(key, variant));

            Assert.Equal(TargetTypeOf(key), theme.TargetType);
        }

        [AvaloniaFact]
        public void ThePillThemes_NameNoTemplatePart()
        {
            // Contract 2. A `/template/` selector inside this file is correct — it targets the
            // template this file itself declares — but a `PART_` name is never correct: it is the
            // hook that let a style in Styles/ reach into somebody else's template.
            var pills = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Themes/ControlThemes/Pills.axaml"]);

            Assert.DoesNotContain("PART_", pills, StringComparison.Ordinal);
        }

        // ===== Status pill ==================================================================

        /// <summary>The four-state vocabulary of mockup 1a: a bound class, a face and a border.</summary>
        public static TheoryData<string, string, string> StatusVariants()
        {
            return new TheoryData<string, string, string>
            {
                { "ok", "StatusOkTintBrush", "StatusOkTintBorderBrush" },
                { "warning", "StatusAdvisoryTintBrush", "StatusAdvisoryTintBorderBrush" },
                { "error", "StatusErrorTintBrush", "StatusErrorTintBorderBrush" },
                { "demo", "StatusDemoTintBrush", "StatusDemoTintBorderBrush" }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(StatusVariants))]
        public void EachStatusVariant_PaintsItsOwnTintInDark(string className, string fillKey, string borderKey)
        {
            AssertStatusVariantPaints(className, fillKey, borderKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(StatusVariants))]
        public void EachStatusVariant_PaintsItsOwnTintInLight(string className, string fillKey, string borderKey)
        {
            AssertStatusVariantPaints(className, fillKey, borderKey, ThemeVariant.Light);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheFourStatusVariants_AreTellableApartAtTheGlass(string variantName)
        {
            // Four hues, four meanings, and the whole point is that a glance separates them. Tinted
            // fills composite onto whatever is behind them, so "each names a different token" is not
            // by itself proof that the painted pixels differ.
            var variant = ToVariant(variantName);
            var painted = new List<Color>();

            foreach (var className in new[] { "ok", "warning", "error", "demo" })
            {
                var pill = SizedBorder(StatusPill(variant, className));

                using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

                var frame = host.Capture();

                painted.Add(FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
            }

            for (var first = 0; first < painted.Count; first++)
            {
                for (var second = first + 1; second < painted.Count; second++)
                {
                    Assert.True(
                        Distance(painted[first], painted[second]) > 8,
                        $"Two status variants painted {painted[first]} and {painted[second]}.");
                }
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheStatusPill_RestsWithNoFaceOfItsOwn(string variantName)
        {
            // No severity bound yet, or a severity none of the four classes matches: the pill is an
            // outline on whatever it sits on rather than a fifth colour.
            var variant = ToVariant(variantName);
            var pill = SizedBorder(StatusPill(variant));

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            var frame = host.Capture();

            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));

            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), pill.BorderBrush);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheStatusPill_KeepsTheCrossFadeTheBudgetNames(string variantName)
        {
            // "Status changes cross-fade the chip's fill over 220 ms" (2b), and the reason the pill
            // is still a Border rather than a templated control: StatusFillTransitions names
            // Border.BackgroundProperty and Border.BorderBrushProperty, which are *different*
            // AvaloniaProperty instances from TemplatedControl's identically named ones. On anything
            // but a Border the set would bind to nothing and the fade would vanish in silence.
            var variant = ToVariant(variantName);
            var pill = SizedBorder(StatusPill(variant, "ok"));

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            var transitions = pill.Transitions;

            Assert.NotNull(transitions);

            // The bare alias, not StatusFillTransitionsFull: MotionResourceBinder re-points it for
            // reduce-motion, and a pill bound to the concrete set would ignore that.
            Assert.Same(DesignTokens.Resolve("StatusFillTransitions", variant), transitions);

            var animated = transitions.Select(AnimatedPropertyOf).ToArray();

            Assert.Contains(Border.BackgroundProperty, animated);
            Assert.Contains(Border.BorderBrushProperty, animated);
        }

        // ===== Nav pill and filter chip =====================================================

        /// <summary>
        /// The state matrix at the glass: theme, state, and the token the face ends up painted
        /// with. <c>null</c> means "no face of its own" — the window's canvas shows through.
        /// </summary>
        public static TheoryData<string, string, string?> PillStates()
        {
            return new TheoryData<string, string, string?>
            {
                // Chromeless until touched, so a strip of them reads as text.
                { "NavPill", "rest", null },
                { "NavPill", "hover", "SurfaceKeySelectedBrush" },
                { "NavPill", "pressed", "SurfaceLineBrush" },
                { "NavPill", "selected", "SurfaceRaisedBrush" },
                { "NavPill", "disabled", "SurfaceInsetBrush" },

                // The chip has a face at rest, and hover deliberately does not move it: the face is
                // the selected state's one signal.
                { "FilterChip", "rest", "SurfaceBarBrush" },
                { "FilterChip", "hover", "SurfaceBarBrush" },
                { "FilterChip", "pressed", "SurfaceLineBrush" },
                { "FilterChip", "selected", "SurfaceRaisedBrush" },
                { "FilterChip", "disabled", "SurfaceInsetBrush" }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(PillStates))]
        public void ThePillStateMatrix_PaintsItsTokenInDark(string key, string state, string? expectedKey)
        {
            AssertFacePaints(key, state, expectedKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(PillStates))]
        public void ThePillStateMatrix_PaintsItsTokenInLight(string key, string state, string? expectedKey)
        {
            AssertFacePaints(key, state, expectedKey, ThemeVariant.Light);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AHoveredFilterChip_MovesItsBorderAndLabelRatherThanItsFace(string variantName)
        {
            // The complement of the matrix above, which can only say the face did not move: hover
            // has to do *something*, and what it does is the emphasized border the surface table
            // names ("line-hi — hover/emphasized borders") plus a step up in the label.
            var variant = ToVariant(variantName);
            var chip = SizedButton(ThemedButton("FilterChip", variant));

            using var host = ThemedHost.Show(chip, variant, HostWidth, HostHeight);

            SetPseudoClasses(chip, ":pointerover");

            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", variant), chip.BorderBrush);
            Assert.Equal(DesignTokens.Resolve("TextSecondaryBrush", variant), chip.Foreground);
            Assert.Equal(DesignTokens.Resolve("SurfaceBarBrush", variant), chip.Background);
        }

        /// <summary>Both clickable pills, in both variants.</summary>
        public static TheoryData<string, string> ClickablePillsAndVariants()
        {
            var cases = new TheoryData<string, string>();

            foreach (var key in new[] { "NavPill", "FilterChip" })
            {
                cases.Add(key, "Dark");
                cases.Add(key, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(ClickablePillsAndVariants))]
        public void KeyboardFocus_PaintsTheHalo(string key, string variantName)
        {
            // Inherited from BaseButton rather than redeclared, so what this really proves is that
            // deriving was enough — no ramp here quietly took the border back off the ring.
            var variant = ToVariant(variantName);
            var pill = SizedButton(ThemedButton(key, variant), "Letters");

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            Assert.True(pill.Focus(NavigationMethod.Tab), "The pill refused keyboard focus.");

            Assert.Contains(":focus-visible", pill.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), pill.BorderBrush);

            var shadows = RootOf(pill).BoxShadow;

            Assert.Equal(1, shadows.Count);
            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), shadows[0].Color);
            Assert.Equal(3, shadows[0].Spread);

            // And at the glass, outside the pill's own edge: contract 6 — a ClipToBounds anywhere up
            // the chain erases a BoxShadow, and only a frame notices.
            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentFocusHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                PixelBesideThePill(host, pill));
        }

        [AvaloniaTheory]
        [MemberData(nameof(ClickablePillsAndVariants))]
        public void APointerPress_DoesNotPaintTheHalo(string key, string variantName)
        {
            // "Mouse clicks suppress it; arrow/Tab summon it." Nothing tracks the input source by
            // hand — NavigationMethod.Pointer simply does not raise :focus-visible.
            var variant = ToVariant(variantName);
            var pill = SizedButton(ThemedButton(key, variant), "Letters");

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            Assert.True(pill.Focus(NavigationMethod.Pointer), "The pill refused pointer focus.");

            Assert.Contains(":focus", pill.Classes);
            Assert.DoesNotContain(":focus-visible", pill.Classes);
            Assert.Equal(0, RootOf(pill).BoxShadow.Count);

            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                PixelBesideThePill(host, pill));
        }

        [AvaloniaTheory]
        [MemberData(nameof(ClickablePillsAndVariants))]
        public void SelectionAndFocus_CoexistAndStayDistinguishable(string key, string variantName)
        {
            // Contract 4. Selection owns the face, focus owns the border and the halo outside it, so
            // a pill that is both shows both and neither erases the other.
            // No caption: the assertion below reads the middle of the frame, which is the middle of
            // the pill, and a glyph there would be what got measured.
            var variant = ToVariant(variantName);
            var pill = SizedButton(ThemedButton(key, variant));

            pill.Classes.Add("selected");

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            Assert.True(pill.Focus(NavigationMethod.Tab), "The pill refused keyboard focus.");

            Assert.Equal(DesignTokens.Resolve("SurfaceRaisedBrush", variant), pill.Background);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), pill.BorderBrush);
            Assert.Equal(1, RootOf(pill).BoxShadow.Count);

            var frame = host.Capture();

            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceRaisedBrush", variant),
                FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
        }

        [AvaloniaTheory]
        [MemberData(nameof(ClickablePillsAndVariants))]
        public void ASelectedPillThatIsAlsoHovered_StaysSelected(string key, string variantName)
        {
            // Contract 5, first half: selection outranks hover, and it does so because every hover
            // setter carries `:not(.selected)` rather than because of where it sits in the file.
            var variant = ToVariant(variantName);
            var pill = SizedButton(ThemedButton(key, variant));

            pill.Classes.Add("selected");

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            SetPseudoClasses(pill, ":pointerover");

            Assert.Equal(DesignTokens.Resolve("SurfaceRaisedBrush", variant), pill.Background);
        }

        [AvaloniaTheory]
        [MemberData(nameof(ClickablePillsAndVariants))]
        public void ADisabledPillThatIsAlsoSelected_TakesTheDisabledFace(string key, string variantName)
        {
            // Contract 5, second half, and the trap it exists for: Avalonia walks BasedOn first, so
            // BaseButton's `:disabled` is applied BEFORE anything declared in these themes. Drop the
            // `:not(:disabled)` from the selected ramp and a dead pill keeps its live face while
            // taking only the dead label colour.
            var variant = ToVariant(variantName);
            var pill = SizedButton(ThemedButton(key, variant));

            pill.Classes.Add("selected");
            pill.IsEnabled = false;

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), pill.Background);
            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), pill.Foreground);
        }

        [AvaloniaTheory]
        [MemberData(nameof(ClickablePillsAndVariants))]
        public void EveryClickablePill_IsALozenge(string key, string variantName)
        {
            // Pills are RadiusPill, not RadiusControl — the one geometry that separates a pill from
            // the buttons it sits beside, and the one thing a copied ramp forgets.
            var variant = ToVariant(variantName);
            var pill = SizedButton(ThemedButton(key, variant));

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("RadiusPill", variant), pill.CornerRadius);

            // Contract 3: our ring replaces Fluent's adorner rather than doubling up with it.
            Assert.Null(pill.FocusAdorner);
        }

        // ===== Kbd chip =====================================================================

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheKbdChip_IsMonoOnALineHighBorder(string variantName)
        {
            // A legend prints a keystroke the user types verbatim, so it is mono; and it is the one
            // place the surface table names line-hi for a fill boundary rather than for a hover
            // ("line-hi — hover/emphasized borders, kbd chips").
            var variant = ToVariant(variantName);
            var label = new TextBlock { Text = "⌘F" };
            var chip = ThemedChip(variant, label);

            using var host = ThemedHost.Show(chip, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", variant), chip.BorderBrush);
            Assert.Equal(DesignTokens.Resolve("RadiusChip", variant), chip.CornerRadius);

            // The chip carries the type, not the call site: a Border could not, which is why this
            // one theme templates a ContentControl.
            Assert.Equal(DesignTokens.Resolve("FontMono", variant), label.FontFamily);
            Assert.Equal(DesignTokens.Resolve("FontSizeMonoValueSmall", variant), label.FontSize);
            Assert.True(label.Bounds.Width > 0, "The chip's legend never got laid out.");

            var frame = host.Capture();
            var origin = chip.TranslatePoint(new Point(2, chip.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The chip is not in the window's visual tree.");

            // Inside the chip's own padding, clear of the glyph: the opaque bar face.
            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceBarBrush", variant),
                FramePixels.At(frame, (int)origin.X, (int)origin.Y));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ADisabledKbdChip_GoesQuietWithoutLosingItsFace(string variantName)
        {
            // A legend is never disabled on its own; it inherits IsEnabled from the panel around it,
            // and when that panel goes dead the chip has to go with it rather than stay the
            // brightest thing on the surface.
            var variant = ToVariant(variantName);
            var chip = ThemedChip(variant, new TextBlock { Text = "Esc" });

            chip.IsEnabled = false;

            using var host = ThemedHost.Show(chip, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), chip.BorderBrush);
            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), chip.Foreground);
            Assert.Equal(DesignTokens.Resolve("SurfaceBarBrush", variant), chip.Background);
        }

        [AvaloniaFact]
        public void TheKbdChipTemplate_NamesItsRootTheWayTheContractSays()
        {
            // Contract 1 and 2 together: the root is a Border called `Root` that template-binds, so
            // a state setter on the control flows through to it and nothing has to reach into the
            // template from outside.
            var chip = ThemedChip(ThemeVariant.Dark, new TextBlock { Text = "Esc" });

            using var host = ThemedHost.Show(chip, ThemeVariant.Dark, HostWidth, HostHeight);

            var root = chip.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Root");

            Assert.Equal(DesignTokens.Resolve("SurfaceBarBrush", ThemeVariant.Dark), root.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", ThemeVariant.Dark), root.BorderBrush);
        }

        // ===== Helpers ======================================================================

        private static void AssertStatusVariantPaints(string className, string fillKey, string borderKey, ThemeVariant variant)
        {
            var pill = SizedBorder(StatusPill(variant, className));

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve(fillKey, variant), pill.Background);
            Assert.Equal(DesignTokens.Resolve(borderKey, variant), pill.BorderBrush);

            // The tints are translucent, so the expectation is the token composited over the
            // window's canvas rather than the token itself.
            var frame = host.Capture();

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor(fillKey, variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
        }

        private static void AssertFacePaints(string key, string state, string? expectedKey, ThemeVariant variant)
        {
            var pill = SizedButton(ThemedButton(key, variant));

            using var host = ThemedHost.Show(pill, variant, HostWidth, HostHeight);

            // After Show, not before: Button re-syncs `:pressed` from IsPressed when its template is
            // applied, so a pressed state raised beforehand is wiped by the first layout pass.
            switch (state)
            {
                case "rest":
                    break;
                case "hover":
                    SetPseudoClasses(pill, ":pointerover");
                    break;
                case "pressed":
                    SetPseudoClasses(pill, ":pointerover", ":pressed");
                    break;
                case "selected":
                    pill.Classes.Add("selected");
                    break;
                case "disabled":
                    pill.IsEnabled = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown pill state.");
            }

            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant);
            var expected = expectedKey is null
                ? canvas
                : Composite(DesignTokens.ResolveBrushColor(expectedKey, variant), canvas);

            var frame = host.Capture();

            AssertClose(expected, FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
        }

        /// <summary>The type each theme templates; a theme aimed at the wrong one never applies.</summary>
        private static Type TargetTypeOf(string key)
        {
            return key switch
            {
                "StatusPill" => typeof(Border),
                "KbdChip" => typeof(ContentControl),
                _ => typeof(Button)
            };
        }

        private static Border StatusPill(ThemeVariant variant, string? severityClass = null)
        {
            var pill = new Border { Theme = (ControlTheme)DesignTokens.Resolve("StatusPill", variant) };

            if (severityClass is not null)
            {
                pill.Classes.Add(severityClass);
            }

            return pill;
        }

        private static Button ThemedButton(string key, ThemeVariant variant)
        {
            return new Button { Theme = (ControlTheme)DesignTokens.Resolve(key, variant) };
        }

        private static ContentControl ThemedChip(ThemeVariant variant, Control content)
        {
            return new ContentControl
            {
                Theme = (ControlTheme)DesignTokens.Resolve("KbdChip", variant),
                Content = content,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        }

        private static Button SizedButton(Button pill, string? content = null)
        {
            pill.Content = content;
            pill.Width = PillWidth;
            pill.Height = PillHeight;
            pill.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            pill.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            return pill;
        }

        private static Border SizedBorder(Border pill)
        {
            pill.Width = PillWidth;
            pill.Height = PillHeight;
            pill.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            pill.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            return pill;
        }

        /// <summary>The <c>Border#Root</c> of a themed button's own template.</summary>
        private static Border RootOf(Button pill)
        {
            return pill.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Root");
        }

        /// <summary>
        /// The colour two pixels outside the pill's left edge: inside the 3px halo when there is
        /// one, and the window's canvas when there is not.
        /// </summary>
        private static Color PixelBesideThePill(ThemedHost host, Button pill)
        {
            var frame = host.Capture();
            var probe = pill.TranslatePoint(new Point(-2, pill.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The pill is not in the window's visual tree.");

            return FramePixels.At(frame, (int)probe.X, (int)probe.Y);
        }

        /// <summary>
        /// The property a transition animates. <see cref="ITransition"/> hides it behind an explicit
        /// implementation and the collection is untyped, so reflection is the only way in.
        /// </summary>
        private static AvaloniaProperty AnimatedPropertyOf(ITransition transition)
        {
            var property = transition.GetType().GetProperty(AnimatedPropertyName)
                ?? throw new InvalidOperationException($"{transition.GetType().Name} has no {AnimatedPropertyName}.");

            return (AvaloniaProperty)(property.GetValue(transition)
                ?? throw new InvalidOperationException($"{transition.GetType().Name}.{AnimatedPropertyName} is null."));
        }

        /// <summary>
        /// Raises pseudo-classes by hand. The headless session has no pointer, so <c>:pointerover</c>
        /// and <c>:pressed</c> are set through the same interface Avalonia's own input code uses.
        /// </summary>
        private static void SetPseudoClasses(Button pill, params string[] pseudoClasses)
        {
            var classes = (IPseudoClasses)pill.Classes;

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
