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

        /// <summary>The setting switch's track, as <c>Fields.axaml</c> sizes it.</summary>
        private const double SwitchTrackWidth = 40;

        /// <inheritdoc cref="SwitchTrackWidth" />
        private const double SwitchTrackHeight = 20;

        /// <summary>
        /// Centre of the knob's off position inside the track: a 1px border, a 3px inset and half of
        /// the 14px knob. The two constants are what let a probe say "here is where the knob is" and
        /// "here is where it is not" without either landing on an edge.
        /// </summary>
        private const double KnobCentreOff = 11;

        /// <inheritdoc cref="KnobCentreOff" />
        private const double KnobCentreOn = SwitchTrackWidth - KnobCentreOff;

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
                { "TokenField", "disabled", "SurfaceLineBrush" },

                { "ComboBox", "rest", "SurfaceLineBrush" },
                { "ComboBox", "hover", "SurfaceLineHighBrush" },
                { "ComboBox", "pressed", "SurfaceLineHighBrush" },
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
        [InlineData("SearchField", "Dark")]
        [InlineData("SearchField", "Light")]
        [InlineData("MonoValueField", "Dark")]
        [InlineData("MonoValueField", "Light")]
        [InlineData("ComboBox", "Dark")]
        [InlineData("ComboBox", "Light")]
        [InlineData("Slider", "Dark")]
        [InlineData("Slider", "Light")]
        public void TheFluentDerivedFields_RingWithoutAHalo(string key, string variantName)
        {
            // THE DEVIATION, PINNED. Fields.axaml's header carries a table saying these four take
            // the accent border and no halo, because the halo is a BoxShadow, a BoxShadow can only
            // be set on a Border, and reaching Fluent's own `Border#PART_BorderElement` would be the
            // very `/template/` selector this issue exists to delete. It is documented in
            // docs/app/design-system.md as a deliberate deviation.
            //
            // A deviation nobody asserts is just an untested hole: this is what would notice if a
            // later commit either quietly grew the halo (making the table a lie) or lost the accent
            // border too (making the ring invisible, which the criterion does not allow). Both
            // halves are therefore asserted — the border IS there, the shadow is NOT, anywhere in
            // the control's tree rather than only on a part we could name.
            var variant = ToVariant(variantName);
            var control = Sized(Create(ThemeTargets[key].FullName!));

            control.Theme = (ControlTheme)DesignTokens.Resolve(key, variant);

            using var host = ThemedHost.Show(control, variant, HostWidth, HostHeight);

            Assert.True(control.Focus(NavigationMethod.Tab), $"'{key}' refused keyboard focus.");

            AssertClose(
                DesignTokens.ResolveBrushColor("AccentBrush", variant),
                BorderPixel(host, RingOf(control)));

            var shadowed = control.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.BoxShadow.Count > 0)
                .Select(border => border.Name ?? border.GetType().Name)
                .ToArray();

            Assert.True(shadowed.Length == 0, $"'{key}' grew a halo on: {string.Join(", ", shadowed)}.");

            // ...and the pixel just outside it is the canvas, not a ring: the border is where the
            // ring stops, which is the whole of what the table claims.
            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                PixelBeside(host, RingOf(control)));
        }

        [AvaloniaTheory]
        [InlineData("CheckBox", "Dark")]
        [InlineData("CheckBox", "Light")]
        [InlineData("SettingSwitch", "Dark")]
        [InlineData("SettingSwitch", "Light")]
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
        [InlineData("SettingSwitch", "Dark")]
        [InlineData("SettingSwitch", "Light")]
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
        public void AnArmedTokenField_DoublesItsBorder(string variantName)
        {
            // Fields.axaml: "The 2px thickness is what carries it at a glance across a panel of
            // three such fields." The theme promised it in a comment and the setter block delivered
            // 1px, because the state matrix pinned only the fill and the brush — a token field
            // waiting for a keystroke was therefore the same weight as one merely sitting there,
            // and the whole of "at a glance" was gone. The thickness is the assertion, so the
            // comment and the setters cannot drift apart again.
            var variant = ToVariant(variantName);
            var field = Sized(new Border());

            field.Theme = (ControlTheme)DesignTokens.Resolve("TokenField", variant);

            using var host = ThemedHost.Show(field, variant, HostWidth, HostHeight);

            Assert.Equal(new Thickness(1), field.BorderThickness);

            ApplyState(field, "armed");

            Assert.Equal(new Thickness(2), field.BorderThickness);

            // And at the glass: two full columns of the advisory hue, where a 1px border leaves the
            // second column showing the tint face behind it.
            var frame = host.Capture();
            var origin = field.TranslatePoint(new Point(0, field.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The field is not in the window's visual tree.");
            var advisory = DesignTokens.ResolveBrushColor("StatusAdvisoryBrush", variant);

            AssertClose(advisory, FramePixels.At(frame, (int)origin.X, (int)origin.Y));
            AssertClose(advisory, FramePixels.At(frame, (int)origin.X + 1, (int)origin.Y));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ASlidersFrame_IsThereToBeRingedAndNothingElse(string variantName)
        {
            // Fields.axaml gives the slider a border that is present-but-transparent at rest so that
            // acquiring one on hover or focus costs no reflow, and drops it entirely when disabled
            // rather than dimming it ("the track and thumb already state the disabled case ... a
            // second grey outline around them only adds noise"). Neither end of that is a colour a
            // pixel probe can read — transparent is whatever is behind it — so both are asserted on
            // the property the frame is drawn from, with the thickness that makes the claim mean
            // anything.
            var variant = ToVariant(variantName);
            var slider = Sized(new Slider());

            slider.Theme = (ControlTheme)DesignTokens.Resolve("Slider", variant);

            using var host = ThemedHost.Show(slider, variant, HostWidth, HostHeight);

            Assert.Equal(new Thickness(1), slider.BorderThickness);
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)slider.BorderBrush!).Color);

            ApplyState(slider, "hover");

            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), slider.BorderBrush);

            slider.IsEnabled = false;

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(new Thickness(1), slider.BorderThickness);
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)slider.BorderBrush!).Color);
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

        /// <summary>
        /// <b><c>ComposerValueField</c> is <c>MonoValueField</c> with exactly one role changed — the
        /// fill (issue #152)</b> — and the shared theme is left alone for its other consumers.
        /// <para>
        /// The macro compose bar's box is flush with the rail it sits on (both SurfaceInset), so this
        /// file's family rule — "a field is a hole in the panel, it rests on SurfaceInset" — would
        /// paint the field in the colour already under it. The mock separates that block by lifting
        /// what stands in it, so this one field comes up a step. Everything else it inherits, and the
        /// assertions below are what stop the derivative quietly becoming a second theme.
        /// </para>
        /// <para>
        /// <b>The disabled face is asserted on purpose:</b> Avalonia walks <c>BasedOn</c> first, so
        /// the base theme's <c>:disabled</c> fill would land on top — and disabled is not an edge
        /// case for this field, it is how the panel opens, before any step is selected.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheComposersField_IsMonoValueFieldWithOnlyItsFillChanged(string variantName)
        {
            var variant = ToVariant(variantName);
            var composer = Sized(new TextBox());
            var shared = Sized(new TextBox());

            composer.Theme = (ControlTheme)DesignTokens.Resolve("ComposerValueField", variant);
            shared.Theme = (ControlTheme)DesignTokens.Resolve("MonoValueField", variant);

            using var host = ThemedHost.Show(
                new StackPanel { Children = { composer, shared } },
                variant,
                HostWidth,
                HostHeight);

            // The one role that differs, in both directions: the composer's fill is the step above
            // the box, and the shared theme still paints the hole this file's header describes.
            Assert.Equal(DesignTokens.Resolve("SurfacePanelBrush", variant), composer.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), shared.Background);

            // Everything else is inherited, and that is the claim: mono family and size (the field
            // holds a value the macro file carries verbatim), the same hairline, the same geometry.
            Assert.Equal(shared.FontFamily, composer.FontFamily);
            Assert.Equal(DesignTokens.Resolve("FontMono", variant), composer.FontFamily);
            Assert.Equal(shared.FontSize, composer.FontSize);
            Assert.Equal(shared.Padding, composer.Padding);
            Assert.Equal(shared.MinHeight, composer.MinHeight);
            Assert.Equal(shared.CornerRadius, composer.CornerRadius);
            Assert.Equal(shared.BorderThickness, composer.BorderThickness);
            Assert.Equal(shared.BorderBrush, composer.BorderBrush);
            Assert.Equal(typeof(TextBox), composer.Theme!.TargetType);

            // ...and the dead face keeps the composer's fill rather than falling back through
            // BasedOn to the base theme's inset one.
            ApplyState(composer, "disabled");
            ApplyState(shared, "disabled");

            Assert.Equal(DesignTokens.Resolve("SurfacePanelBrush", variant), composer.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), shared.Background);
            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), composer.Foreground);
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

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheSettingSwitch_SaysItsValueWithTheKnobsPosition(string variantName)
        {
            // A switch has no mark to show or hide — where the knob IS is the value, which is the
            // whole reason the settings rows use one instead of a check box. Both halves are
            // asserted at the glass, because the position is an alignment setter carried by a
            // `/template/` selector and a layout pass, and neither is visible on the control.
            var variant = ToVariant(variantName);
            var toggle = Sized(new ToggleButton());

            toggle.Theme = (ControlTheme)DesignTokens.Resolve("SettingSwitch", variant);

            using var host = ThemedHost.Show(toggle, variant, HostWidth, HostHeight);

            var root = RootOf(toggle);
            var knob = KnobOf(toggle);

            Assert.Equal(new Size(SwitchTrackWidth, SwitchTrackHeight), root.Bounds.Size);
            Assert.Equal(DesignTokens.Resolve("TextMutedBrush", variant), knob.Fill);

            // Off: the knob is at the near end and the far end is bare track.
            AssertClose(DesignTokens.ResolveBrushColor("TextMutedBrush", variant), TrackPixel(host, root, KnobCentreOff));
            AssertClose(DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant), TrackPixel(host, root, KnobCentreOn));

            toggle.IsChecked = true;

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // On: it has crossed, the track is accent, and the knob is drawn in the on-accent colour
            // — reached through the AccentButton* aliases, which is where the per-variant divergence
            // is written down.
            Assert.Equal(DesignTokens.Resolve("AccentTextBrush", variant), knob.Fill);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), toggle.Background);

            AssertClose(DesignTokens.ResolveBrushColor("AccentTextBrush", variant), TrackPixel(host, root, KnobCentreOn));
            AssertClose(DesignTokens.ResolveBrushColor("AccentBrush", variant), TrackPixel(host, root, KnobCentreOff));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ADisabledSettingSwitch_LeavesTheAccentFamilyEntirely(string variantName)
        {
            // The read-only settings panel is a screen full of these, and an unreachable "on" that
            // kept the accent face would read as an "on" that works. Same rule as the disabled
            // primary action and the disabled check box.
            var variant = ToVariant(variantName);
            var toggle = Sized(new ToggleButton { IsChecked = true });

            toggle.Theme = (ControlTheme)DesignTokens.Resolve("SettingSwitch", variant);

            using var host = ThemedHost.Show(toggle, variant, HostWidth, HostHeight);

            toggle.IsEnabled = false;

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), toggle.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), toggle.BorderBrush);
            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), KnobOf(toggle).Fill);

            // And the knob has NOT snapped back: a disabled switch still tells the truth about the
            // value it is showing, which is exactly what the read-only panel needs of it.
            var root = RootOf(toggle);

            AssertClose(DesignTokens.ResolveBrushColor("TextDisabledBrush", variant), TrackPixel(host, root, KnobCentreOn));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheSettingSwitch_RingsItsTrackOnKeyboardFocus(string variantName)
        {
            // The ring is asserted on the track's straight top edge rather than through
            // BorderPixel's left-edge probe: this is the only field cut to RadiusPill, and a pill's
            // leftmost pixel is the extremity of a curve, where antialiasing decides the answer.
            var variant = ToVariant(variantName);
            var toggle = Sized(new ToggleButton());

            toggle.Theme = (ControlTheme)DesignTokens.Resolve("SettingSwitch", variant);

            using var host = ThemedHost.Show(toggle, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", variant), toggle.BorderBrush);

            Assert.True(toggle.Focus(NavigationMethod.Tab), "The switch refused keyboard focus.");

            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), toggle.BorderBrush);

            var root = RootOf(toggle);
            var frame = host.Capture();
            var probe = root.TranslatePoint(new Point(SwitchTrackWidth / 2, 0), host.Window)
                ?? throw new InvalidOperationException("The track is not in the window's visual tree.");

            AssertClose(
                DesignTokens.ResolveBrushColor("AccentBrush", variant),
                FramePixels.At(frame, (int)probe.X, (int)probe.Y));
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
            //
            // Scoped to this file because these are the nine themes it owns;
            // ControlThemeBridgeTests runs the same scan over the whole directory, so a file added
            // to the layer later is covered without anybody remembering to extend a list.
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
            // ToggleButton, NOT ToggleSwitch, and the TargetType is the assertion: ToggleSwitch
            // declares `PART_MovingKnobs` a required template part, so a theme that owned its
            // template would have to write a `PART_` name — banned by contract 2 and by
            // ControlThemeBridgeTests across this whole directory. See Fields.axaml.
            ["SettingSwitch"] = typeof(ToggleButton),
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
            ["SettingSwitch"] = "RadiusPill",
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
        /// the two toggles, where the ring belongs on the box or the track rather than around the
        /// label — <see cref="CheckBox"/> derives from <see cref="ToggleButton"/>, so one test
        /// covers both.
        /// </summary>
        private static Visual RingOf(Control control)
        {
            return control is ToggleButton ? RootOf(control) : control;
        }

        /// <summary>
        /// The part whose fill the face probe should land on — the check box's own 16px box, the
        /// switch's 40x20 track, and the control itself for everything else.
        /// </summary>
        private static Visual FaceOf(Control control)
        {
            return control is ToggleButton ? RootOf(control) : control;
        }

        /// <summary>The moving knob of the setting switch.</summary>
        private static Avalonia.Controls.Shapes.Ellipse KnobOf(Control control)
        {
            return control.GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Ellipse>()
                .Single(ellipse => ellipse.Name == "Knob");
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
        /// The colour of the switch's track at <paramref name="x"/>, sampled on its vertical middle
        /// — either the knob's fill or the bare track, depending on which end the knob is at.
        /// </summary>
        private static Color TrackPixel(ThemedHost host, Visual track, double x)
        {
            var frame = host.Capture();
            var probe = track.TranslatePoint(new Point(x, SwitchTrackHeight / 2), host.Window)
                ?? throw new InvalidOperationException("The track is not in the window's visual tree.");

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
                _ when targetTypeName == typeof(ToggleButton).FullName => new ToggleButton(),
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
