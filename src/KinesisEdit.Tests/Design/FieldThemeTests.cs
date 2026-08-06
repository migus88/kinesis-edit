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
    /// The field family — <c>Themes/ControlThemes/Fields.axaml</c>: the search box, the two value
    /// fields, the slider, the check box, the combo box and the selectable list row.
    /// <para>
    /// Three of those derive from FluentTheme's own theme rather than replacing its template,
    /// because <see cref="TextBox"/>, <see cref="ComboBox"/> and <see cref="Slider"/> each look a
    /// <c>PART_</c>-prefixed part up with a call that throws when it is missing, and the authoring
    /// contract forbids writing such a name. That makes these tests load-bearing in a way the button
    /// suite's are not: a setter aimed at the control can be silently outranked by a state setter
    /// Fluent aims at its own template part, so nearly everything below is asserted at the glass or
    /// on the property the frame was actually drawn from.
    /// </para>
    /// </summary>
    public class FieldThemeTests
    {
        private const double HostWidth = 320;

        private const double HostHeight = 160;

        private const double FieldWidth = 200;

        private const double FieldHeight = 40;

        /// <summary>Side of the check box's own box, as <c>Fields.axaml</c> sizes it.</summary>
        private const double CheckBoxSize = 16;

        /// <summary>Every theme this file declares, and the control type it templates.</summary>
        public static TheoryData<string, string, string> ThemesAndVariants()
        {
            var cases = new TheoryData<string, string, string>();

            foreach (var (key, targetType) in ThemeTargets)
            {
                cases.Add(key, targetType.FullName!, "Dark");
                cases.Add(key, targetType.FullName!, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(ThemesAndVariants))]
        public void EveryFieldTheme_ResolvesInBothVariants(string key, string targetTypeName, string variantName)
        {
            var variant = ToVariant(variantName);
            var theme = Assert.IsType<ControlTheme>(DesignTokens.Resolve(key, variant));

            Assert.Equal(targetTypeName, theme.TargetType?.FullName);
        }

        [AvaloniaTheory]
        [MemberData(nameof(ThemesAndVariants))]
        public void EveryFieldTheme_ReachesItsControl(string key, string targetTypeName, string variantName)
        {
            // A ControlTheme that resolves has still done nothing until it is applied: a TargetType
            // mismatch, or a `BasedOn` that failed to find FluentTheme's theme from inside
            // Application.Resources, both leave the control on its default template and merely
            // looking wrong.
            var variant = ToVariant(variantName);
            var control = Create(targetTypeName);

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(Sized(control), variant, HostWidth, HostHeight);

            Assert.Same(DesignTokens.Resolve(key, variant), control.Theme);
            Assert.True(control.Bounds.Width > 0, $"'{key}' never got laid out.");

            // Every field is a hole in the panel, cut to one of the design's radii — the control
            // radius for a field-sized thing, the tighter chip radius for the 16px check box, and
            // never a number invented at the call site.
            Assert.Equal(DesignTokens.Resolve(ThemeRadii[key], variant), CornerRadiusOf(control));
        }

        /// <summary>
        /// The face at the glass, for the three themes that own the pixel under the probe: theme,
        /// state, and the token the face has to end up painted with.
        /// </summary>
        public static TheoryData<string, string, string?> FaceStates()
        {
            return new TheoryData<string, string, string?>
            {
                { "TokenField", "rest", "SurfaceInsetBrush" },
                { "TokenField", "armed", "StatusAdvisoryTintBrush" },
                { "TokenField", "disabled", "SurfaceInsetBrush" },

                { "CheckBox", "rest", "SurfaceInsetBrush" },
                { "CheckBox", "hover", "SurfaceRaisedBrush" },
                { "CheckBox", "pressed", "SurfaceLineBrush" },
                { "CheckBox", "checked", "AccentBrush" },
                { "CheckBox", "disabled", "SurfaceInsetBrush" },

                { "SelectableListRow", "rest", null },
                { "SelectableListRow", "hover", "SurfaceKeySelectedBrush" },
                { "SelectableListRow", "pressed", "SurfaceRaisedBrush" },
                { "SelectableListRow", "selected", "AccentSelectionFillBrush" },
                { "SelectableListRow", "disabled", null }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(FaceStates))]
        public void TheFieldStateMatrix_PaintsItsTokenInDark(string key, string state, string? expectedKey)
        {
            AssertFacePaints(key, state, expectedKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(FaceStates))]
        public void TheFieldStateMatrix_PaintsItsTokenInLight(string key, string state, string? expectedKey)
        {
            AssertFacePaints(key, state, expectedKey, ThemeVariant.Light);
        }

        /// <summary>
        /// The border ramp of the four themes whose face never changes — a field is inset in the
        /// panel and says everything it has to say with its outline.
        /// </summary>
        public static TheoryData<string, string, string> BorderStates()
        {
            return new TheoryData<string, string, string>
            {
                { "SearchField", "rest", "SurfaceLineBrush" },
                { "SearchField", "hover", "SurfaceLineHighBrush" },
                { "SearchField", "disabled", "SurfaceLineBrush" },

                { "MonoValueField", "rest", "SurfaceLineBrush" },
                { "MonoValueField", "hover", "SurfaceLineHighBrush" },
                { "MonoValueField", "disabled", "SurfaceLineBrush" },

                { "TokenField", "rest", "SurfaceLineBrush" },
                { "TokenField", "hover", "SurfaceLineHighBrush" },
                { "TokenField", "armed", "StatusAdvisoryBrush" },

                { "ComboBox", "rest", "SurfaceLineBrush" },
                { "ComboBox", "hover", "SurfaceLineHighBrush" },
                { "ComboBox", "disabled", "SurfaceLineBrush" },

                { "Slider", "hover", "SurfaceLineBrush" }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(BorderStates))]
        public void TheFieldBorderMatrix_PaintsItsTokenInDark(string key, string state, string expectedKey)
        {
            AssertBorderPaints(key, state, expectedKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(BorderStates))]
        public void TheFieldBorderMatrix_PaintsItsTokenInLight(string key, string state, string expectedKey)
        {
            AssertBorderPaints(key, state, expectedKey, ThemeVariant.Light);
        }

        /// <summary>Every theme whose control can take focus, and how its ring is reached.</summary>
        public static TheoryData<string, string> FocusableThemes()
        {
            var cases = new TheoryData<string, string>();

            foreach (var key in new[] { "SearchField", "MonoValueField", "ComboBox", "Slider", "CheckBox", "SelectableListRow" })
            {
                cases.Add(key, "Dark");
                cases.Add(key, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(FocusableThemes))]
        public void KeyboardFocus_PaintsTheAccentBorderAtTheGlass(string key, string variantName)
        {
            // "Ring is 1px accent border + 3px 28% halo, never an outline offset" (2b). The halo
            // half is only reachable on the two themes that own their template — see the header of
            // Fields.axaml — but the accent border is the ring's minimum and every field has it.
            var variant = ToVariant(variantName);
            var control = Sized(Create(ThemeTargets[key].FullName!));

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(control, variant, HostWidth, HostHeight);

            Assert.True(control.Focus(NavigationMethod.Tab), $"'{key}' refused keyboard focus.");

            AssertClose(
                DesignTokens.ResolveBrushColor("AccentBrush", variant),
                BorderPixel(host, RingOf(control)));
        }

        [AvaloniaTheory]
        [InlineData("SearchField", "Dark")]
        [InlineData("SearchField", "Light")]
        [InlineData("MonoValueField", "Dark")]
        [InlineData("MonoValueField", "Light")]
        [InlineData("ComboBox", "Dark")]
        [InlineData("ComboBox", "Light")]
        public void PointerFocusOfATextField_StillShowsTheAccentBorder(string key, string variantName)
        {
            // A text field is not a button. A button clicked needs no ring — the click was its own
            // confirmation — but a field clicked into is where the caret now is and the user is
            // about to type, so its accent border rides `:focus` rather than `:focus-visible`.
            var variant = ToVariant(variantName);
            var control = Sized(Create(ThemeTargets[key].FullName!));

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(control, variant, HostWidth, HostHeight);

            Assert.True(control.Focus(NavigationMethod.Pointer), $"'{key}' refused pointer focus.");

            Assert.DoesNotContain(":focus-visible", control.Classes);

            AssertClose(
                DesignTokens.ResolveBrushColor("AccentBrush", variant),
                BorderPixel(host, RingOf(control)));
        }

        [AvaloniaTheory]
        [InlineData("CheckBox", "Dark")]
        [InlineData("CheckBox", "Light")]
        [InlineData("SelectableListRow", "Dark")]
        [InlineData("SelectableListRow", "Light")]
        public void KeyboardFocus_PaintsTheHaloOnTheThemesThatOwnTheirTemplate(string key, string variantName)
        {
            var variant = ToVariant(variantName);
            var control = Sized(Create(ThemeTargets[key].FullName!));

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(control, variant, HostWidth, HostHeight);

            Assert.True(control.Focus(NavigationMethod.Tab), $"'{key}' refused keyboard focus.");
            Assert.Contains(":focus-visible", control.Classes);

            var root = RootOf(control);
            var shadows = root.BoxShadow;

            Assert.Equal(1, shadows.Count);

            var shadow = shadows[0];

            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), shadow.Color);
            Assert.Equal(3, shadow.Spread);
            Assert.Equal(0, shadow.OffsetX);
            Assert.Equal(0, shadow.OffsetY);

            // And at the glass, two pixels outside the ringed part's own edge: a BoxShadow is erased
            // by a ClipToBounds anywhere up the chain, which is what this catches.
            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentFocusHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                PixelBeside(host, root));
        }

        [AvaloniaTheory]
        [InlineData("CheckBox", "Dark")]
        [InlineData("CheckBox", "Light")]
        [InlineData("SelectableListRow", "Dark")]
        [InlineData("SelectableListRow", "Light")]
        public void PointerFocusOfAButtonLikeControl_DoesNotPaintTheHalo(string key, string variantName)
        {
            // The other half of the rule: "mouse clicks suppress it". Nothing tracks the input
            // source by hand — NavigationMethod.Pointer simply does not raise :focus-visible.
            var variant = ToVariant(variantName);
            var control = Sized(Create(ThemeTargets[key].FullName!));

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(control, variant, HostWidth, HostHeight);

            Assert.True(control.Focus(NavigationMethod.Pointer), $"'{key}' refused pointer focus.");

            Assert.Contains(":focus", control.Classes);
            Assert.DoesNotContain(":focus-visible", control.Classes);

            var root = RootOf(control);

            Assert.Equal(0, root.BoxShadow.Count);

            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                PixelBeside(host, root));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ASelectedRow_DrawsTheInsetRing(string variantName)
        {
            // The handoff names AccentSelectedRing the "selected-row inset ring", and this is the
            // row it names it for. It is the Border's own 1px border, which a Border draws inside
            // its bounds — the same mark an `inset` box-shadow would make, in a colour that is a
            // token rather than a literal.
            var variant = ToVariant(variantName);
            var row = Sized(new ListBoxItem { Content = "[esc]" });

            row.Theme = (ControlTheme)DesignTokens.Resolve("SelectableListRow", variant);

            using var host = ThemedHost.Show(row, variant, HostWidth, HostHeight);

            SetPseudoClasses(row, ":selected");

            Assert.Equal(DesignTokens.Resolve("AccentSelectedRingBrush", variant), row.BorderBrush);
            Assert.Equal(0, RootOf(row).BoxShadow.Count);

            AssertClose(
                DesignTokens.ResolveBrushColor("AccentSelectedRingBrush", variant),
                BorderPixel(host, row));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void SelectionAndFocus_CoexistAndStayDistinguishable(string variantName)
        {
            // Contract 4. The filled face is selection's, the border and the halo are focus's, and
            // neither erases the other — which is only true because the selected BorderBrush setter
            // carries `:not(:focus-visible)` rather than trusting the order of the file.
            var variant = ToVariant(variantName);
            var row = Sized(new ListBoxItem { Content = "[esc]" });

            row.Theme = (ControlTheme)DesignTokens.Resolve("SelectableListRow", variant);

            using var host = ThemedHost.Show(row, variant, HostWidth, HostHeight);

            SetPseudoClasses(row, ":selected");

            Assert.True(row.Focus(NavigationMethod.Tab), "The row refused keyboard focus.");

            Assert.Equal(DesignTokens.Resolve("AccentSelectionFillBrush", variant), row.Background);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), row.BorderBrush);
            Assert.Equal(1, RootOf(row).BoxShadow.Count);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheSearchField_DrawsItsLeadingGlyph(string variantName)
        {
            // The mockups spell it `⌕`, and neither embedded family carries U+2315, so it is drawn
            // as geometry on the design's own icon grid instead. Whichever it is, it has to be there
            // and it has to be inside the field: a search box with no mark is just a text box.
            var variant = ToVariant(variantName);
            var field = Sized(new TextBox());

            field.Theme = (ControlTheme)DesignTokens.Resolve("SearchField", variant);

            using var host = ThemedHost.Show(field, variant, HostWidth, HostHeight);

            var glyph = GlyphOf(field);

            Assert.True(glyph.Bounds.Width > 0 && glyph.Bounds.Height > 0, "The search glyph never got laid out.");
            Assert.Equal(DesignTokens.Resolve("TextMutedBrush", variant), glyph.Stroke);
            Assert.Equal(DesignTokens.Resolve("IconStrokeThickness", variant), glyph.StrokeThickness);

            // Inside the field's own bounds, and left of centre: it is the *leading* glyph.
            var origin = glyph.TranslatePoint(new Point(0, 0), field)
                ?? throw new InvalidOperationException("The glyph is not inside the field.");

            Assert.True(origin.X >= 0, "The glyph sits outside the field's left edge.");
            Assert.True(origin.X < field.Bounds.Width / 2, "The glyph is not leading the field.");
        }

        [AvaloniaFact]
        public void EverySearchField_GetsAGlyphOfItsOwn()
        {
            // A Setter holding a bare control hands the SAME instance to every control it matches,
            // and a control has one parent — so the second search field in a window would silently
            // steal the first one's glyph. The `<Template>` wrapper is what makes each field build
            // its own; without it this is the test that fails.
            var first = Sized(new TextBox());
            var second = Sized(new TextBox());

            first.Theme = (ControlTheme)DesignTokens.Resolve("SearchField", ThemeVariant.Dark);
            second.Theme = (ControlTheme)DesignTokens.Resolve("SearchField", ThemeVariant.Dark);

            var stack = new StackPanel { Children = { first, second } };

            using var host = ThemedHost.Show(stack, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.NotSame(GlyphOf(first), GlyphOf(second));
            Assert.True(GlyphOf(first).Bounds.Width > 0, "The first field's glyph never got laid out.");
            Assert.True(GlyphOf(second).Bounds.Width > 0, "The second field's glyph never got laid out.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheMonoValueField_IsMonoAndTheSearchFieldIsNot(string variantName)
        {
            // "Mono type means this is literally a value in a config file" (docs/design/README.md).
            // A delay in milliseconds is; a search query the user is typing is not, and the two
            // themes exist separately for exactly that reason.
            var variant = ToVariant(variantName);
            var value = Sized(new TextBox());
            var search = Sized(new TextBox());

            value.Theme = (ControlTheme)DesignTokens.Resolve("MonoValueField", variant);
            search.Theme = (ControlTheme)DesignTokens.Resolve("SearchField", variant);

            using var host = ThemedHost.Show(new StackPanel { Children = { value, search } }, variant, HostWidth, HostHeight);

            var mono = (FontFamily)DesignTokens.Resolve("FontMono", variant);

            Assert.Equal(mono, value.FontFamily);
            Assert.Equal(DesignTokens.Resolve("FontSizeMonoValue", variant), value.FontSize);

            Assert.NotEqual(mono, search.FontFamily);
            Assert.Equal(DesignTokens.Resolve("FontSans", variant), search.FontFamily);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheCheckBoxGlyph_AppearsOnlyWhenItIsChecked(string variantName)
        {
            // The box is the whole control's state vocabulary, so the mark has to actually switch —
            // a glyph left visible under an unchecked face reads as checked no matter what the fill
            // says.
            var variant = ToVariant(variantName);
            var box = Sized(new CheckBox { Content = "Warn before leaving" });

            box.Theme = (ControlTheme)DesignTokens.Resolve("CheckBox", variant);

            using var host = ThemedHost.Show(box, variant, HostWidth, HostHeight);

            Assert.False(GlyphOf(box).IsVisible, "The unchecked box drew a mark.");

            box.IsChecked = true;

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(GlyphOf(box).IsVisible, "The checked box drew no mark.");
            Assert.Equal(DesignTokens.Resolve("AccentTextBrush", variant), GlyphOf(box).Stroke);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheIndeterminateCheckBox_DrawsADashOnTheAccentFace(string variantName)
        {
            // The checked and indeterminate faces are declared in one comma-separated selector, so
            // this is what proves the second half of it is live: a selector list that Avalonia only
            // half-applied would leave an indeterminate box unfilled and marked with a tick.
            var variant = ToVariant(variantName);
            var box = Sized(new CheckBox { Content = "Some layers only", IsThreeState = true, IsChecked = null });

            box.Theme = (ControlTheme)DesignTokens.Resolve("CheckBox", variant);

            using var host = ThemedHost.Show(box, variant, HostWidth, HostHeight);

            Assert.Contains(":indeterminate", box.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), box.Background);

            var glyph = GlyphOf(box);

            Assert.True(glyph.IsVisible, "The indeterminate box drew no mark.");

            // A dash, not a tick: one straight run, so the geometry is exactly as wide as it is long.
            Assert.NotNull(glyph.Data);
            Assert.Equal(0, glyph.Data!.Bounds.Height);
            Assert.True(glyph.Data.Bounds.Width > 0, "The indeterminate mark has no length.");
        }

        [AvaloniaFact]
        public void TheFieldThemes_NameNoFluentTemplatePart()
        {
            // The whole point of the layer: a theme owns its own template or extends somebody's
            // through properties, and never reaches into one it does not own. Three controls here
            // could not have a template of their own precisely because Fluent looks their parts up
            // by a `PART_` name — so this is the guard that the workaround stayed a workaround.
            var fields = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Themes/ControlThemes/Fields.axaml"]);

            Assert.DoesNotContain("PART_", fields, StringComparison.Ordinal);

            // Every `/template/` selector below names a part this file itself declares.
            foreach (var part in TemplateSelectorParts(fields))
            {
                Assert.Contains($"x:Name=\"{part}\"", fields, StringComparison.Ordinal);
            }
        }

        [AvaloniaFact]
        public void TheFieldThemes_HardcodeNoColour()
        {
            // "Never hardcode a hex in a view", and a control theme is where the temptation is
            // strongest: an inset selection ring and a focus halo both read naturally as literals.
            // Themes/Tokens.axaml is the only file in the app that may hold one. (A bare `#` is not
            // enough to look for — `/template/ Border#Root` is the correct way to name a part.)
            var fields = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Themes/ControlThemes/Fields.axaml"]);
            var literals = System.Text.RegularExpressions.Regex
                .Matches(fields, "=\"#[0-9A-Fa-f]{3,8}\"")
                .Select(match => match.Value)
                .ToArray();

            Assert.True(literals.Length == 0, string.Join(", ", literals));
        }

        /// <summary>Each theme key and the control type its <c>TargetType</c> has to name.</summary>
        private static readonly IReadOnlyDictionary<string, Type> ThemeTargets = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["SearchField"] = typeof(TextBox),
            ["MonoValueField"] = typeof(TextBox),
            ["TokenField"] = typeof(Border),
            ["Slider"] = typeof(Slider),
            ["CheckBox"] = typeof(CheckBox),
            ["ComboBox"] = typeof(ComboBox),
            ["SelectableListRow"] = typeof(ListBoxItem)
        };

        /// <summary>The radius token each theme is cut to.</summary>
        private static readonly IReadOnlyDictionary<string, string> ThemeRadii = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SearchField"] = "RadiusControl",
            ["MonoValueField"] = "RadiusControl",
            ["TokenField"] = "RadiusControl",
            ["Slider"] = "RadiusControl",
            ["CheckBox"] = "RadiusChip",
            ["ComboBox"] = "RadiusControl",
            ["SelectableListRow"] = "RadiusControl"
        };

        private static void AssertFacePaints(string key, string state, string? expectedKey, ThemeVariant variant)
        {
            var control = Sized(Create(ThemeTargets[key].FullName!));

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(control, variant, HostWidth, HostHeight);

            ApplyState(control, state);

            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant);
            var expected = expectedKey is null
                ? canvas
                : Composite(DesignTokens.ResolveBrushColor(expectedKey, variant), canvas);

            AssertClose(expected, FacePixel(host, FaceOf(control)));
        }

        private static void AssertBorderPaints(string key, string state, string expectedKey, ThemeVariant variant)
        {
            var control = Sized(Create(ThemeTargets[key].FullName!));

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(control, variant, HostWidth, HostHeight);

            ApplyState(control, state);

            AssertClose(
                DesignTokens.ResolveBrushColor(expectedKey, variant),
                BorderPixel(host, RingOf(control)));
        }

        /// <summary>
        /// Raises a state after the control is on screen, never before: a templated control re-syncs
        /// its own pseudo-classes when the template is applied, so anything set beforehand is wiped
        /// by the first layout pass.
        /// </summary>
        private static void ApplyState(Control control, string state)
        {
            switch (state)
            {
                case "rest":
                    break;
                case "hover":
                    SetPseudoClasses(control, ":pointerover");
                    break;
                case "pressed":
                    SetPseudoClasses(control, ":pointerover", ":pressed");
                    break;
                case "selected":
                    SetPseudoClasses(control, ":selected");
                    break;
                case "checked":
                    ((CheckBox)control).IsChecked = true;
                    break;
                case "armed":
                    control.Classes.Add("armed");
                    break;
                case "disabled":
                    control.IsEnabled = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown field state.");
            }

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        /// <summary>
        /// The part whose border carries the focus ring. It is the control itself everywhere except
        /// the check box, where the ring belongs on the box rather than around the label.
        /// </summary>
        private static Visual RingOf(Control control)
        {
            return control is CheckBox ? RootOf(control) : control;
        }

        /// <summary>
        /// The part whose fill the face probe should land on — the check box's own 16px box, and the
        /// control itself for everything else.
        /// </summary>
        private static Visual FaceOf(Control control)
        {
            return control is CheckBox ? RootOf(control) : control;
        }

        /// <summary>The <c>Border#Root</c> of a theme that writes its own template.</summary>
        private static Border RootOf(Control control)
        {
            return control.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "Root");
        }

        /// <summary>The stroked mark inside a search field or a check box.</summary>
        private static Avalonia.Controls.Shapes.Path GlyphOf(Control control)
        {
            return control.GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Path>()
                .Single(path => path.Classes.Contains("searchGlyph") || path.Name == "Glyph");
        }

        /// <summary>
        /// The colour of <paramref name="face"/>'s own fill. Sampled at the middle of a wide face,
        /// and 4px in from the top-left of the check box's 16px one — the centre of that box is
        /// where the mark is drawn, and a probe on the glyph reads the glyph's colour rather than
        /// the fill it sits on.
        /// </summary>
        private static Color FacePixel(ThemedHost host, Visual face)
        {
            var frame = host.Capture();
            var inside = face.Bounds.Width <= CheckBoxSize
                ? new Point(4, 4)
                : new Point(face.Bounds.Width / 2, face.Bounds.Height / 2);
            var probe = face.TranslatePoint(inside, host.Window)
                ?? throw new InvalidOperationException("The face is not in the window's visual tree.");

            return FramePixels.At(frame, (int)probe.X, (int)probe.Y);
        }

        /// <summary>
        /// The colour of the 1px border on <paramref name="ringed"/>'s left edge, sampled at its
        /// vertical middle so no probe lands on the corner rounding.
        /// </summary>
        private static Color BorderPixel(ThemedHost host, Visual ringed)
        {
            var frame = host.Capture();
            var probe = ringed.TranslatePoint(new Point(0, ringed.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The ringed part is not in the window's visual tree.");

            return FramePixels.At(frame, (int)probe.X, (int)probe.Y);
        }

        /// <summary>
        /// The colour two pixels outside <paramref name="root"/>'s left edge — inside the 3px halo
        /// when there is one, and the window's canvas when there is not.
        /// </summary>
        private static Color PixelBeside(ThemedHost host, Visual root)
        {
            var frame = host.Capture();
            var probe = root.TranslatePoint(new Point(-2, root.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The ringed part is not in the window's visual tree.");

            return FramePixels.At(frame, (int)probe.X, (int)probe.Y);
        }

        /// <summary>
        /// Raises pseudo-classes by hand. The headless session has no pointer and no selection
        /// owner, so <c>:pointerover</c>, <c>:pressed</c> and <c>:selected</c> are set through the
        /// same interface Avalonia's own code uses.
        /// </summary>
        private static void SetPseudoClasses(Control control, params string[] pseudoClasses)
        {
            var classes = (IPseudoClasses)control.Classes;

            foreach (var pseudoClass in pseudoClasses)
            {
                classes.Set(pseudoClass, true);
            }
        }

        /// <summary>Every part name a <c>/template/</c> selector in <paramref name="xaml"/> reaches for.</summary>
        private static IReadOnlyList<string> TemplateSelectorParts(string xaml)
        {
            return System.Text.RegularExpressions.Regex
                .Matches(xaml, @"/template/\s*\w+#(?<part>\w+)")
                .Select(match => match.Groups["part"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static Control Create(string targetTypeName)
        {
            return targetTypeName switch
            {
                _ when targetTypeName == typeof(TextBox).FullName => new TextBox(),
                _ when targetTypeName == typeof(Border).FullName => new Border(),
                _ when targetTypeName == typeof(Slider).FullName => new Slider { Minimum = 1, Maximum = 9, Value = 4 },
                _ when targetTypeName == typeof(CheckBox).FullName => new CheckBox { Content = "Warn before leaving" },
                _ when targetTypeName == typeof(ComboBox).FullName => new ComboBox { ItemsSource = new[] { "Top", "Fn" } },
                _ when targetTypeName == typeof(ListBoxItem).FullName => new ListBoxItem { Content = "[esc]" },
                _ => throw new ArgumentOutOfRangeException(nameof(targetTypeName), targetTypeName, "Unknown field target.")
            };
        }

        private static T Sized<T>(T control)
            where T : Control
        {
            control.Width = FieldWidth;
            control.Height = FieldHeight;
            control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            return control;
        }

        private static CornerRadius CornerRadiusOf(Control control)
        {
            return control switch
            {
                Border border => border.CornerRadius,
                TemplatedControl templated => templated.CornerRadius,
                _ => throw new ArgumentOutOfRangeException(nameof(control), control, "No corner radius on this control.")
            };
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
