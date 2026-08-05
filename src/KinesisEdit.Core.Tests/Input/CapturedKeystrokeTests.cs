using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Input
{
    /// <summary>
    /// Pins the modifier rules of specs/05-key-model.md §5.1 that the record enforces itself:
    /// the active-modifier set is deduplicated by key code, a key that is itself a modifier
    /// never carries a modifier string, and the stored list is a defensive copy.
    /// </summary>
    public class CapturedKeystrokeTests
    {
        [Fact]
        public void HeldModifiers_WithRepeatedKeyCodes_AreDeduplicatedByCode()
        {
            var leftShift = KeyRegistry.FindByCode(160)!;
            var leftControl = KeyRegistry.FindByCode(162)!;

            var keystroke = new CapturedKeystroke
            {
                Key = KeyRegistry.FindByCode(65)!,
                PhysicalKey = PhysicalKeyCode.A,
                HeldModifiers = new[] { leftShift, leftControl, leftShift, leftControl }
            };

            Assert.Equal(new[] { 160, 162 }, keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void HeldModifiers_WithDistinctKeyCodes_KeepThePressOrder()
        {
            var keystroke = new CapturedKeystroke
            {
                Key = KeyRegistry.FindByCode(65)!,
                PhysicalKey = PhysicalKeyCode.A,
                HeldModifiers = new[]
                {
                    KeyRegistry.FindByCode(165)!,
                    KeyRegistry.FindByCode(160)!,
                    KeyRegistry.FindByCode(91)!
                }
            };

            Assert.Equal(new[] { 165, 160, 91 }, keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void HeldModifiers_WhenUnset_AreEmpty()
        {
            var keystroke = new CapturedKeystroke
            {
                Key = KeyRegistry.FindByCode(65)!,
                PhysicalKey = PhysicalKeyCode.A
            };

            Assert.Empty(keystroke.HeldModifiers);
        }

        [Theory]
        [InlineData(160, PhysicalKeyCode.ShiftLeft)]
        [InlineData(161, PhysicalKeyCode.ShiftRight)]
        [InlineData(162, PhysicalKeyCode.ControlLeft)]
        [InlineData(163, PhysicalKeyCode.ControlRight)]
        [InlineData(164, PhysicalKeyCode.AltLeft)]
        [InlineData(165, PhysicalKeyCode.AltRight)]
        [InlineData(91, PhysicalKeyCode.MetaLeft)]
        [InlineData(92, PhysicalKeyCode.MetaRight)]
        public void HeldModifiers_WhenTheKeyIsItselfAModifier_AreForcedEmpty(int keyCode, PhysicalKeyCode physicalKey)
        {
            var keystroke = new CapturedKeystroke
            {
                Key = KeyRegistry.FindByCode(keyCode)!,
                PhysicalKey = physicalKey,
                HeldModifiers = new[] { KeyRegistry.FindByCode(162)! }
            };

            Assert.Empty(keystroke.HeldModifiers);
        }

        [Fact]
        public void HeldModifiers_WhenTheKeyIsAGenericModifier_AreForcedEmpty()
        {
            var keystroke = new CapturedKeystroke
            {
                Key = KeyRegistry.FindByCode(16)!,
                PhysicalKey = PhysicalKeyCode.ShiftLeft,
                HeldModifiers = new[] { KeyRegistry.FindByCode(162)! }
            };

            Assert.Equal(16, keystroke.Key.Code);
            Assert.Empty(keystroke.HeldModifiers);
        }

        [Fact]
        public void HeldModifiers_AfterTheSourceListChanges_AreUnaffected()
        {
            var source = new List<KeyDefinition> { KeyRegistry.FindByCode(160)! };

            var keystroke = new CapturedKeystroke
            {
                Key = KeyRegistry.FindByCode(65)!,
                PhysicalKey = PhysicalKeyCode.A,
                HeldModifiers = source
            };

            source.Add(KeyRegistry.FindByCode(162)!);
            source.Clear();

            Assert.Equal(new[] { 160 }, keystroke.HeldModifiers.Select(modifier => modifier.Code));
        }

        [Fact]
        public void Key_AndPhysicalKey_AreStoredAsGiven()
        {
            var keystroke = new CapturedKeystroke
            {
                Key = KeyRegistry.FindByCode(10000)!,
                PhysicalKey = PhysicalKeyCode.NumPadEnter
            };

            Assert.Equal(10000, keystroke.Key.Code);
            Assert.Equal(PhysicalKeyCode.NumPadEnter, keystroke.PhysicalKey);
        }
    }
}
