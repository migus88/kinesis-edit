using KinesisEdit.Core.Keys;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The token picker as §11.6's <c>Search Keys (Macro)</c> modal: the verbatim strings, the
    /// validation, and the accept path the macro panel's insertion depends on.
    /// <para>
    /// Everything the user searches and filters is <see cref="TokenPickerViewModel"/> and is covered
    /// by its own suite; what is asserted here is only what the wrapper adds.
    /// </para>
    /// </summary>
    public sealed class TokenPickerOverlayViewModelTests
    {
        [Fact]
        public void TheTitlesAndTheValidation_AreSpec11Point6sWordsVerbatim()
        {
            Assert.Equal("Search Keys", TokenPickerOverlayViewModel.DefaultTitle);
            Assert.Equal("Search Keys (Macro)", TokenPickerOverlayViewModel.MacroTitle);
            Assert.Equal("Search key", TokenPickerOverlayViewModel.SearchLabel);
            Assert.Equal("You must select a key", TokenPickerOverlayViewModel.NoSelectionMessage);
        }

        [Fact]
        public void ItOpensOverTheDialectsCatalog_WithItsTitle()
        {
            var overlay = Create();

            Assert.Equal(TokenPickerOverlayViewModel.MacroTitle, overlay.Title);
            Assert.Equal(TokenDialect.Gen1, overlay.Picker.Dialect);
            Assert.Equal(KeySearchCatalog.Build(TokenDialect.Gen1).Count, overlay.Picker.TotalCount);
            Assert.Null(overlay.SelectedKey);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public void ItAsksForTheCaretAsItOpens()
        {
            // A type-to-filter modal opens with the caret in its field; the request has to survive
            // until ViewLocator has built a field to put it in.
            var overlay = Create();

            Assert.True(overlay.Picker.IsFocusPending);
        }

        [Fact]
        public void AcceptingWithNothingSelected_RefusesInSpec11Point6sWords_AndStaysOpen()
        {
            var overlay = Create();

            overlay.AcceptCommand.Execute(null);

            Assert.Equal(TokenPickerOverlayViewModel.NoSelectionMessage, overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
            Assert.False(overlay.WasAccepted);
            Assert.Null(overlay.SelectedKey);
        }

        [Fact]
        public void AcceptingASelection_ReportsItAndCloses()
        {
            var overlay = Create();
            KeyDefinition? selected = null;

            overlay.Selected += definition => selected = definition;

            overlay.Picker.Query = "esc";
            overlay.AcceptCommand.Execute(null);

            Assert.Same(TestLayouts.Gen1Key("esc"), selected);
            Assert.Same(selected, overlay.SelectedKey);
            Assert.True(overlay.WasAccepted);
            Assert.True(overlay.IsClosed);
            Assert.Null(overlay.ErrorMessage);
        }

        [Fact]
        public void TakingARowAccepts_WhichIsBothTheDoubleClickAndReturn()
        {
            var overlay = Create();
            KeyDefinition? selected = null;

            overlay.Selected += definition => selected = definition;

            overlay.Picker.Query = "vol";

            var row = overlay.Picker.Rows.First(candidate => candidate.Token == "vol+");

            overlay.Picker.ChooseCommand.Execute(row);

            Assert.Same(row.Definition, selected);
            Assert.True(overlay.IsClosed);
        }

        [Fact]
        public void AnAcceptedPick_JoinsTheSessionsRecentList()
        {
            // The same store the rail's picker reads, so a token inserted into a macro is offered
            // back on the Recent chip.
            var store = new RecentTokenStore();
            var overlay = new TokenPickerOverlayViewModel(TokenPickerOverlayViewModel.MacroTitle, TokenDialect.Gen1, store);

            overlay.Picker.Query = "esc";
            overlay.AcceptCommand.Execute(null);

            Assert.Same(TestLayouts.Gen1Key("esc"), Assert.Single(store.Entries));
        }

        [Fact]
        public void Cancelling_ReportsNothingAndCloses()
        {
            var overlay = Create();
            var raised = 0;

            overlay.Selected += _ => raised++;

            overlay.Picker.Query = "esc";
            overlay.CancelCommand.Execute(null);

            Assert.Equal(0, raised);
            Assert.Null(overlay.SelectedKey);
            Assert.False(overlay.WasAccepted);
            Assert.True(overlay.IsClosed);
        }

        [Fact]
        public void AClosedOverlay_IgnoresFurtherCommands()
        {
            var overlay = Create();
            var raised = 0;

            overlay.Selected += _ => raised++;

            overlay.CancelCommand.Execute(null);

            overlay.Picker.Query = "esc";
            overlay.AcceptCommand.Execute(null);

            Assert.Equal(0, raised);
        }

        [Fact]
        public void FocusSearch_ReachesThePickersOwnRequest()
        {
            var overlay = Create();
            var raised = 0;

            overlay.Picker.FocusRequested += (_, _) => raised++;

            overlay.FocusSearch();

            Assert.Equal(1, raised);
        }

        private static TokenPickerOverlayViewModel Create()
        {
            return new TokenPickerOverlayViewModel(TokenPickerOverlayViewModel.MacroTitle, TokenDialect.Gen1);
        }
    }
}
