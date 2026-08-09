using KinesisEdit.Core.Keys;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The token picker's own behaviour: the counter, the query, the chips, <c>Recent</c>, the
    /// grouping, the aliases and what <c>↵</c> takes. No Avalonia anywhere — the picker is a view
    /// model over Core's catalog and nothing else.
    /// <para>
    /// <b>Three things the mockups draw are unreachable, and the tests say so rather than chasing
    /// them.</b> The counter's <c>18/1204</c> is illustrative (the Gen1 searchable catalog is 200
    /// rows and <c>esc</c> matches one of them); <c>[escape]</c> as "alias of <c>[esc]</c>" is two
    /// tokens of one key-table entry, which no catalog ever lists together; and every real alias
    /// spells the same token as its canonical, so the label has to name the canonical's action
    /// instead of its token.
    /// </para>
    /// </summary>
    public sealed class TokenPickerViewModelTests
    {
        [Fact]
        public void TheCounterDenominator_IsTheDialectsOwnCatalogCount_NeverALiteral()
        {
            var picker = Create();

            Assert.Equal(KeySearchCatalog.Build(TokenDialect.Gen1).Count, picker.TotalCount);
            Assert.Equal(picker.TotalCount, picker.MatchCount);
            Assert.Equal($"{picker.TotalCount}/{picker.TotalCount}", picker.CounterText);
        }

        [Theory]
        [InlineData(TokenDialect.Legacy)]
        [InlineData(TokenDialect.Gen1)]
        [InlineData(TokenDialect.Gen2)]
        public void EveryDialect_CountsItsOwnCatalog(TokenDialect dialect)
        {
            // The three dialects name different sets of actions, so a denominator shared between
            // them would be wrong on at least two boards.
            var picker = new TokenPickerViewModel(dialect);

            Assert.Equal(KeySearchCatalog.Build(dialect).Count, picker.TotalCount);
        }

        [Fact]
        public void Querying_NarrowsTheCounterAndTheRows()
        {
            var picker = Create();

            picker.Query = "esc";

            Assert.Equal(picker.Rows.Count, picker.MatchCount);
            Assert.True(picker.MatchCount < picker.TotalCount, "The query narrowed nothing.");
            Assert.Contains(picker.Rows, row => row.Token == "[esc]");
            Assert.Equal($"{picker.MatchCount}/{picker.TotalCount}", picker.CounterText);
        }

        [Fact]
        public void AQueryForEsc_IsOneRowUnderOneHeader_NotTheMockups18()
        {
            // Mockup 1e draws "18/1204" and a "Navigation · 3" header over four rows. The real Gen1
            // catalog answers one row in one group; the mock's figures are illustrative, and a test
            // written to them would be a test written to fiction.
            var picker = Create();

            picker.Query = "esc";

            var group = Assert.Single(picker.Groups);

            Assert.Equal(TokenPickerGroupViewModel.TitleFor(KeyTable.Navigation) + " · 1", group.Header);
            Assert.Equal("[esc]", Assert.Single(group.Rows).Token);
        }

        [Fact]
        public void AQueryThatMatchesNothing_SaysSoAndSelectsNothing()
        {
            var picker = Create();

            picker.Query = "no such action";

            Assert.Empty(picker.Rows);
            Assert.Empty(picker.Groups);
            Assert.False(picker.HasMatches);
            Assert.Null(picker.SelectedRow);
        }

        [Fact]
        public void QueryingIsCaseInsensitive_AndMatchesTheFileTokenAsWellAsTheName()
        {
            // §11.6: the user searches "by either name or file token". Gen1 spells Print Screen
            // `[prnt]`, so the two halves of that rule are separable on this one row.
            var picker = Create();

            picker.Query = "ESC";

            Assert.Contains(picker.Rows, row => row.Token == "[esc]");

            picker.Query = "print scrn";

            Assert.Contains(picker.Rows, row => row.Token == "[prnt]");

            picker.Query = "PRNT";

            Assert.Contains(picker.Rows, row => row.Token == "[prnt]");
        }

        [Fact]
        public void TheChips_AreTheMockupsSevenWithRecentLast()
        {
            var picker = Create();

            Assert.Equal(
                new[]
                {
                    TokenPickerCategoryViewModel.AllCaption,
                    TokenPickerCategoryViewModel.LettersCaption,
                    TokenPickerCategoryViewModel.NavCaption,
                    TokenPickerCategoryViewModel.MediaCaption,
                    TokenPickerCategoryViewModel.MouseCaption,
                    TokenPickerCategoryViewModel.HotkeysCaption,
                    TokenPickerCategoryViewModel.RecentCaption
                },
                picker.Categories.Select(chip => chip.Caption));

            Assert.True(picker.Categories[0].IsSelected);
            Assert.All(picker.Categories.Skip(1), chip => Assert.False(chip.IsSelected));
        }

        [Theory]
        [InlineData(KeySearchCategory.Letters)]
        [InlineData(KeySearchCategory.Nav)]
        [InlineData(KeySearchCategory.Media)]
        [InlineData(KeySearchCategory.Mouse)]
        [InlineData(KeySearchCategory.Hotkeys)]
        public void AChip_NarrowsToExactlyItsCategory(KeySearchCategory category)
        {
            var picker = Create();
            var chip = picker.Categories.Single(candidate => !candidate.IsRecent && candidate.Category == category);

            picker.SelectCategoryCommand.Execute(chip);

            var expected = KeySearchCatalog.Filter(KeySearchCatalog.Build(TokenDialect.Gen1), category);

            Assert.True(chip.IsSelected);
            Assert.Equal(expected.Count, picker.MatchCount);
            Assert.All(picker.Rows, row => Assert.Equal(category, row.Entry.Category));
        }

        [Fact]
        public void ChoosingAChip_DeselectsTheOneBefore()
        {
            var picker = Create();

            picker.SelectCategoryCommand.Execute(picker.Categories[1]);
            picker.SelectCategoryCommand.Execute(picker.Categories[2]);

            Assert.Equal(1, picker.Categories.Count(chip => chip.IsSelected));
            Assert.True(picker.Categories[2].IsSelected);
        }

        [Fact]
        public void AChipAndAQuery_Compose()
        {
            var picker = Create();

            picker.SelectCategoryCommand.Execute(
                picker.Categories.Single(chip => !chip.IsRecent && chip.Category == KeySearchCategory.Media));
            picker.Query = "vol";

            Assert.NotEmpty(picker.Rows);
            Assert.All(picker.Rows, row => Assert.Equal(KeySearchCategory.Media, row.Entry.Category));
            Assert.All(picker.Rows, row => Assert.Contains("vol", row.Token, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TheRecentChip_IsEmptyUntilSomethingIsAssigned()
        {
            var picker = Create();

            picker.SelectCategoryCommand.Execute(RecentChip(picker));

            Assert.True(picker.IsRecentSelected);
            Assert.Empty(picker.Rows);
            Assert.Equal(0, picker.MatchCount);
        }

        [Fact]
        public void TheRecentChip_ListsWhatWasAssigned_MostRecentFirst()
        {
            var picker = Create();

            picker.Remember(TestLayouts.Gen1Key("esc"));
            picker.Remember(TestLayouts.Gen1Key("f1"));

            picker.SelectCategoryCommand.Execute(RecentChip(picker));

            Assert.Equal(new[] { "[F1]", "[esc]" }, picker.Rows.Select(row => row.Token));
        }

        [Fact]
        public void RememberingSomethingTwice_MovesItUpRatherThanListingItTwice()
        {
            var picker = Create();

            picker.Remember(TestLayouts.Gen1Key("esc"));
            picker.Remember(TestLayouts.Gen1Key("f1"));
            picker.Remember(TestLayouts.Gen1Key("esc"));

            picker.SelectCategoryCommand.Execute(RecentChip(picker));

            Assert.Equal(new[] { "[esc]", "[F1]" }, picker.Rows.Select(row => row.Token));
        }

        [Fact]
        public void TheRecentChip_RefiltersWhenSomethingIsAssignedWhileItIsLive()
        {
            var picker = Create();

            picker.SelectCategoryCommand.Execute(RecentChip(picker));
            picker.Remember(TestLayouts.Gen1Key("esc"));

            Assert.Equal("[esc]", Assert.Single(picker.Rows).Token);
        }

        [Fact]
        public void TheRecentStore_IsSharedWhenItIsHandedIn()
        {
            // The rail's picker and the macro-insertion picker of one editor remember the same
            // things; that is the whole reason the store is a parameter.
            var store = new RecentTokenStore();
            var rail = new TokenPickerViewModel(TokenDialect.Gen1, store);
            var modal = new TokenPickerViewModel(TokenDialect.Gen1, store);

            rail.Remember(TestLayouts.Gen1Key("esc"));

            modal.SelectCategoryCommand.Execute(RecentChip(modal));

            Assert.Equal("[esc]", Assert.Single(modal.Rows).Token);
        }

        [Fact]
        public void ResultsAreGrouped_ByTheKeyTableAndCountedByWhatIsUnderTheHeader()
        {
            var picker = Create();

            picker.SelectCategoryCommand.Execute(
                picker.Categories.Single(chip => !chip.IsRecent && chip.Category == KeySearchCategory.Letters));

            Assert.NotEmpty(picker.Groups);
            Assert.All(picker.Groups, group => Assert.Equal(group.Rows.Count, group.Count));
            Assert.All(
                picker.Groups,
                group => Assert.All(group.Rows, row => Assert.Equal(group.Table, row.Entry.Definition.Table)));
            Assert.Equal(picker.MatchCount, picker.Groups.Sum(group => group.Count));
        }

        [Fact]
        public void AGroupHeader_NamesTheTableInProseAndCountsIt()
        {
            var picker = Create();

            picker.Query = "esc";

            var group = Assert.Single(picker.Groups);

            Assert.Equal("Navigation", group.Title);
            Assert.Equal("Navigation · 1", group.Header);
        }

        [Fact]
        public void AnAliasRow_IsLabelledByItsCanonicalsNameAndItsOwnScope_NeverByItsToken()
        {
            // Gen1's [numlk] is registered twice: once plainly and once on the keypad layer. Both
            // rows spell [numlk], so "alias of [numlk]" would be on the row it names.
            var picker = Create();

            picker.Query = "num lock";

            var alias = Assert.Single(picker.Rows, row => row.IsAlias);

            Assert.Equal("[numlk]", alias.Token);
            Assert.Equal("Num Lock — keypad layer duplicate", alias.Detail);
            Assert.DoesNotContain("[", alias.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public void Escape_HasNoAliasAtAll_BecauseEscapeAndEscAreOneEntrysTwoDialects()
        {
            // Mockup 1e draws [escape] as "alias of [esc]". They are the Legacy and Gen1 tokens of
            // one KeyDefinition, so no catalog lists both and neither is an alias of anything.
            var gen1 = Create();
            var legacy = new TokenPickerViewModel(TokenDialect.Legacy);

            gen1.Query = "esc";
            legacy.Query = "esc";

            Assert.All(gen1.Rows, row => Assert.False(row.IsAlias));
            Assert.DoesNotContain(gen1.Rows, row => row.Token == "[escape]");
            Assert.DoesNotContain(legacy.Rows, row => row.Token == "[esc]");
        }

        [Fact]
        public void AScopedRow_SaysWhereItLives()
        {
            var picker = Create();

            picker.Query = "kp0";

            var row = Assert.Single(picker.Rows);

            Assert.False(row.IsAlias);
            Assert.Equal(TokenPickerRowViewModel.KeypadScopeLabel, row.Detail);
        }

        [Fact]
        public void AnOrdinaryRow_SaysNothingBeyondItsName()
        {
            var picker = Create();

            picker.Query = "esc";

            var row = Assert.Single(picker.Rows);

            Assert.Equal("Esc", row.Name);
            Assert.False(row.HasDetail);
        }

        [Fact]
        public void WithNoFilterAtAll_NothingIsHighlighted()
        {
            // Which is what keeps §11.6's "You must select a key" reachable, and what stops ↵
            // assigning whichever action happens to sort first.
            var picker = Create();

            Assert.Null(picker.SelectedRow);
            Assert.Null(picker.SelectedKey);
            Assert.False(picker.HasSelection);
        }

        [Fact]
        public void Narrowing_HighlightsTheFirstRow_SoTypingThenReturnAssigns()
        {
            var picker = Create();

            picker.Query = "esc";

            Assert.Same(picker.Rows[0], picker.SelectedRow);
            Assert.True(picker.Rows[0].IsSelected);
        }

        [Fact]
        public void AHighlightThatSurvivesTheNextKeystroke_IsKept()
        {
            var picker = Create();

            picker.Query = "vol";

            var chosen = picker.Rows.First(row => row.Token == "[vol+]");

            picker.SelectRowCommand.Execute(chosen);

            picker.Query = "vol+";

            Assert.Equal("[vol+]", picker.SelectedRow!.Token);
        }

        [Fact]
        public void OnlyOneRowEverCarriesTheAssignHint()
        {
            var picker = Create();

            picker.Query = "vol";

            var first = picker.Rows[0];
            var second = picker.Rows[1];

            picker.SelectRowCommand.Execute(first);
            picker.SelectRowCommand.Execute(second);

            Assert.False(first.IsSelected);
            Assert.True(second.IsSelected);
            Assert.Equal(1, picker.Rows.Count(row => row.IsSelected));
        }

        [Fact]
        public void Choose_RaisesTheHighlightedRowsAction()
        {
            var picker = Create();

            picker.Query = "esc";

            KeyDefinition? chosen = null;

            picker.Chosen += definition => chosen = definition;

            picker.ChooseCommand.Execute(null);

            Assert.Same(TestLayouts.Gen1Key("esc"), chosen);
        }

        [Fact]
        public void ChooseWithARow_HighlightsItFirst()
        {
            // The double click: §11.6's "double-clicking an item accepts immediately".
            var picker = Create();

            picker.Query = "vol";

            var row = picker.Rows.First(candidate => candidate.Token == "[vol-]");
            KeyDefinition? chosen = null;

            picker.Chosen += definition => chosen = definition;

            picker.ChooseCommand.Execute(row);

            Assert.Same(row.Definition, chosen);
            Assert.Same(row, picker.SelectedRow);
        }

        [Fact]
        public void ChooseWithNothingHighlighted_RaisesNothing()
        {
            var picker = Create();
            var raised = 0;

            picker.Chosen += _ => raised++;

            picker.ChooseCommand.Execute(null);

            Assert.Equal(0, raised);
        }

        [Fact]
        public void ArrowsWalkTheResults_AndWrap()
        {
            var picker = Create();

            picker.Query = "vol";

            Assert.True(picker.Rows.Count >= 2, "The 'vol' query returned too few rows to walk.");

            picker.SelectNextCommand.Execute(null);

            Assert.Same(picker.Rows[1], picker.SelectedRow);

            picker.SelectPreviousCommand.Execute(null);
            picker.SelectPreviousCommand.Execute(null);

            Assert.Same(picker.Rows[^1], picker.SelectedRow);
        }

        [Fact]
        public void ArrowsOnAnEmptyList_DoNothing()
        {
            var picker = Create();

            picker.Query = "no such action";

            picker.SelectNextCommand.Execute(null);
            picker.SelectPreviousCommand.Execute(null);

            Assert.Null(picker.SelectedRow);
        }

        [Fact]
        public void TheFlatItemList_IsEachHeaderFollowedByItsOwnRows()
        {
            // What the view draws. `Groups` and `Rows` are the same objects seen two other ways, so
            // a header the list forgot, or a row drawn under the wrong caption, is a divergence
            // between three views of one answer rather than a rendering accident.
            var picker = Create();

            var expected = new List<ITokenPickerItem>();

            foreach (var group in picker.Groups)
            {
                expected.Add(group);
                expected.AddRange(group.Rows);
            }

            Assert.Equal(picker.Groups.Count + picker.Rows.Count, picker.Items.Count);
            Assert.True(
                picker.Items.SequenceEqual(expected),
                "The flat list is not each group's header followed by that group's own rows.");

            // The two kinds say which they are; the flat list is the only place they meet.
            Assert.All(picker.Items.OfType<TokenPickerGroupViewModel>(), header => Assert.True(header.IsHeader));
            Assert.All(picker.Items.OfType<TokenPickerRowViewModel>(), row => Assert.False(row.IsHeader));
        }

        [Fact]
        public void TheRowList_HoldsNoHeaders_SoArrowsWalkOnlyRealRows()
        {
            // ↑/↓ walk `Rows`, and the whole reason `Items` is a separate list is that `Rows` must
            // stay free of the captions the view draws between the blocks.
            var picker = Create();

            Assert.True(picker.Groups.Count > 1, "The unfiltered catalog came back as one group.");
            Assert.True(picker.Items.Count > picker.Rows.Count, "The flat list carries no headers at all.");

            var walked = new List<TokenPickerRowViewModel>();

            for (var step = 0; step < picker.Rows.Count; step++)
            {
                picker.SelectNextCommand.Execute(null);

                var landed = picker.SelectedRow;

                Assert.NotNull(landed);
                Assert.False(landed.IsHeader);
                Assert.Contains(landed, picker.Rows);

                walked.Add(landed);
            }

            // Every row, in order, and nothing else: a walk that crossed every group boundary in
            // the catalog without once stopping on one of them.
            Assert.Equal(picker.Rows.Count, walked.Count);
            Assert.True(walked.SequenceEqual(picker.Rows), "The arrow walk did not visit the rows in order.");
        }

        [Fact]
        public void Clear_WithNothingToClear_RebuildsNothingAndNotifiesNothing()
        {
            // Issue #133: RemapPanelViewModel.Refresh clears the picker on every position the rail
            // is pointed at, and Clear used to run the whole filter — one new row view model per
            // catalog entry, a new group list, a new flat list, and the result tree the view builds
            // from them. Selecting a key took about a second because of it.
            var picker = Create();

            var rows = picker.Rows;
            var groups = picker.Groups;
            var items = picker.Items;
            var first = picker.Rows[0];
            var changes = 0;

            picker.PropertyChanged += (_, _) => changes++;

            picker.Clear();

            Assert.Same(rows, picker.Rows);
            Assert.Same(groups, picker.Groups);
            Assert.Same(items, picker.Items);
            Assert.Same(first, picker.Rows[0]);

            // Not one notification either: a binding that re-evaluates is a tree that re-realizes.
            Assert.Equal(0, changes);
        }

        [Fact]
        public void Clear_WithAHighlightAndNoQuery_DropsTheHighlightWithoutRebuildingTheRows()
        {
            // The stale-highlight case Clear exists for. It still has to go — ↵ must not assign an
            // action the user chose for a different key — but the rows it was chosen from are the
            // rows a rebuild would have arrived at, so they stay.
            var picker = Create();

            var rows = picker.Rows;
            var chosen = picker.Rows[3];

            picker.SelectRowCommand.Execute(chosen);

            Assert.True(chosen.IsSelected);

            picker.Clear();

            Assert.Same(rows, picker.Rows);
            Assert.Same(chosen, picker.Rows[3]);
            Assert.Null(picker.SelectedRow);
            Assert.Null(picker.SelectedKey);
            Assert.False(picker.HasSelection);
            Assert.False(chosen.IsSelected);
        }

        [Fact]
        public void Clear_UnderALiveChipWithNoQuery_FallsBackToTheFirstRowWithoutRebuildingIt()
        {
            // A chip is itself a narrowing, so the rule "a narrowed list opens on its first row"
            // still applies — and it applies over the rows that are already there.
            var picker = Create();
            var chip = picker.Categories.Single(candidate => !candidate.IsRecent && candidate.Category == KeySearchCategory.Media);

            picker.SelectCategoryCommand.Execute(chip);

            var rows = picker.Rows;

            picker.SelectRowCommand.Execute(picker.Rows[^1]);
            picker.Clear();

            Assert.Same(rows, picker.Rows);
            Assert.True(chip.IsSelected);
            Assert.Same(picker.Rows[0], picker.SelectedRow);
        }

        [Fact]
        public void Clear_EmptiesTheQueryAndTheHighlight_ButNotTheChip()
        {
            var picker = Create();
            var chip = picker.Categories.Single(candidate => !candidate.IsRecent && candidate.Category == KeySearchCategory.Media);

            picker.SelectCategoryCommand.Execute(chip);
            picker.Query = "vol+";

            picker.Clear();

            Assert.Equal(string.Empty, picker.Query);
            Assert.True(chip.IsSelected);

            // A chip is itself a narrowing, so the first row of what it leaves is highlighted again
            // — the highlight the user chose is what went away, not the filter.
            Assert.Same(picker.Rows[0], picker.SelectedRow);
        }

        [Fact]
        public void ANarrowRow_DropsMouseAndHotkeys_AndKeepsTheOtherFive()
        {
            // Mockup 2a: "filter chips reduced at this width to All, Letters, Nav, Media, Recent".
            var picker = Create();

            picker.SetAvailableWidth(TokenPickerViewModel.NarrowWidth - 1);

            Assert.True(picker.IsNarrow);
            Assert.Equal(
                new[]
                {
                    TokenPickerCategoryViewModel.AllCaption,
                    TokenPickerCategoryViewModel.LettersCaption,
                    TokenPickerCategoryViewModel.NavCaption,
                    TokenPickerCategoryViewModel.MediaCaption,
                    TokenPickerCategoryViewModel.RecentCaption
                },
                picker.Categories.Where(chip => chip.IsShown).Select(chip => chip.Caption));
        }

        [Fact]
        public void AWideRow_KeepsAllSeven()
        {
            var picker = Create();

            picker.SetAvailableWidth(TokenPickerViewModel.NarrowWidth + 1);

            Assert.False(picker.IsNarrow);
            Assert.All(picker.Categories, chip => Assert.True(chip.IsShown));
        }

        [Fact]
        public void NarrowingWhileAHiddenChipIsLive_FallsBackToAll()
        {
            // A filter the user can no longer see is a filter they cannot undo.
            var picker = Create();

            picker.SelectCategoryCommand.Execute(
                picker.Categories.Single(chip => !chip.IsRecent && chip.Category == KeySearchCategory.Hotkeys));

            picker.SetAvailableWidth(TokenPickerViewModel.NarrowWidth - 1);

            Assert.True(picker.Categories[0].IsSelected);
            Assert.Equal(picker.TotalCount, picker.MatchCount);
        }

        [Fact]
        public void AnUnarrangedControl_DoesNotNarrowThePicker()
        {
            var picker = Create();

            picker.SetAvailableWidth(0);
            picker.SetAvailableWidth(double.NaN);

            Assert.False(picker.IsNarrow);
        }

        [Fact]
        public void RequestFocus_RaisesTheRequestAndLeavesItPendingUntilAViewAnswers()
        {
            var picker = Create();
            var raised = 0;

            picker.FocusRequested += (_, _) => raised++;

            Assert.False(picker.IsFocusPending);

            picker.RequestFocus();

            Assert.Equal(1, raised);
            Assert.True(picker.IsFocusPending);

            picker.ClearPendingFocus();

            Assert.False(picker.IsFocusPending);
        }

        private static TokenPickerCategoryViewModel RecentChip(TokenPickerViewModel picker)
        {
            return picker.Categories.Single(chip => chip.IsRecent);
        }

        private static TokenPickerViewModel Create()
        {
            return new TokenPickerViewModel(TokenDialect.Gen1);
        }
    }
}
