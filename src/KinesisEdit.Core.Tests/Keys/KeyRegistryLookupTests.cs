using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Keys
{
    /// <summary>
    /// Asserts the first-match lookup semantics of specs/05-key-model.md §1.5/§7: duplicated
    /// codes and tokens always resolve to the earliest registration in spec table order.
    /// </summary>
    public class KeyRegistryLookupTests
    {
        [Fact]
        public void FindByCode_WithNumLockCode_ReturnsNavigationEntryNotKeypadDuplicate()
        {
            var entry = KeyRegistry.FindByCode(0x90);

            Assert.NotNull(entry);
            Assert.Equal(KeyTable.Navigation, entry.Table);
            Assert.Equal("numlk", entry.LegacyToken);

            var keypadDuplicate = KeyRegistry.Entries.Single(candidate => candidate.Code == 10052);

            Assert.Equal(KeyTable.KeypadKeys, keypadDuplicate.Table);
        }

        [Fact]
        public void FindByCode_WithCode10087_ReturnsRandomDelayNotGeneratedD002()
        {
            var entry = KeyRegistry.FindByCode(10087);

            Assert.NotNull(entry);
            Assert.Equal("dran", entry.LegacyToken);

            var duplicates = KeyRegistry.Entries.Where(candidate => candidate.Code == 10087).ToList();

            Assert.Equal(2, duplicates.Count);
            Assert.Equal("dran", duplicates[0].LegacyToken);
            Assert.Equal("d002", duplicates[1].LegacyToken);
        }

        [Fact]
        public void FindByCode_WithUnknownCode_ReturnsNull()
        {
            Assert.Null(KeyRegistry.FindByCode(-1));
            Assert.Null(KeyRegistry.FindByCode(65535));
        }

        [Fact]
        public void FindByToken_WithNumLockToken_ReturnsNavigationEntryFirst()
        {
            var entry = KeyRegistry.FindByToken("numlk");

            Assert.NotNull(entry);
            Assert.Equal(0x90, entry.Code);
            Assert.Equal(KeyTable.Navigation, entry.Table);
        }

        [Theory]
        [InlineData(TokenDialect.Legacy, "numlk", 0x90)]
        [InlineData(TokenDialect.Gen1, "numlk", 0x90)]
        [InlineData(TokenDialect.Gen2, "nmlk", 0x90)]
        public void FindByToken_WithDialectScopedNumLock_ReturnsNavigationEntryFirst(
            TokenDialect dialect,
            string token,
            int expectedCode)
        {
            var entry = KeyRegistry.FindByToken(token, dialect);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
        }

        [Fact]
        public void FindByToken_WithKpshftToken_ReturnsAdvantage2KeypadEntryFirst()
        {
            var entry = KeyRegistry.FindByToken("kpshft");

            Assert.NotNull(entry);
            Assert.Equal(10048, entry.Code);
            Assert.Equal(KeyTable.KeypadKeys, entry.Table);
        }

        [Theory]
        [InlineData(TokenDialect.Legacy, 10048)]
        [InlineData(TokenDialect.Gen1, 10016)]
        [InlineData(TokenDialect.Gen2, 10016)]
        public void FindByToken_WithDialectScopedKpshft_ReturnsFirstEntryWithTokenInThatDialect(
            TokenDialect dialect,
            int expectedCode)
        {
            var entry = KeyRegistry.FindByToken("kpshft", dialect);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
        }

        [Fact]
        public void FindByToken_WithPlayToken_ReturnsPlayPauseEntryFirst()
        {
            var entry = KeyRegistry.FindByToken("play");

            Assert.NotNull(entry);
            Assert.Equal(0xB3, entry.Code);
            Assert.Equal("Play\nPause", entry.DisplayText);
        }

        [Theory]
        [InlineData("play", TokenDialect.Legacy, 0xB3)]
        [InlineData("play", TokenDialect.Gen1, 0xB3)]
        [InlineData("play", TokenDialect.Gen2, 11151)]
        [InlineData("plpa", TokenDialect.Gen2, 0xB3)]
        public void FindByToken_WithDialectScopedPlay_ReturnsFirstEntryWithTokenInThatDialect(
            string token,
            TokenDialect dialect,
            int expectedCode)
        {
            var entry = KeyRegistry.FindByToken(token, dialect);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
        }

        [Theory]
        [InlineData("prtscr", 0x2C)]
        [InlineData("calc", 10009)]
        [InlineData("menu", 0x5D)]
        [InlineData("lwin", 0x5B)]
        [InlineData("d125", 10007)]
        [InlineData("d500", 10008)]
        public void FindByToken_WithDuplicatedToken_ReturnsFirstRegistrationInSpecOrder(string token, int expectedCode)
        {
            var entry = KeyRegistry.FindByToken(token);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
        }

        [Theory]
        [InlineData("prtscr", 0x2A)]
        [InlineData("calc", 10047)]
        [InlineData("menu", 10043)]
        [InlineData("d125", 10210)]
        [InlineData("d500", 10585)]
        public void Entries_WithDuplicatedToken_StillContainsLaterRegistration(string token, int duplicateCode)
        {
            var duplicate = KeyRegistry.Entries.Single(candidate => candidate.Code == duplicateCode);

            Assert.Equal(token, duplicate.LegacyToken, ignoreCase: true);
        }

        [Theory]
        [InlineData("kpdiv", TokenDialect.Legacy, 0x6F)]
        [InlineData("kp0", TokenDialect.Legacy, 0x60)]
        [InlineData("kp9", TokenDialect.Legacy, 0x69)]
        public void FindByToken_WithLegacyKeypadToken_ReturnsStandardKeyBeforeKeypadLayerDuplicate(
            string token,
            TokenDialect dialect,
            int expectedCode)
        {
            var entry = KeyRegistry.FindByToken(token, dialect);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
        }

        [Theory]
        [InlineData("NUMLK", 0x90)]
        [InlineData("Numlk", 0x90)]
        [InlineData("f1", 0x70)]
        [InlineData("F1", 0x70)]
        [InlineData("f13", 0x7C)]
        [InlineData("led", 10022)]
        [InlineData("fn", 10017)]
        [InlineData("ESCAPE", 0x1B)]
        public void FindByToken_WithAnyCasing_ReturnsMatchingEntry(string token, int expectedCode)
        {
            var entry = KeyRegistry.FindByToken(token);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
        }

        [Fact]
        public void FindByToken_WithLowercaseQuery_ReturnsEntryKeepingCanonicalCasing()
        {
            var functionKey = KeyRegistry.FindByToken("f13");
            var ledKey = KeyRegistry.FindByToken("led", TokenDialect.Gen1);
            var fnKey = KeyRegistry.FindByToken("fn");

            Assert.NotNull(functionKey);
            Assert.NotNull(ledKey);
            Assert.NotNull(fnKey);
            Assert.Equal("F13", functionKey.Gen1Token);
            Assert.Equal("LED", ledKey.Gen1Token);
            Assert.Equal("Fn", fnKey.LegacyToken);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void FindByToken_WithEmptyOrWhitespaceQuery_ReturnsNull(string? token)
        {
            Assert.Null(KeyRegistry.FindByToken(token));
            Assert.Null(KeyRegistry.FindByToken(token, TokenDialect.Legacy));
            Assert.Null(KeyRegistry.FindByToken(token, TokenDialect.Gen1));
            Assert.Null(KeyRegistry.FindByToken(token, TokenDialect.Gen2));
        }

        [Fact]
        public void FindByToken_WithUnknownToken_ReturnsNull()
        {
            Assert.Null(KeyRegistry.FindByToken("no-such-token"));
            Assert.Null(KeyRegistry.FindByToken("no-such-token", TokenDialect.Gen2));
        }

        [Theory]
        [InlineData("mous4", TokenDialect.Legacy)]
        [InlineData("sumo", TokenDialect.Gen1)]
        [InlineData("eql", TokenDialect.Legacy)]
        [InlineData("shift", TokenDialect.Gen1)]
        public void FindByToken_WithTokenFromAnotherDialect_ReturnsNull(string token, TokenDialect dialect)
        {
            Assert.Null(KeyRegistry.FindByToken(token, dialect));
        }

        [Fact]
        public void FindByToken_WithGen1OnlyMouseButton_ReflectsAvailabilityMask()
        {
            var entry = KeyRegistry.FindByToken("mous4");

            Assert.NotNull(entry);
            Assert.Equal(10036, entry.Code);
            Assert.False(entry.IsAvailableIn(TokenDialect.Legacy));
            Assert.True(entry.IsAvailableIn(TokenDialect.Gen1));
            Assert.True(entry.IsAvailableIn(TokenDialect.Gen2));
            Assert.Equal("", entry.GetToken(TokenDialect.Legacy));
            Assert.Equal("mous4", entry.GetToken(TokenDialect.Gen1));
            Assert.Equal("4mou", entry.GetToken(TokenDialect.Gen2));
        }

        [Fact]
        public void FindByToken_WithEqualsSign_ReturnsPunctuationEntryBeforeKeypadEquals()
        {
            var entry = KeyRegistry.FindByToken("=");

            Assert.NotNull(entry);
            Assert.Equal(0xBB, entry.Code);
            Assert.Equal(KeyTable.Punctuation, entry.Table);

            var keypadEquals = KeyRegistry.FindByToken("kp=");

            Assert.NotNull(keypadEquals);
            Assert.Equal(10053, keypadEquals.Code);
        }
    }
}
