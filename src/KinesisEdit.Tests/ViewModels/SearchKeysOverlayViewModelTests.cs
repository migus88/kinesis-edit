using KinesisEdit.Core.Keys;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Search Keys overlay of specs/11-feature-dialogs.md §11.6: the four caller-supplied
    /// titles, the incremental filter over the catalog, the double-click accept, and the
    /// "You must select a key" validation.
    /// </summary>
    public sealed class SearchKeysOverlayViewModelTests
    {
        [Fact]
        public void Titles_MatchTheFourTheSpecNames()
        {
            Assert.Equal("Search Keys", SearchKeysOverlayViewModel.DefaultTitle);
            Assert.Equal("Search Keys (Macro)", SearchKeysOverlayViewModel.MacroTitle);
            Assert.Equal("Search Keys (Tap Action)", SearchKeysOverlayViewModel.TapActionTitle);
            Assert.Equal("Search Keys (Hold Action)", SearchKeysOverlayViewModel.HoldActionTitle);
            Assert.Equal("Search key", SearchKeysOverlayViewModel.SearchLabel);
            Assert.Equal("You must select a key", SearchKeysOverlayViewModel.NoSelectionMessage);
        }

        [Fact]
        public void Constructor_ForADialect_ListsThatDialectsSearchableActions()
        {
            var overlay = Create();

            Assert.Equal(KeySearchCatalog.Build(TokenDialect.Gen1).Count, overlay.Results.Count);
            Assert.NotEmpty(overlay.Results);
            Assert.Equal(SearchKeysOverlayViewModel.DefaultTitle, overlay.Title);
        }

        [Fact]
        public void Query_WithAName_NarrowsTheListToTheMatches()
        {
            var overlay = Create();

            overlay.Query = "Caps";

            Assert.NotEmpty(overlay.Results);
            Assert.True(overlay.Results.Count < KeySearchCatalog.Build(TokenDialect.Gen1).Count);
            Assert.All(overlay.Results, entry => Assert.Contains("caps", entry.DisplayText, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Query_WithAFileToken_MatchesByTokenToo()
        {
            var overlay = Create();
            var entry = Find(overlay, "lctrl");

            overlay.Query = entry.FileToken;

            Assert.Contains(overlay.Results, row => row.FileToken == entry.FileToken);
        }

        [Fact]
        public void Query_ClearedAgain_RestoresEveryRow()
        {
            var overlay = Create();
            var total = overlay.Results.Count;

            overlay.Query = "caps";
            overlay.Query = string.Empty;

            Assert.Equal(total, overlay.Results.Count);
        }

        [Fact]
        public void Query_ThatFiltersOutTheSelection_ClearsIt()
        {
            var overlay = Create();

            overlay.SelectedEntry = Find(overlay, "caps");
            overlay.Query = "zzzzzz-no-such-key";

            Assert.Null(overlay.SelectedEntry);
            Assert.Empty(overlay.Results);
        }

        [Fact]
        public void Accept_WithNothingSelected_ReportsTheSpecMessageAndStaysOpen()
        {
            var overlay = Create();

            overlay.AcceptCommand.Execute(null);

            Assert.Equal(SearchKeysOverlayViewModel.NoSelectionMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
            Assert.Null(overlay.SelectedKey);
        }

        [Fact]
        public void Accept_WithASelection_RaisesSelectedWithThatActionAndCloses()
        {
            var overlay = Create();
            var entry = Find(overlay, "caps");
            KeyDefinition? selected = null;

            overlay.Selected += definition => selected = definition;
            overlay.SelectedEntry = entry;
            overlay.AcceptCommand.Execute(null);

            Assert.Same(entry.Definition, selected);
            Assert.Same(entry.Definition, overlay.SelectedKey);
            Assert.True(overlay.WasAccepted);
            Assert.True(overlay.IsClosed);
            Assert.Null(overlay.ErrorMessage);
        }

        [Fact]
        public void ChooseCommand_WithARow_SelectsItAndAcceptsImmediately()
        {
            var overlay = Create();
            var entry = Find(overlay, "caps");

            overlay.ChooseCommand.Execute(entry);

            Assert.Same(entry, overlay.SelectedEntry);
            Assert.Same(entry.Definition, overlay.SelectedKey);
            Assert.True(overlay.IsClosed);
        }

        [Fact]
        public void ChooseCommand_WithNoRow_ReportsTheSpecMessage()
        {
            var overlay = Create();

            overlay.ChooseCommand.Execute(null);

            Assert.Equal(SearchKeysOverlayViewModel.NoSelectionMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public void Cancel_AfterASelection_ClosesWithoutReportingIt()
        {
            var overlay = Create();
            var raised = 0;

            overlay.Selected += _ => raised++;
            overlay.SelectedEntry = Find(overlay, "caps");
            overlay.CancelCommand.Execute(null);

            Assert.Equal(0, raised);
            Assert.True(overlay.IsClosed);
            Assert.False(overlay.WasAccepted);
        }

        private static SearchKeysOverlayViewModel Create(string? title = null)
        {
            return new SearchKeysOverlayViewModel(title ?? SearchKeysOverlayViewModel.DefaultTitle, TokenDialect.Gen1);
        }

        private static KeySearchEntry Find(SearchKeysOverlayViewModel overlay, string token)
        {
            return overlay.Results.First(entry => entry.FileToken == token);
        }
    }
}
