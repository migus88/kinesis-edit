using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Keys
{
    /// <summary>
    /// The category chips over the token picker (docs/design/mockups.md §1e): the mapping from the
    /// specs/05-key-model.md §3 sub-tables onto <see cref="KeySearchCategory"/>, and the filter
    /// the chips drive. The load-bearing property is that the mapping is total — the tables no
    /// chip names fall to <see cref="KeySearchCategory.Other"/> and stay reachable, so §11.6's
    /// flat list of every assignable action is never cut down to make the chips tidy.
    /// </summary>
    public sealed class KeySearchCategoryTests
    {
        [Theory]
        [InlineData(KeyTable.LettersAndDigits, KeySearchCategory.Letters)]
        [InlineData(KeyTable.Navigation, KeySearchCategory.Nav)]
        [InlineData(KeyTable.MediaKeys, KeySearchCategory.Media)]
        [InlineData(KeyTable.MouseActions, KeySearchCategory.Mouse)]
        [InlineData(KeyTable.ProfilesAndHotkeys, KeySearchCategory.Hotkeys)]
        [InlineData(KeyTable.Punctuation, KeySearchCategory.Other)]
        [InlineData(KeyTable.FunctionKeys, KeySearchCategory.Other)]
        [InlineData(KeyTable.Modifiers, KeySearchCategory.Other)]
        [InlineData(KeyTable.KeypadKeys, KeySearchCategory.Other)]
        [InlineData(KeyTable.SpecialActions, KeySearchCategory.Other)]
        [InlineData(KeyTable.LayerKeys, KeySearchCategory.Other)]
        [InlineData(KeyTable.MacroTiming, KeySearchCategory.Other)]
        [InlineData(KeyTable.EdgeZones, KeySearchCategory.Other)]
        [InlineData(KeyTable.None, KeySearchCategory.Other)]
        public void CategoryFor_MapsEverySubTableOntoAChipOrOther(KeyTable table, KeySearchCategory expected)
        {
            Assert.Equal(expected, KeySearchCatalog.CategoryFor(table));
        }

        [Fact]
        public void CategoryFor_NeverAnswersAll()
        {
            // 'All' is a filter value, not a row's category: a row that carried it would be
            // invisible to every chip but the first, which is the regression this guards.
            Assert.All(
                Enum.GetValues<KeyTable>(),
                table => Assert.NotEqual(KeySearchCategory.All, KeySearchCatalog.CategoryFor(table)));
        }

        [Fact]
        public void CategoryFor_WithADefinition_ReadsItsSubTable()
        {
            var escape = KeyRegistry.FindByToken("esc", TokenDialect.Gen1);

            Assert.NotNull(escape);
            Assert.Equal(KeySearchCategory.Nav, KeySearchCatalog.CategoryFor(escape));
        }

        [Fact]
        public void CategoryFor_WithNullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => KeySearchCatalog.CategoryFor((KeyDefinition)null!));
        }

        [Fact]
        public void Build_GivesEveryRowAConcreteCategory()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.All(entries, entry => Assert.NotEqual(KeySearchCategory.All, entry.Category));
            Assert.All(
                entries,
                entry => Assert.Equal(KeySearchCatalog.CategoryFor(entry.Definition), entry.Category));
        }

        [Fact]
        public void Filter_WithAll_ReturnsTheInputUnchanged()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.Same(entries, KeySearchCatalog.Filter(entries, KeySearchCategory.All));
        }

        [Fact]
        public void Filter_ByCategory_KeepsOnlyThatCategorysRows()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);
            var media = KeySearchCatalog.Filter(entries, KeySearchCategory.Media);

            Assert.NotEmpty(media);
            Assert.All(media, entry => Assert.Equal(KeySearchCategory.Media, entry.Category));
            Assert.Contains(media, entry => entry.FileToken == "mute");
            Assert.DoesNotContain(media, entry => entry.FileToken == "a");
        }

        [Fact]
        public void Filter_ByEveryChip_PartitionsTheListWithNothingUnreachable()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);
            var reached = 0;

            foreach (var category in Chips)
            {
                var rows = KeySearchCatalog.Filter(entries, category);

                Assert.All(rows, entry => Assert.Equal(category, entry.Category));
                reached += rows.Count;
            }

            Assert.Equal(entries.Count, reached);
        }

        [Fact]
        public void Filter_ByOther_KeepsTheTablesNoChipNamesReachable()
        {
            var other = KeySearchCatalog.Filter(
                KeySearchCatalog.Build(TokenDialect.Gen1),
                KeySearchCategory.Other);
            var tables = other.Select(entry => entry.Definition.Table).Distinct().ToList();

            Assert.Contains(KeyTable.Punctuation, tables);
            Assert.Contains(KeyTable.FunctionKeys, tables);
            Assert.Contains(KeyTable.Modifiers, tables);
            Assert.Contains(KeyTable.KeypadKeys, tables);
            Assert.Contains(KeyTable.SpecialActions, tables);
            Assert.Contains(KeyTable.LayerKeys, tables);
            Assert.Contains(other, entry => entry.FileToken == "lshft");
            Assert.Contains(other, entry => entry.FileToken == "F1");
        }

        [Fact]
        public void Filter_ByCategory_ComposesWithTheQueryFilterInEitherOrder()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            var queryThenChip = KeySearchCatalog.Filter(
                KeySearchCatalog.Filter(entries, "vol"),
                KeySearchCategory.Media);
            var chipThenQuery = KeySearchCatalog.Filter(
                KeySearchCatalog.Filter(entries, KeySearchCategory.Media),
                "vol");

            Assert.NotEmpty(queryThenChip);
            Assert.Equal(queryThenChip, chipThenQuery);
        }

        [Fact]
        public void Filter_ByAChipWithNoRowsInTheDialect_ReturnsEmpty()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);
            var letters = KeySearchCatalog.Filter(entries, KeySearchCategory.Letters);

            Assert.Empty(KeySearchCatalog.Filter(letters, KeySearchCategory.Mouse));
        }

        [Fact]
        public void Filter_ByCategory_WithNullEntries_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => KeySearchCatalog.Filter(null!, KeySearchCategory.Media));
        }

        private static KeySearchCategory[] Chips =>
            new[]
            {
                KeySearchCategory.Letters,
                KeySearchCategory.Nav,
                KeySearchCategory.Media,
                KeySearchCategory.Mouse,
                KeySearchCategory.Hotkeys,
                KeySearchCategory.Other
            };
    }
}
