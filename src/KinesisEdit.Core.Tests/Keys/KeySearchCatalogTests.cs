using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Keys
{
    /// <summary>
    /// The Search Keys list of specs/11-feature-dialogs.md §11.6: every assignable action of a
    /// dialect minus the non-searchable entries, the three-part item text
    /// ("search name, plus its display text when different, plus <c>' (' + token + ')'</c> when
    /// the display text differs from the layout-file token"), and the incremental
    /// case-insensitive filter over name and file token.
    /// </summary>
    public sealed class KeySearchCatalogTests
    {
        [Fact]
        public void Build_WithGen1Dialect_ListsTheAssignableActionsOfThatDialect()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.NotEmpty(entries);
            Assert.All(entries, entry => Assert.NotEqual(0, entry.FileToken.Length));
            Assert.All(entries, entry => Assert.Equal(entry.FileToken, entry.Definition.Gen1Token));
            Assert.Contains(entries, entry => entry.FileToken == "a");
            Assert.Contains(entries, entry => entry.FileToken == "lshft");
        }

        [Fact]
        public void Build_WithGen1Dialect_SkipsEntriesFlaggedHiddenFromSearch()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.All(
                entries,
                entry => Assert.Equal(
                    KeyDefinitionFlags.None,
                    entry.Definition.Flags & KeyDefinitionFlags.HiddenFromSearch));

            // The §3.12 delay/speed pseudo-keys, the §3.13 edge zones, and the Fn action are the
            // entries the key table flags today.
            Assert.DoesNotContain(entries, entry => entry.Definition.Table == KeyTable.MacroTiming);
            Assert.DoesNotContain(entries, entry => entry.Definition.Table == KeyTable.EdgeZones);
            Assert.DoesNotContain(entries, entry => entry.FileToken == "dran");
            Assert.DoesNotContain(entries, entry => entry.Definition.Code == 10017);
        }

        [Fact]
        public void Build_WithGen1Dialect_SkipsEntriesThatDialectDoesNotName()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            // The generic modifiers and the Program button exist only in the Legacy table / in no
            // table at all, so they can never be written to a Gen1 file.
            Assert.DoesNotContain(entries, entry => entry.FileToken == "shift");
            Assert.DoesNotContain(entries, entry => entry.Definition.Code == 10013);
            Assert.Contains(KeySearchCatalog.Build(TokenDialect.Legacy), entry => entry.FileToken == "shift");
        }

        [Fact]
        public void Build_WhenTokenMatchesTheDisplayText_ComposesTheNameAlone()
        {
            var entry = Single(TokenDialect.Gen1, "a");

            Assert.Equal("A", entry.SearchName);
            Assert.Equal("A", entry.DisplayText);
        }

        [Fact]
        public void Build_WhenTheTokenDiffersFromTheDisplayText_AppendsTheTokenInParentheses()
        {
            var entry = Single(TokenDialect.Gen1, "lshft");

            Assert.Equal("Left Shift", entry.SearchName);
            Assert.Equal("Left Shift (lshft)", entry.DisplayText);
        }

        [Fact]
        public void Build_WhenTheDialectCaptionDiffers_AppendsTheDisplayText()
        {
            var entry = First(TokenDialect.Gen2, "app");

            Assert.Equal("PC Menu", entry.SearchName);
            Assert.Equal("PC Menu App", entry.DisplayText);
        }

        [Fact]
        public void Build_WhenName_DisplayTextAndTokenAllDiffer_AppendsBoth()
        {
            var entry = First(TokenDialect.Legacy, "1");

            Assert.Equal("1 !", entry.SearchName);
            Assert.Equal("1 ! ! 1 (1)", entry.DisplayText);
        }

        [Fact]
        public void Build_WithABlankCaption_FallsBackToTheFileToken()
        {
            var entry = Single(TokenDialect.Gen1, "hk1");

            Assert.Equal("hk1", entry.SearchName);
            Assert.Equal("hk1", entry.DisplayText);
        }

        [Fact]
        public void Build_WithNoDialect_ListsEveryActionUnderItsFirstToken()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.None);

            Assert.Contains(entries, entry => entry.FileToken == "escape");
            Assert.Contains(entries, entry => entry.FileToken == "lwin");
            Assert.All(entries, entry => Assert.NotEqual(0, entry.FileToken.Length));
        }

        [Fact]
        public void Filter_WithNullOrBlankQuery_ReturnsEveryEntry()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.Same(entries, KeySearchCatalog.Filter(entries, null));
            Assert.Same(entries, KeySearchCatalog.Filter(entries, string.Empty));
            Assert.Same(entries, KeySearchCatalog.Filter(entries, "   "));
        }

        [Fact]
        public void Filter_ByDisplayName_MatchesCaseInsensitively()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);
            var matches = KeySearchCatalog.Filter(entries, "left shift");

            Assert.Contains(matches, entry => entry.FileToken == "lshft");
            Assert.DoesNotContain(matches, entry => entry.FileToken == "rshft");
        }

        [Fact]
        public void Filter_ByFileToken_MatchesTheEntryWhoseNameDoesNotContainIt()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);
            var matches = KeySearchCatalog.Filter(entries, "LSHFT");

            Assert.Contains(matches, entry => entry.FileToken == "lshft");
        }

        [Fact]
        public void Filter_Incrementally_NarrowsTheResultSet()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            var wide = KeySearchCatalog.Filter(entries, "shif");
            var narrow = KeySearchCatalog.Filter(entries, "left shif");

            Assert.NotEmpty(narrow);
            Assert.True(narrow.Count < wide.Count);
            Assert.All(narrow, entry => Assert.Contains(entry, wide));
        }

        [Fact]
        public void Filter_WithAQueryNothingMatches_ReturnsEmpty()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.Empty(KeySearchCatalog.Filter(entries, "no-such-key"));
        }

        private static KeySearchEntry Single(TokenDialect dialect, string token)
        {
            return KeySearchCatalog.Build(dialect).Single(entry => entry.FileToken == token);
        }

        private static KeySearchEntry First(TokenDialect dialect, string token)
        {
            return KeySearchCatalog.Build(dialect).First(entry => entry.FileToken == token);
        }
    }
}
