using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The token picker and the Remap panel at the glass, in both theme variants: the chips on the
    /// <c>FilterChip</c> theme #86 left without a call site, the red <c>● Record</c>, the mono/sans
    /// split row by row, the <c>↵ assign</c> hint, the amber inline advisory, and the responsive chip
    /// row.
    /// <para>
    /// Two marks the mockups spell as characters are drawn as geometry here, and that is asserted
    /// rather than assumed: neither U+21B5 (<c>↵</c>) nor U+25CF (<c>●</c>) is in either embedded IBM
    /// Plex family, so a caption carrying one would render as tofu and every other assertion about
    /// the row would still hold.
    /// </para>
    /// </summary>
    public class TokenPickerTests
    {
        /// <summary>The rail's own content width: 268 px less the inspector section's 12 either side.</summary>
        private const double RailContentWidth = 244;

        /// <summary>
        /// What <c>ScrollViewer.tokenPickerResults</c> caps the result list at, and therefore the
        /// virtualizing viewport the realized rows are bounded by.
        /// </summary>
        private const double ResultsViewportHeight = 300;

        /// <summary>
        /// How many rows that viewport may realize before the list is not virtualizing any more. A
        /// row is a border, 4 px of padding either side and one or two lines of 11 px type, so ten
        /// or eleven fit and a virtualizing panel realizes a couple beyond them. The ceiling is
        /// deliberately loose — the regression it guards is 200, not 13.
        /// </summary>
        private const int MaxRealizedRows = 40;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheChips_ReachTheFilterChipTheme(string variantName)
        {
            // The theme was written with the rest of the control-theme layer and left with no bridge
            // and no call site. A bridge that goes missing fails silently — the chip keeps Fluent's
            // template and merely looks a little wrong.
            var variant = ToVariant(variantName);
            var scene = new Scene();

            using var host = Show(scene.CreatePickerView(), variant);

            var chips = Descendants<Button>(host).Where(button => button.Classes.Contains("filterChip")).ToArray();

            Assert.Equal(scene.Picker.Categories.Count, chips.Length);
            Assert.All(chips, chip => Assert.Same(Theme("FilterChip"), chip.Theme));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheSearchField_ReachesTheSearchFieldThemeAndItsDrawnGlyph(string variantName)
        {
            var scene = new Scene();

            using var host = Show(scene.CreatePickerView(), ToVariant(variantName));

            var field = Assert.Single(Descendants<TextBox>(host));

            Assert.Same(Theme("SearchField"), field.Theme);
            Assert.Equal(TokenPickerViewModel.SearchLabel, field.Watermark);

            // The theme draws `⌕` as a Path because U+2315 is in neither embedded family.
            Assert.Contains(
                Descendants<Avalonia.Controls.Shapes.Path>(host),
                path => path.Classes.Contains("searchGlyph"));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheCounter_IsOnScreenAndIsDerivedFromTheDialectsCatalog(string variantName)
        {
            var scene = new Scene();

            scene.Picker.Query = "esc";

            using var host = Show(scene.CreatePickerView(), ToVariant(variantName));

            var expected = $"{scene.Picker.MatchCount}/{KeySearchCatalog.Build(TokenDialect.Gen1).Count}";

            Assert.Contains(expected, VisibleTexts(host));

            // Mockup 1e's "18/1204" is illustrative: neither figure exists on this board.
            Assert.DoesNotContain("18/1204", VisibleTexts(host));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AResultRow_IsMonoWhereTheFileIsAndSansWhereTheAppIs(string variantName)
        {
            var scene = new Scene();

            scene.Picker.Query = "esc";

            using var host = Show(scene.CreatePickerView(), ToVariant(variantName));

            var blocks = Descendants<TextBlock>(host).Where(block => block.IsEffectivelyVisible).ToArray();

            var token = Assert.Single(blocks, block => block.Text == "esc");
            var name = Assert.Single(blocks, block => block.Text == "Esc");
            var header = Assert.Single(blocks, block => block.Text == "Navigation · 1");

            Assert.Contains("Mono", token.FontFamily.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("Mono", name.FontFamily.Name, StringComparison.Ordinal);

            // A group header is the app naming a §3 table, not a value out of a file — which is why
            // it is deliberately NOT on `sectionLabel`, the app's usual caption role, which is mono.
            Assert.DoesNotContain("Mono", header.FontFamily.Name, StringComparison.Ordinal);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheRows_WearTheSelectableListRowTheme_AndOnlyTheChosenOneIsSelected(string variantName)
        {
            var variant = ToVariant(variantName);
            var scene = new Scene();

            scene.Picker.Query = "vol";

            using var host = Show(scene.CreatePickerView(), variant);

            var rows = Descendants<ListBoxItem>(host).ToArray();

            Assert.True(rows.Length >= 2, "The 'vol' query drew too few rows.");
            Assert.All(rows, row => Assert.Same(Theme("SelectableListRow"), row.Theme));

            var selected = Assert.Single(rows, row => row.IsSelected);

            Assert.Equal(DesignTokens.Resolve("AccentSelectionFillBrush", variant), selected.Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheAssignHint_IsOnTheSelectedRowOnly_AndItsMarkIsDrawnNotTyped(string variantName)
        {
            var variant = ToVariant(variantName);
            var scene = new Scene();

            scene.Picker.Query = "vol";

            using var host = Show(scene.CreatePickerView(), variant);

            var hints = Descendants<TextBlock>(host)
                .Where(block => block.Text == TokenPickerRowViewModel.AssignHint)
                .ToArray();

            Assert.True(hints.Length >= 2, "Every row should carry the hint markup, shown or not.");
            Assert.Single(hints, hint => hint.IsEffectivelyVisible);

            var marks = Descendants<Icon>(host).Where(icon => icon.Classes.Contains("assignHint")).ToArray();
            var mark = Assert.Single(marks, icon => icon.IsEffectivelyVisible);

            Assert.NotNull(mark.Data);

            // TextSecondary, never AccentText: the row is filled with a 14% accent TINT, and
            // AccentText is the colour that rides a SOLID accent fill — white on the light variant,
            // which would be invisible here.
            Assert.Equal(DesignTokens.Resolve("TextSecondaryBrush", variant), mark.Stroke);
            Assert.NotEqual(DesignTokens.Resolve("AccentTextBrush", variant), mark.Stroke);

            // U+21B5 is in neither embedded family; nothing may print it as text.
            Assert.DoesNotContain(VisibleTexts(host), text => text.Contains('↵', StringComparison.Ordinal));
        }

        [AvaloniaFact]
        public void TheResultList_Virtualizes_SoTheViewportBoundsWhatIsRealized()
        {
            // Issue #133. The list used to be an ItemsControl of groups each holding an ItemsControl
            // of rows, and a list of lists cannot virtualize: every one of the dialect's ~200 rows —
            // some 2 300 controls — was realized into a 300 px viewport that shows about ten of
            // them, on the first open and on every keystroke. This is the assertion that says the
            // realized set follows the viewport instead of the catalog.
            var scene = new Scene();

            using var host = Show(scene.CreatePickerView(), ThemeVariant.Dark);

            // A virtualizing panel learns its viewport from EffectiveViewportChanged, which is
            // raised at the end of a layout pass and invalidates the measure it arrived too late
            // for. A second pass is what makes "how many did it realize" a settled question.
            host.Capture();

            var total = scene.Picker.MatchCount;

            Assert.True(total >= 100, $"The unfiltered Gen1 catalog came back as {total} rows; the case proves nothing.");
            Assert.Equal(scene.Picker.Groups.Count + scene.Picker.Rows.Count, scene.Picker.Items.Count);

            // The panel itself, so a revert to the default StackPanel fails here rather than only
            // in the count below (which a small enough catalog could pass by accident).
            Assert.Single(Descendants<VirtualizingStackPanel>(host));

            var realized = Descendants<ListBoxItem>(host).Count(row => row.IsEffectivelyVisible);

            Assert.True(
                realized >= 4,
                $"Only {realized} rows were realized into a {ResultsViewportHeight} px viewport — "
                + "the list drew almost nothing, so the bound below would pass vacuously.");
            Assert.True(
                realized <= MaxRealizedRows,
                $"{realized} of {total} rows were realized. The viewport is {ResultsViewportHeight} px "
                + "and shows about ten; a count near the catalog size means the list stopped virtualizing.");
        }

        [AvaloniaFact]
        public void AGroupHeader_IsItsOwnLineOfTheFlatList_NeverARowContainer()
        {
            // The reason the header was outside the row containers before the list was flattened,
            // and the reason it still is: a header drawn inside a row container would take the
            // row's selection fill along with it — and an arrow could land on it.
            var scene = new Scene();

            scene.Picker.Query = "vol";

            using var host = Show(scene.CreatePickerView(), ThemeVariant.Dark);

            var header = Assert.Single(
                Descendants<TextBlock>(host),
                block => block.IsEffectivelyVisible && block.Text == scene.Picker.Groups[0].Header);

            Assert.Empty(header.GetVisualAncestors().OfType<ListBoxItem>());

            // Every selectable container in the list is a row. This is what makes "↑/↓ never land on
            // a header" true at the glass as well as in TokenPickerViewModel.Rows.
            var rows = Descendants<ListBoxItem>(host).ToArray();

            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.IsType<TokenPickerRowViewModel>(row.DataContext));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnAliasRow_NamesItsCanonicalsAction_NeverItsOwnToken(string variantName)
        {
            var scene = new Scene();

            scene.Picker.Query = "num lock";

            using var host = Show(scene.CreatePickerView(), ToVariant(variantName));

            var texts = VisibleTexts(host);

            Assert.Contains("Num Lock — keypad layer duplicate", texts);
            Assert.DoesNotContain("alias of numlk", texts);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheRecordButton_IsTheRedRole_AndItsDotIsDrawnNotTyped(string variantName)
        {
            var variant = ToVariant(variantName);
            var scene = new Scene();

            using var host = Show(scene.CreatePanelView(), variant, RailWidth);

            var record = Assert.Single(
                Descendants<Button>(host),
                button => button.Classes.Contains("recordAction"));

            // handoff.md:82 gives the StatusError hue to Record as well as to Discard, so the two
            // share a theme rather than growing a 28th that paints the same thing.
            Assert.Same(Theme("DiscardButton"), record.Theme);
            Assert.Equal(DesignTokens.Resolve("StatusErrorTintBrush", variant), record.Background);

            var dot = Assert.Single(
                Descendants<Ellipse>(host),
                ellipse => ellipse.Classes.Contains("recordDot"));

            Assert.Equal(DesignTokens.Resolve("StatusErrorBrush", variant), dot.Fill);

            // U+25CF is in neither embedded family.
            Assert.DoesNotContain(VisibleTexts(host), text => text.Contains('●', StringComparison.Ordinal));
            Assert.Contains(RemapPanelViewModel.RecordCaption, VisibleTexts(host));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheCurrentTokenField_IsABorderOnTokenField_AndArmsWhileRecording(string variantName)
        {
            // Never a TextBox: focus inside one suspends keystroke capture, so the field would
            // swallow the very keypress it exists to record.
            var variant = ToVariant(variantName);
            var scene = new Scene();

            using var host = Show(scene.CreatePanelView(), variant, RailWidth);

            var field = Assert.Single(
                Descendants<Border>(host),
                border => border.Classes.Contains("actionField"));

            Assert.Same(Theme("TokenField"), field.Theme);
            Assert.DoesNotContain("armed", field.Classes);

            scene.Panel.RecordCommand.Execute(null);

            host.Capture();

            Assert.Contains("armed", field.Classes);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryBrush", variant), field.BorderBrush);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheInlineAdvisory_IsAmberAndNeverRed_AndDoesNotBlockTheAssignment(string variantName)
        {
            var variant = ToVariant(variantName);
            var scene = new Scene();

            scene.AssignDuplicate();

            using var host = Show(scene.CreatePanelView(), variant, RailWidth);

            Assert.True(scene.Key.Key.IsModified, "The advisory blocked the assignment it was reporting.");

            var note = Assert.Single(
                Descendants<TextBlock>(host),
                block => block.Text == scene.Panel.DuplicateAdvisory);

            Assert.True(note.IsEffectivelyVisible);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTextBrush", variant), note.Foreground);
            Assert.NotEqual(DesignTokens.Resolve("StatusErrorTextBrush", variant), note.Foreground);

            var block = Assert.Single(
                Descendants<Border>(host),
                border => border.Classes.Contains("pickerAdvisory"));

            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTintBrush", variant), block.Background);
        }

        [AvaloniaFact]
        public void TheAdvisoryWraps_RatherThanRunningOffTheRail()
        {
            // The defect a horizontal StackPanel produces: it measures its children with infinite
            // width, so TextWrapping never fires and a long sentence leaves the 268 px column.
            var scene = new Scene();

            scene.AssignDuplicate();

            using var host = Show(scene.CreatePanelView(), ThemeVariant.Dark, RailWidth);

            var note = Assert.Single(
                Descendants<TextBlock>(host),
                block => block.Text == scene.Panel.DuplicateAdvisory);

            Assert.Equal(TextWrapping.Wrap, note.TextWrapping);
            Assert.True(
                note.Bounds.Width <= RailContentWidth,
                $"The advisory was arranged {note.Bounds.Width} wide inside a {RailContentWidth} px rail.");
            Assert.True(note.Bounds.Height > 0, "The advisory was arranged with no height at all.");
        }

        [AvaloniaFact]
        public void NothingInThePanel_IsArrangedWiderThanTheRail()
        {
            // The whole panel, not one label: anything wider than the column is clipped by the rail
            // and reads as a truncation the user cannot fix.
            var scene = new Scene();

            scene.Panel.Picker.Query = "vol";
            scene.AssignDuplicate();

            var view = scene.CreatePanelView();

            using var host = Show(view, ThemeVariant.Dark, RailWidth);

            var overflowing = Descendants<Control>(host)
                .Where(control => control.IsEffectivelyVisible)
                .Where(control => control.TranslatePoint(default, view) is { } origin
                                  && origin.X + control.Bounds.Width > RailWidth + 0.5)
                .Select(control => $"{control.GetType().Name} at {control.Bounds}")
                .ToArray();

            Assert.True(overflowing.Length == 0, string.Join(Environment.NewLine, overflowing));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void InTheRail_TheChipRowReducesToTheFiveOfMockup2A(string variantName)
        {
            var scene = new Scene();

            using var host = Show(scene.CreatePanelView(), ToVariant(variantName), RailWidth);

            Assert.True(scene.Panel.Picker.IsNarrow, "The rail's own width did not reach the picker.");

            var shown = Descendants<Button>(host)
                .Where(button => button.Classes.Contains("filterChip") && button.IsEffectivelyVisible)
                .Select(button => button.Content as string)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    TokenPickerCategoryViewModel.AllCaption,
                    TokenPickerCategoryViewModel.LettersCaption,
                    TokenPickerCategoryViewModel.NavCaption,
                    TokenPickerCategoryViewModel.MediaCaption,
                    TokenPickerCategoryViewModel.RecentCaption
                },
                shown);
        }

        [AvaloniaFact]
        public void InTheModal_AllSevenChipsFit()
        {
            // One responsive rule, both of the mockups' chip rows: 1e's seven in the wide panel,
            // 2a's five in the rail.
            var overlay = new TokenPickerOverlayViewModel(TokenPickerOverlayViewModel.MacroTitle, TokenDialect.Gen1);
            var view = new TokenPickerOverlayView { DataContext = overlay };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.False(overlay.Picker.IsNarrow);
            Assert.Equal(
                overlay.Picker.Categories.Count,
                Descendants<Button>(host).Count(button => button.Classes.Contains("filterChip") && button.IsEffectivelyVisible));
        }

        [AvaloniaFact]
        public void TheModal_PutsTheCaretInItsQueryBox()
        {
            // ViewLocator materialises the picker a layout pass after the overlay is constructed, so
            // the request has to survive until there is a field to answer it.
            var overlay = new TokenPickerOverlayViewModel(TokenPickerOverlayViewModel.MacroTitle, TokenDialect.Gen1);
            var view = new TokenPickerOverlayView { DataContext = overlay };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var field = Assert.Single(Descendants<TextBox>(host));

            Assert.True(field.IsFocused, "The modal opened without its caret in the query box.");
            Assert.False(overlay.Picker.IsFocusPending);
        }

        [AvaloniaFact]
        public void TheRailsPicker_NeverStealsTheCaretOnItsOwn()
        {
            // Focus inside a TextBox suspends keystroke capture. A picker that grabbed the caret as
            // the Remap tab opened would quietly break "click a key, press a key".
            var scene = new Scene();

            using var host = Show(scene.CreatePanelView(), ThemeVariant.Dark, RailWidth);

            var field = Assert.Single(Descendants<TextBox>(host));

            Assert.False(field.IsFocused);

            scene.Panel.FocusSearch();

            host.Capture();

            Assert.True(field.IsFocused, "⌘F did not reach the query box.");
        }

        private const double RailWidth = 268;

        private static ThemedHost Show(Control view, ThemeVariant variant, double width = 480)
        {
            var host = ThemedHost.Show(view, variant, width, ThemedHost.DefaultHeight);

            host.Capture();

            return host;
        }

        private static IEnumerable<T> Descendants<T>(ThemedHost host) where T : Visual
        {
            return host.Window.GetVisualDescendants().OfType<T>();
        }

        private static IReadOnlyList<string> VisibleTexts(ThemedHost host)
        {
            return Descendants<TextBlock>(host)
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty)
                .ToArray();
        }

        private static ControlTheme Theme(string key)
        {
            return (ControlTheme)DesignTokens.Resolve(key, ThemeVariant.Dark);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>A Remap panel over a real board, refreshed the way the rail refreshes it.</summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public KeyboardLayerViewModel Layer { get; }

            public RemapPanelViewModel Panel { get; }

            public TokenPickerViewModel Picker => Panel.Picker;

            public KeyboardKeyViewModel Key { get; }

            public Scene()
            {
                Layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
                Layer = KeyboardLayerViewModel.BuildAll(Layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];
                Panel = new RemapPanelViewModel(Layout.Dialect);
                Key = Layer.FindByIndex(TestLayouts.RgbDigitOneKeyIndex)!;

                Panel.Assigned += (_, _) => Refresh();

                Refresh();
            }

            /// <summary>Puts the digit 2 on the digit 1 position, which the layer already carries.</summary>
            public void AssignDuplicate()
            {
                Picker.Query = "2";

                Picker.ChooseCommand.Execute(Picker.Rows.First(row => row.Token == "2"));
            }

            public RemapPanelView CreatePanelView()
            {
                return new RemapPanelView { DataContext = Panel };
            }

            public TokenPickerView CreatePickerView()
            {
                return new TokenPickerView { DataContext = Picker };
            }

            private void Refresh()
            {
                Panel.Refresh(Key, Layer, Layout, EditorAdvisories.Build(Layout));
            }
        }
    }
}
