using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// The seven inputs and the configuration container (specs/12-savant-elite.md §1 the input
    /// list, §4.6 the fixed save order, §5 the row captions). The tokens and the order are pinned
    /// against <see cref="KeyRegistry.PedalPositionTokens"/> (05 §3.14) so they can never drift
    /// into a second hard-coded list.
    /// </summary>
    public class PedalInputsTests
    {
        [Fact]
        public void All_FollowsTheRegistryPedalPositionTokenOrder()
        {
            Assert.Equal(
                KeyRegistry.PedalPositionTokens,
                PedalInputs.All.Select(PedalInputs.GetToken));
        }

        [Fact]
        public void All_HasOneEntryPerRegisteredPedalPositionToken()
        {
            Assert.Equal(KeyRegistry.PedalPositionTokens.Count, PedalInputs.All.Count);
            Assert.Equal(PedalInputs.Count, PedalInputs.All.Count);
        }

        [Theory]
        [InlineData(PedalInputId.LeftPedal, "Left Pedal")]
        [InlineData(PedalInputId.MiddlePedal, "Middle Pedal")]
        [InlineData(PedalInputId.RightPedal, "Right Pedal")]
        [InlineData(PedalInputId.Jack1, "Jack 1")]
        [InlineData(PedalInputId.Jack4, "Jack 4")]
        public void GetDisplayName_ForEachInput_ReturnsTheRowCaption(PedalInputId input, string expected)
        {
            Assert.Equal(expected, PedalInputs.GetDisplayName(input));
        }

        [Theory]
        [InlineData("lpedal", PedalInputId.LeftPedal)]
        [InlineData("JACK3", PedalInputId.Jack3)]
        public void TryParseToken_WithAKnownToken_ResolvesCaseInsensitively(string token, PedalInputId expected)
        {
            Assert.True(PedalInputs.TryParseToken(token, out var input));
            Assert.Equal(expected, input);
        }

        [Theory]
        [InlineData("jack9")]
        [InlineData("")]
        [InlineData(null)]
        public void TryParseToken_WithAnUnknownToken_ReturnsFalseAndNone(string? token)
        {
            Assert.False(PedalInputs.TryParseToken(token, out var input));
            Assert.Equal(PedalInputId.None, input);
        }

        [Fact]
        public void GetToken_ForNone_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PedalInputs.GetToken(PedalInputId.None));
        }

        [Fact]
        public void Configuration_StartsWithSevenUnassignedInputsInFileOrder()
        {
            var configuration = new PedalConfiguration();

            Assert.Equal(PedalInputs.All, configuration.Inputs.Select(input => input.Id));
            Assert.All(configuration.Inputs, input => Assert.Equal(PedalInputMode.None, input.Mode));
            Assert.True(configuration.IsEmpty);
        }

        [Fact]
        public void Configuration_IndexerAndGet_ReturnTheSameInput()
        {
            var configuration = new PedalConfiguration();

            Assert.Same(configuration.Get(PedalInputId.Jack2), configuration[PedalInputId.Jack2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => configuration[PedalInputId.None]);
        }

        [Fact]
        public void SetMacro_ThenSetSingleAction_KeepsTheModeAndPayloadConsistent()
        {
            var input = new PedalInput(PedalInputId.Jack1);
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(PedalFixtures.Key("a")));
            input.SetMacro(macro);

            Assert.Equal(PedalInputMode.Macro, input.Mode);
            Assert.True(input.IsAssigned);

            input.SetSingleAction(PedalFixtures.Key("b"));

            Assert.Equal(PedalInputMode.Single, input.Mode);
            Assert.Null(input.Macro);

            input.SetSingleAction(null);

            Assert.Equal(PedalInputMode.None, input.Mode);
            Assert.False(input.IsAssigned);
        }

        [Fact]
        public void Clone_CopiesTheMacroDeeply()
        {
            var configuration = new PedalConfiguration();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(PedalFixtures.Key("a")));
            configuration[PedalInputId.Jack1].SetMacro(macro);

            var clone = configuration.Clone();

            macro.AddKeystroke(new Keystroke(PedalFixtures.Key("b")));

            Assert.Single(clone[PedalInputId.Jack1].Macro!.Keystrokes);
            Assert.Equal(2, configuration[PedalInputId.Jack1].Macro!.Keystrokes.Count);
        }
    }
}
