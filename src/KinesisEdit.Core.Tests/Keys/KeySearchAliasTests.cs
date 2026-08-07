using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Keys
{
    /// <summary>
    /// Alias derivation over the searchable token list: the token picker lists the duplicate
    /// registrations of specs/05-key-model.md §7 rather than hiding them, and labels the later
    /// ones. The rule is the registry's own first-match semantics — same numeric code, or same
    /// file token in the catalog's dialect, earliest registration canonical — with
    /// <see cref="KeyTable.MacroTiming"/> excluded because its code collisions are an artefact of
    /// the code space, not two names for one action.
    /// </summary>
    public sealed class KeySearchAliasTests
    {
        [Fact]
        public void Build_NamesTheFirstRegistrationCanonicalAndTheRestAliases()
        {
            var rows = Rows(TokenDialect.Gen1, "numlk");

            Assert.Equal(2, rows.Count);
            Assert.False(rows[0].IsAlias);
            Assert.Null(rows[0].AliasOf);
            Assert.Equal(KeyTable.Navigation, rows[0].Definition.Table);
            Assert.True(rows[1].IsAlias);
            Assert.Same(rows[0], rows[1].AliasOf);
            Assert.Equal(10052, rows[1].Definition.Code);
        }

        [Fact]
        public void Build_LinksTheKeypadLayerDuplicatesToTheStandardKeypadRow()
        {
            var rows = Rows(TokenDialect.Legacy, "kp0");

            Assert.Equal(2, rows.Count);
            Assert.False(rows[0].IsAlias);
            Assert.Equal(10056, rows[1].Definition.Code);
            Assert.Same(rows[0], rows[1].AliasOf);
            Assert.Equal(KeySearchScope.Keypad, rows[1].Scope);
        }

        [Fact]
        public void Build_LinksSeveralAliasesOfOneActionToTheSameCanonicalRow()
        {
            var rows = Rows(TokenDialect.None, "play");

            Assert.Equal(3, rows.Count);
            Assert.False(rows[0].IsAlias);
            Assert.Same(rows[0], rows[1].AliasOf);
            Assert.Same(rows[0], rows[2].AliasOf);
        }

        [Fact]
        public void Build_ResolvesTheCanonicalRowPerDialect()
        {
            // Gen2 names VK_MEDIA_PLAY_PAUSE 'plpa', so the first row that writes 'play' there is
            // the Gen2-only 11151 entry rather than the play/pause key that owns it elsewhere.
            var gen1 = Rows(TokenDialect.Gen1, "play");
            var gen2 = Rows(TokenDialect.Gen2, "play");

            Assert.Equal("plpa", gen1[0].Definition.Gen2Token);
            Assert.Equal(11151, gen2[0].Definition.Code);
            Assert.NotEqual(gen1[0].Definition.Code, gen2[0].Definition.Code);
            Assert.All(gen2.Skip(1), row => Assert.Same(gen2[0], row.AliasOf));
        }

        [Fact]
        public void Build_ResolvesEveryAliasToWhatTheRegistryWouldAnswer()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);
            var aliases = entries.Where(entry => entry.IsAlias).ToList();

            Assert.NotEmpty(aliases);
            Assert.All(
                aliases,
                alias => Assert.Equal(
                    KeyRegistry.FindByToken(alias.FileToken, TokenDialect.Gen1),
                    alias.AliasOf!.Definition));
        }

        [Fact]
        public void Build_LeavesAnActionWithOneRegistrationCanonical()
        {
            var entries = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.False(entries.Single(entry => entry.FileToken == "esc").IsAlias);
            Assert.False(entries.Single(entry => entry.FileToken == "a").IsAlias);
            Assert.False(entries.Single(entry => entry.FileToken == "lshft").IsAlias);
        }

        [Fact]
        public void Build_NeverChainsOneAliasOntoAnother()
        {
            foreach (var dialect in Dialects)
            {
                Assert.All(
                    KeySearchCatalog.Build(dialect),
                    entry => Assert.Null(entry.AliasOf?.AliasOf));
            }
        }

        [Fact]
        public void Build_OnTheShippedKeyTable_GivesEveryAliasTheCanonicalRowsToken()
        {
            // Every alias in the shipped table is a duplicate *registration* (05 §7): the two rows
            // write the identical token and differ only in numeric code. The mockups' pairing of
            // [escape] with "alias of [esc]" is not reproducible — those are the Legacy and Gen1
            // tokens of one entry, so no catalog ever lists both.
            foreach (var dialect in Dialects)
            {
                Assert.All(
                    KeySearchCatalog.Build(dialect).Where(entry => entry.IsAlias),
                    entry => Assert.Equal(entry.AliasOf!.FileToken, entry.FileToken));
            }
        }

        [Fact]
        public void Build_ListsEscapeUnderOneTokenPerDialect()
        {
            var legacy = KeySearchCatalog.Build(TokenDialect.Legacy);
            var gen1 = KeySearchCatalog.Build(TokenDialect.Gen1);

            Assert.Equal(1, legacy.Count(entry => entry.FileToken == "escape"));
            Assert.Equal(0, legacy.Count(entry => entry.FileToken == "esc"));
            Assert.Equal(1, gen1.Count(entry => entry.FileToken == "esc"));
            Assert.Equal(0, gen1.Count(entry => entry.FileToken == "escape"));
        }

        [Fact]
        public void IsAliasable_RejectsTheMacroTimingTable()
        {
            var timing = KeyRegistry.Entries.Where(entry => entry.Table == KeyTable.MacroTiming).ToList();

            Assert.NotEmpty(timing);
            Assert.All(timing, entry => Assert.False(KeySearchCatalog.IsAliasable(entry)));
        }

        [Fact]
        public void IsAliasable_AcceptsEveryOtherTable()
        {
            Assert.All(
                KeyRegistry.Entries.Where(entry => entry.Table != KeyTable.MacroTiming),
                entry => Assert.True(KeySearchCatalog.IsAliasable(entry)));
        }

        [Fact]
        public void LinkAliases_NeverCallsTheGeneratedDelayAnAliasOfTheRandomDelay()
        {
            var random = KeyRegistry.Entries.Single(
                entry => entry.Table == KeyTable.MacroTiming && entry.Gen1Token == "dran");
            var twoMilliseconds = KeyRegistry.Entries.Single(
                entry => entry.Table == KeyTable.MacroTiming && entry.Gen1Token == "d002");

            // The hazard is real: these two rows share code 10087, and the registry answers the
            // random delay for it. Grouping them would report a 2 ms delay as a random one.
            Assert.Equal(10087, random.Code);
            Assert.Equal(random.Code, twoMilliseconds.Code);
            Assert.Equal(random, KeyRegistry.FindByCode(10087));

            var rows = KeySearchCatalog.LinkAliases(new[] { Row(random), Row(twoMilliseconds) });

            Assert.All(rows, row => Assert.False(row.IsAlias));
        }

        [Fact]
        public void LinkAliases_LinksTwoRowsThatShareACode()
        {
            var first = Definition(10500, "aaa");
            var second = Definition(10500, "bbb");

            var rows = KeySearchCatalog.LinkAliases(new[] { Row(first), Row(second) });

            Assert.False(rows[0].IsAlias);
            Assert.Same(rows[0], rows[1].AliasOf);
        }

        [Fact]
        public void LinkAliases_CollapsesRowsBridgedByACodeAndAToken()
        {
            var first = Definition(10500, "aaa");
            var second = Definition(10500, "bbb");
            var third = Definition(10501, "bbb");

            var rows = KeySearchCatalog.LinkAliases(new[] { Row(first), Row(second), Row(third) });

            Assert.False(rows[0].IsAlias);
            Assert.Same(rows[0], rows[1].AliasOf);
            Assert.Same(rows[0], rows[2].AliasOf);
        }

        [Fact]
        public void LinkAliases_MatchesTokensCaseInsensitively()
        {
            var first = Definition(10500, "aaa");
            var second = Definition(10501, "AAA");

            var rows = KeySearchCatalog.LinkAliases(new[] { Row(first), Row(second) });

            Assert.Same(rows[0], rows[1].AliasOf);
        }

        [Fact]
        public void LinkAliases_LeavesAListWithNoDuplicatesAlone()
        {
            var rows = KeySearchCatalog.LinkAliases(
                new[] { Row(Definition(10500, "aaa")), Row(Definition(10501, "bbb")) });

            Assert.All(rows, row => Assert.False(row.IsAlias));
        }

        [Fact]
        public void LinkAliases_IsIdempotent()
        {
            var once = KeySearchCatalog.Build(TokenDialect.Gen1);
            var twice = KeySearchCatalog.LinkAliases(once);

            Assert.Equal(once.Select(entry => entry.IsAlias), twice.Select(entry => entry.IsAlias));
            Assert.Equal(
                once.Select(entry => entry.AliasOf?.Definition),
                twice.Select(entry => entry.AliasOf?.Definition));
        }

        [Fact]
        public void LinkAliases_WithNullEntries_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => KeySearchCatalog.LinkAliases(null!));
        }

        [Fact]
        public void IsAliasable_WithNullDefinition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => KeySearchCatalog.IsAliasable(null!));
        }

        private static TokenDialect[] Dialects =>
            new[] { TokenDialect.None, TokenDialect.Legacy, TokenDialect.Gen1, TokenDialect.Gen2 };

        private static List<KeySearchEntry> Rows(TokenDialect dialect, string token)
        {
            return KeySearchCatalog.Build(dialect).Where(entry => entry.FileToken == token).ToList();
        }

        private static KeyDefinition Definition(int code, string token)
        {
            return new KeyDefinition
            {
                Code = code,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                LegacyToken = token,
                Gen1Token = token,
                Gen2Token = token
            };
        }

        private static KeySearchEntry Row(KeyDefinition definition)
        {
            return new KeySearchEntry(definition, definition.Gen1Token, definition.Gen1Token, definition.Gen1Token);
        }
    }
}
