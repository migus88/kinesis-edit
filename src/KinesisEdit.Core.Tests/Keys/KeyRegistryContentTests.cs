using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Keys
{
    /// <summary>
    /// Asserts the registry content against specs/05-key-model.md §3: per-table entry counts,
    /// spec-order grouping, generated ranges, canonical casing, and the §3.14 pedal tokens.
    /// </summary>
    public class KeyRegistryContentTests
    {
        [Theory]
        [InlineData(KeyTable.LettersAndDigits, 36)]
        [InlineData(KeyTable.Punctuation, 12)]
        [InlineData(KeyTable.Navigation, 24)]
        [InlineData(KeyTable.FunctionKeys, 24)]
        [InlineData(KeyTable.Modifiers, 14)]
        [InlineData(KeyTable.KeypadKeys, 36)]
        [InlineData(KeyTable.MediaKeys, 21)]
        [InlineData(KeyTable.MouseActions, 11)]
        [InlineData(KeyTable.SpecialActions, 22)]
        [InlineData(KeyTable.LayerKeys, 14)]
        [InlineData(KeyTable.ProfilesAndHotkeys, 21)]
        [InlineData(KeyTable.MacroTiming, 1014)]
        [InlineData(KeyTable.EdgeZones, 33)]
        public void Entries_WithSpecTable_ContainsExpectedEntryCount(KeyTable table, int expectedCount)
        {
            var count = KeyRegistry.Entries.Count(entry => entry.Table == table);

            Assert.Equal(expectedCount, count);
        }

        [Fact]
        public void Entries_WithAllTables_ContainsExpectedTotalCount()
        {
            Assert.Equal(1282, KeyRegistry.Entries.Count);
        }

        [Fact]
        public void Entries_WithSpecOrdering_GroupsTablesInSpecOrder()
        {
            var groupedTables = new List<KeyTable>();

            foreach (var entry in KeyRegistry.Entries)
            {
                if (groupedTables.Count == 0 || groupedTables[^1] != entry.Table)
                {
                    groupedTables.Add(entry.Table);
                }
            }

            var expectedOrder = new[]
            {
                KeyTable.LettersAndDigits,
                KeyTable.Punctuation,
                KeyTable.Navigation,
                KeyTable.FunctionKeys,
                KeyTable.Modifiers,
                KeyTable.KeypadKeys,
                KeyTable.MediaKeys,
                KeyTable.MouseActions,
                KeyTable.SpecialActions,
                KeyTable.LayerKeys,
                KeyTable.ProfilesAndHotkeys,
                KeyTable.MacroTiming,
                KeyTable.EdgeZones
            };

            Assert.Equal(expectedOrder, groupedTables);
        }

        [Theory]
        [InlineData("d001", 10086)]
        [InlineData("d042", 10127)]
        [InlineData("d087", 10172)]
        [InlineData("d999", 11084)]
        public void FindByToken_WithGeneratedDelayToken_ReturnsCodeOffsetFrom10085(string token, int expectedCode)
        {
            var entry = KeyRegistry.FindByToken(token);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
            Assert.Equal(KeyTable.MacroTiming, entry.Table);
        }

        [Fact]
        public void Entries_WithGeneratedDelayRange_Contains999ZeroPaddedDelays()
        {
            var delays = KeyRegistry.Entries
                .Where(entry => entry.Code is >= 10086 and <= 11084 && entry.LegacyToken != "dran")
                .ToList();

            Assert.Equal(999, delays.Count);
            Assert.All(delays, entry => Assert.Matches("^d[0-9]{3}$", entry.LegacyToken));
            Assert.Equal("d007", delays.Single(entry => entry.Code == 10092).LegacyToken);
        }

        [Theory]
        [InlineData("d42")]
        [InlineData("d1000")]
        [InlineData("d0")]
        public void FindByToken_WithUnpaddedOrOutOfRangeDelay_ReturnsNull(string token)
        {
            Assert.Null(KeyRegistry.FindByToken(token));
        }

        [Fact]
        public void Entries_WithMacroTimingTable_MarksEveryEntryHiddenAndSingleEvent()
        {
            var timingEntries = KeyRegistry.Entries.Where(entry => entry.Table == KeyTable.MacroTiming);

            Assert.All(timingEntries, entry =>
            {
                Assert.True(entry.Flags.HasFlag(KeyDefinitionFlags.HiddenFromSearch));
                Assert.True(entry.Flags.HasFlag(KeyDefinitionFlags.SingleEvent));
            });
        }

        [Theory]
        [InlineData("L1", 11113)]
        [InlineData("L9", 11121)]
        [InlineData("B1", 11122)]
        [InlineData("B15", 11136)]
        [InlineData("R1", 11137)]
        [InlineData("R9", 11145)]
        public void FindByToken_WithEdgeZoneToken_ReturnsSpecCode(string token, int expectedCode)
        {
            var entry = KeyRegistry.FindByToken(token);

            Assert.NotNull(entry);
            Assert.Equal(expectedCode, entry.Code);
            Assert.Equal(KeyTable.EdgeZones, entry.Table);
            Assert.True(entry.Flags.HasFlag(KeyDefinitionFlags.HiddenFromSearch));
        }

        [Fact]
        public void Entries_WithFunctionKeys_StoreCanonicalUppercaseTokens()
        {
            var functionKeys = KeyRegistry.Entries.Where(entry => entry.Table == KeyTable.FunctionKeys).ToList();

            Assert.Equal(24, functionKeys.Count);
            Assert.All(functionKeys, entry => Assert.Matches("^F([1-9]|1[0-9]|2[0-4])$", entry.Gen1Token));
            Assert.Equal("F1", functionKeys[0].LegacyToken);
            Assert.Equal("F24", functionKeys[^1].LegacyToken);
        }

        [Theory]
        [InlineData(11174, "pro0", "Profile 0")]
        [InlineData(11183, "pro9", "Profile 9")]
        [InlineData(10071, "hk0", " ")]
        [InlineData(10024, "hk1", " ")]
        [InlineData(10031, "hk8", " ")]
        [InlineData(10032, "hk9", "Fn\nToggle")]
        [InlineData(10033, "hk10", "PC\nMenu")]
        public void FindByCode_WithProfileOrHotkeyCode_ReturnsSpecTokenAndDisplay(
            int code,
            string expectedToken,
            string expectedDisplay)
        {
            var entry = KeyRegistry.FindByCode(code);

            Assert.NotNull(entry);
            Assert.Equal(KeyTable.ProfilesAndHotkeys, entry.Table);
            Assert.Equal(expectedToken, entry.Gen1Token);
            Assert.Equal(expectedDisplay, entry.DisplayText);
        }

        [Fact]
        public void PedalPositionTokens_WithSpecOrder_ContainsSevenPlainTokens()
        {
            var expected = new[] { "lpedal", "mpedal", "rpedal", "jack1", "jack2", "jack3", "jack4" };

            Assert.Equal(expected, KeyRegistry.PedalPositionTokens);
        }

        [Fact]
        public void FindByToken_WithPedalPositionToken_ReturnsNull()
        {
            Assert.All(KeyRegistry.PedalPositionTokens, token => Assert.Null(KeyRegistry.FindByToken(token)));
        }

        [Theory]
        [InlineData(10013, "Pro-\ngram")]
        [InlineData(10014, "Key-\npad")]
        public void FindByCode_WithNotWritableEntry_HasEmptyTokensInEveryDialect(int code, string expectedDisplay)
        {
            var entry = KeyRegistry.FindByCode(code);

            Assert.NotNull(entry);
            Assert.True(entry.Flags.HasFlag(KeyDefinitionFlags.NotWritable));
            Assert.Equal(expectedDisplay, entry.DisplayText);
            Assert.Equal("", entry.GetToken(TokenDialect.Legacy));
            Assert.Equal("", entry.GetToken(TokenDialect.Gen1));
            Assert.Equal("", entry.GetToken(TokenDialect.Gen2));
        }

        [Fact]
        public void FindByCode_WithDigitFive_CarriesPerDialectDisplayAndShiftedValue()
        {
            var entry = KeyRegistry.FindByCode(0x35);

            Assert.NotNull(entry);
            Assert.Equal("%", entry.ShiftedValue);
            Assert.Equal("%\n5", entry.GetDisplayText(TokenDialect.Legacy));
            Assert.Equal("5 %", entry.GetDisplayText(TokenDialect.Gen1));
            Assert.True(entry.Flags.HasFlag(KeyDefinitionFlags.ConvertToUnicode));
            Assert.True(entry.Flags.HasFlag(KeyDefinitionFlags.ShowShiftedValue));
        }

        [Fact]
        public void FindByCode_WithReturnKey_CarriesMacDisplayOverride()
        {
            var entry = KeyRegistry.FindByCode(0x0D);

            Assert.NotNull(entry);
            Assert.Equal("Enter", entry.DisplayText);
            Assert.Equal("Return", entry.MacDisplayText);
        }

        [Fact]
        public void FindByCode_WithLedKey_CarriesGlyphAndCanonicalUppercaseToken()
        {
            var entry = KeyRegistry.FindByCode(10022);

            Assert.NotNull(entry);
            Assert.Equal("LED", entry.LegacyToken);
            Assert.Equal("LED", entry.Gen1Token);
            Assert.Equal("ledt", entry.Gen2Token);
            Assert.Equal("☀", entry.GlyphText);
            Assert.Equal("LED", entry.DisplayText);
        }

        [Fact]
        public void FindByCode_WithInternationalKey_UsesPerDialectTokensFromSpec()
        {
            var entry = KeyRegistry.FindByCode(0xE2);

            Assert.NotNull(entry);
            Assert.Equal("intl-\\", entry.LegacyToken);
            Assert.Equal("intl\\", entry.Gen1Token);
            Assert.Equal("int#", entry.Gen2Token);
            Assert.Equal("intl-\\", entry.ShiftedValue);
        }
    }
}
