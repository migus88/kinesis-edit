using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// One key position per specs/05-key-model.md §1.3: remap rules (§1.5, 04 §2.1), the
    /// trigger-identity and modified-key exceptions for the Fn1 shift and keypad toggle keys
    /// (§5.3), tap-and-hold (§5.6), multi-modifiers (§5.7, 11 §11.2), the five macro slots
    /// (06 §1, §2.2), reset, and copying between positions — including the split between the
    /// editor paths, which refuse what the position cannot hold, and the tolerant load paths,
    /// which store it for <c>Validate()</c> to report.
    /// </summary>
    public class KeyboardKeyTests
    {
        [Fact]
        public void Remap_ToADifferentKey_MarksThePositionModified()
        {
            var key = ModelTokens.CreateKey("caps");

            Assert.True(key.Remap(ModelTokens.Key("lwin")));
            Assert.True(key.IsModified);
            Assert.Equal(ModelTokens.Key("lwin"), key.ModifiedKey);
            Assert.Equal(ModelTokens.Key("lwin"), key.ModifiedOrOriginalKey);
        }

        [Fact]
        public void Remap_ToTheOriginalKey_ClearsTheRemap()
        {
            var key = ModelTokens.CreateKey("caps");

            key.Remap(ModelTokens.Key("lwin"));

            Assert.True(key.Remap(ModelTokens.Key("caps")));
            Assert.False(key.IsModified);
            Assert.Null(key.ModifiedKey);
            Assert.Equal(ModelTokens.Key("caps"), key.ModifiedOrOriginalKey);
        }

        [Fact]
        public void Remap_OnALockedPosition_IsRefused()
        {
            var key = ModelTokens.CreateKey("ss", canEdit: false, canAssignMacro: false);

            Assert.False(key.Remap(ModelTokens.Key("a")));
            Assert.False(key.IsModified);
            Assert.Null(key.ModifiedKey);
        }

        [Fact]
        public void Remap_OnALockedPositionToItsOwnOriginalKey_IsRefusedToo()
        {
            var key = ModelTokens.CreateKey("ss", canEdit: false, canAssignMacro: false);

            // A position that accepts nothing must answer the same way whatever it is offered
            // (05 §5.3) — not "true" for the one assignment that happens to be a no-op.
            Assert.False(key.Remap(key.OriginalKey));
            Assert.False(key.IsModified);
        }

        [Fact]
        public void ApplyRemap_OnALockedPosition_StoresItSoTheValidatorCanReportIt()
        {
            var key = ModelTokens.CreateKey("ss", canEdit: false, canAssignMacro: false);

            key.ApplyRemap(ModelTokens.Key("a"));

            Assert.True(key.IsModified);
            Assert.Equal(ModelTokens.Key("a"), key.ModifiedKey);
        }

        [Fact]
        public void ApplyTapAndHold_OnALockedPosition_StoresItSoTheValidatorCanReportIt()
        {
            var key = ModelTokens.CreateKey("ss", canEdit: false, canAssignMacro: false);

            key.ApplyTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), 250);

            Assert.True(key.IsTapAndHold);
            Assert.Equal(250, key.TimingDelay);
        }

        [Fact]
        public void TriggerKey_ForAnOrdinaryPosition_IsThePositionKey()
        {
            var key = ModelTokens.CreateKey("kp7", TokenDialect.Gen2, positionToken: "u");

            Assert.Equal(ModelTokens.Key("u", TokenDialect.Gen2), key.TriggerKey);
            Assert.Equal(ModelTokens.Key("kp7", TokenDialect.Gen2), key.OriginalKey);
        }

        [Fact]
        public void TriggerKey_WithoutAPositionToken_FallsBackToTheOriginalKey()
        {
            var key = ModelTokens.CreateKey("a");

            Assert.Equal(key.OriginalKey, key.PositionKey);
            Assert.Equal(key.OriginalKey, key.TriggerKey);
        }

        [Theory]
        [InlineData("fn1s", "lfn", KeyboardKey.Fn1ShiftKeyCode)]
        [InlineData("keyt", "kp", KeyboardKey.KeypadToggleKeyCode)]
        public void TriggerKey_ForTheLayerSwitchKeys_IsTheOriginalKey(
            string defaultToken,
            string positionToken,
            int expectedCode)
        {
            var key = ModelTokens.CreateKey(defaultToken, TokenDialect.Gen2, positionToken);

            Assert.Equal(expectedCode, key.TriggerKey.Code);
            Assert.NotEqual(key.PositionKey.Code, key.TriggerKey.Code);
        }

        [Theory]
        [InlineData("fn1s", "lfn")]
        [InlineData("keyt", "kp")]
        public void ModifiedOrOriginalKey_ForAModifiedLayerSwitchKey_StaysTheOriginalKey(
            string defaultToken,
            string positionToken)
        {
            var key = ModelTokens.CreateKey(defaultToken, TokenDialect.Gen2, positionToken);

            // 05 §5.3: these two keys "always act/report as their original layer-switch action even
            // when 'modified'" — so the remap *is* stored (04 §4.3 writes the line and 04 §4.2 reads
            // it back into the same state); only the derived action ignores it.
            Assert.True(key.Remap(ModelTokens.Key("a", TokenDialect.Gen2)));
            Assert.True(key.IsModified);
            Assert.Equal(ModelTokens.Key("a", TokenDialect.Gen2), key.ModifiedKey);
            Assert.Equal(key.OriginalKey, key.ModifiedOrOriginalKey);
            Assert.Equal(key.OriginalKey, key.TriggerKey);
        }

        [Fact]
        public void SetTapAndHold_WithADeviceCapability_TakesItsDefaultDelay()
        {
            var key = ModelTokens.CreateKey("caps");
            var capability = DeviceCatalog.GetById(DeviceId.Advantage2).TapAndHold;

            Assert.True(key.SetTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), capability));
            Assert.True(key.IsTapAndHold);
            Assert.Equal(250, key.TimingDelay);
            Assert.Equal(ModelTokens.Key("a"), key.TapAction);
            Assert.Equal(ModelTokens.Key("lctrl"), key.HoldAction);
        }

        [Fact]
        public void SetTapAndHold_OnTheAdvantage360_TakesItsShorterDefaultDelay()
        {
            var key = ModelTokens.CreateKey("caps", TokenDialect.Gen2);
            var capability = DeviceCatalog.GetById(DeviceId.Advantage360).TapAndHold;

            key.SetTapAndHold(ModelTokens.Key("a", TokenDialect.Gen2), ModelTokens.Key("lctr", TokenDialect.Gen2), capability);

            Assert.Equal(150, key.TimingDelay);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(250)]
        [InlineData(999)]
        public void SetTapAndHold_WithADelayInsideTheBounds_IsWithinTheDeviceRange(int delay)
        {
            var key = ModelTokens.CreateKey("caps");
            var range = DeviceCatalog.GetById(DeviceId.Advantage2).TapAndHold.DelayMilliseconds;

            key.SetTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), delay);

            Assert.Equal(delay, key.TimingDelay);
            Assert.True(range!.Contains(key.TimingDelay));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void SetTapAndHold_WithADelayOutsideTheBounds_StoresItAnyway(int delay)
        {
            var key = ModelTokens.CreateKey("caps");
            var range = DeviceCatalog.GetById(DeviceId.Advantage2).TapAndHold.DelayMilliseconds;

            Assert.True(key.SetTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), delay));
            Assert.Equal(delay, key.TimingDelay);
            Assert.False(range!.Contains(key.TimingDelay));
        }

        [Fact]
        public void SetTapAndHold_OnALockedPosition_IsRefused()
        {
            var key = ModelTokens.CreateKey("ss", canEdit: false, canAssignMacro: false);

            Assert.False(key.SetTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), 250));
            Assert.False(key.IsTapAndHold);
        }

        [Fact]
        public void Remap_AfterATapAndHoldAssignment_ClearsIt()
        {
            var key = Advantage360Key("caps");

            key.SetTapAndHold(ModelTokens.Key("a", TokenDialect.Gen2), ModelTokens.Key("lctr", TokenDialect.Gen2), 250);

            // 11 §11.1: "a plain remap of the key likewise clears its tap-and-hold configuration."
            // Without this, 04 §4.3's precedence writes the tap-and-hold line and drops the remap.
            Assert.True(key.Remap(ModelTokens.Key("z", TokenDialect.Gen2)));
            Assert.False(key.IsTapAndHold);
            Assert.Null(key.TapAction);
            Assert.Null(key.HoldAction);
            Assert.Equal(0, key.TimingDelay);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void Remap_AfterAMultiModifierAssignment_ClearsIt()
        {
            var key = Advantage360Key("caps");

            key.TrySetMultiModifiers("caws");

            // 04 §2.3 makes a multi-modifier "a remap whose output is a combination of modifiers",
            // so a plain remap replaces it — one [pos]>[value] line, one rule.
            Assert.True(key.Remap(ModelTokens.Key("z", TokenDialect.Gen2)));
            Assert.Null(key.MultiModifiers);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void SetTapAndHold_AfterARemapAndAMultiModifier_ClearsThem()
        {
            var key = Advantage360Key("caps");

            key.TrySetMultiModifiers("caws");
            key.Remap(ModelTokens.Key("z", TokenDialect.Gen2));

            Assert.True(key.SetTapAndHold(
                ModelTokens.Key("a", TokenDialect.Gen2),
                ModelTokens.Key("lctr", TokenDialect.Gen2),
                250));
            Assert.False(key.IsModified);
            Assert.Null(key.ModifiedKey);
            Assert.Null(key.MultiModifiers);
            Assert.True(key.IsTapAndHold);
        }

        [Fact]
        public void TrySetMultiModifiers_AfterARemapAndATapAndHold_ClearsThem()
        {
            var key = Advantage360Key("caps");

            key.Remap(ModelTokens.Key("z", TokenDialect.Gen2));
            key.SetTapAndHold(ModelTokens.Key("a", TokenDialect.Gen2), ModelTokens.Key("lctr", TokenDialect.Gen2), 250);

            Assert.True(key.TrySetMultiModifiers("caws"));
            Assert.False(key.IsModified);
            Assert.False(key.IsTapAndHold);
            Assert.Equal("caws", key.MultiModifiers);
        }

        [Fact]
        public void TrySetMultiModifiers_ForARefusedCode_LeavesTheOtherRulesAlone()
        {
            var key = Advantage360Key("caps");

            key.Remap(ModelTokens.Key("z", TokenDialect.Gen2));

            Assert.False(key.TrySetMultiModifiers("xxxx"));
            Assert.True(key.IsModified);
        }

        [Fact]
        public void ApplyRemap_AfterATapAndHoldAssignment_LeavesItForTheValidatorToReport()
        {
            var key = Advantage360Key("caps");

            key.ApplyTapAndHold(ModelTokens.Key("a", TokenDialect.Gen2), ModelTokens.Key("lctr", TokenDialect.Gen2), 250);
            key.ApplyRemap(ModelTokens.Key("z", TokenDialect.Gen2));

            // The tolerant path never drops a line a file carried; Validate() reports the conflict.
            Assert.True(key.IsTapAndHold);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void ClearTapAndHold_AfterAnAssignment_RemovesEverything()
        {
            var key = ModelTokens.CreateKey("caps");

            key.SetTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), 250);
            key.ClearTapAndHold();

            Assert.False(key.IsTapAndHold);
            Assert.Null(key.TapAction);
            Assert.Null(key.HoldAction);
            Assert.Equal(0, key.TimingDelay);
        }

        [Theory]
        [InlineData("caws")]
        [InlineData("cawx")]
        [InlineData("cxws")]
        [InlineData("caxs")]
        [InlineData("xaws")]
        [InlineData("caxx")]
        [InlineData("cxwx")]
        [InlineData("cxxs")]
        [InlineData("xawx")]
        [InlineData("xaxs")]
        [InlineData("xxws")]
        public void TrySetMultiModifiers_ForEachAcceptedCode_StoresIt(string code)
        {
            var key = Advantage360Key("caps");

            Assert.True(key.TrySetMultiModifiers(code));
            Assert.Equal(code, key.MultiModifiers);
            Assert.True(key.HasMultiModifiers);
        }

        [Theory]
        [InlineData("CAWS", "caws")]
        [InlineData("CxWs", "cxws")]
        public void TrySetMultiModifiers_IsCaseInsensitive_AndStoresTheCanonicalCasing(string code, string expected)
        {
            var key = Advantage360Key("caps");

            Assert.True(key.TrySetMultiModifiers(code));
            Assert.Equal(expected, key.MultiModifiers);
        }

        [Fact]
        public void TrySetMultiModifiers_OnADeviceWithoutTheFeature_IsRefused()
        {
            // 11 §11.2: the Multimodifiers dialog is "Advantage360 only" and the code is serialized
            // in the "Adv360 format only"; 05 §1.3 and §5.7 scope the field the same way.
            var key = ModelTokens.CreateKey("caps");

            Assert.False(key.SupportsMultiModifiers);
            Assert.False(key.TrySetMultiModifiers("caws"));
            Assert.Null(key.MultiModifiers);
        }

        [Fact]
        public void ApplyMultiModifiers_OnADeviceWithoutTheFeature_StoresItSoTheValidatorCanReportIt()
        {
            var key = ModelTokens.CreateKey("caps");

            Assert.True(key.ApplyMultiModifiers("caws"));
            Assert.Equal("caws", key.MultiModifiers);
        }

        [Fact]
        public void ApplyMultiModifiers_ForAnUnacceptedCode_IsStillRefused()
        {
            var key = ModelTokens.CreateKey("caps");

            Assert.False(key.ApplyMultiModifiers("xxxx"));
            Assert.Null(key.MultiModifiers);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("caw")]
        [InlineData("cawsx")]
        [InlineData("xxxs")]
        [InlineData("cxxx")]
        [InlineData("xxxx")]
        [InlineData("wacs")]
        [InlineData("lshft")]
        public void TrySetMultiModifiers_ForAnUnacceptedCode_IsRefused(string? code)
        {
            var key = Advantage360Key("caps");

            Assert.False(key.TrySetMultiModifiers(code));
            Assert.Null(key.MultiModifiers);
        }

        [Fact]
        public void TrySetMultiModifiers_OnALockedPosition_IsRefused()
        {
            var key = ModelTokens.CreateKey(
                "ss",
                TokenDialect.Gen2,
                canEdit: false,
                canAssignMacro: false,
                supportsMultiModifiers: true);

            Assert.False(key.TrySetMultiModifiers("caws"));
            Assert.Null(key.MultiModifiers);
        }

        [Fact]
        public void ActiveMacroIndex_OnANewKey_DefaultsToTheFirstSlot()
        {
            var key = ModelTokens.CreateKey("a");

            Assert.Equal(1, key.ActiveMacroIndex);
            Assert.Null(key.ActiveMacro);
            Assert.False(key.IsMacro);
        }

        [Fact]
        public void SetMacro_ForEachSlot_StampsTheMacroIndexAndFillsTheSlot()
        {
            var key = ModelTokens.CreateKey("a");

            for (var slot = 1; slot <= KeyboardKey.MacroSlotCount; slot++)
            {
                key.SetMacro(slot, new Macro());
            }

            Assert.Equal(KeyboardKey.MacroSlotCount, key.MacroCount);
            Assert.True(key.IsMacro);
            Assert.Same(key.Macro1, key.ActiveMacro);

            for (var slot = 1; slot <= KeyboardKey.MacroSlotCount; slot++)
            {
                Assert.Equal(slot, key.GetMacro(slot)!.MacroIndex);
            }
        }

        [Fact]
        public void SetMacro_OnARestrictedPosition_IsStillStoredForTolerantLoading()
        {
            var key = ModelTokens.CreateKey("lshft", canAssignMacro: false);

            key.SetMacro(1, new Macro());

            Assert.True(key.IsMacro);
        }

        [Fact]
        public void AssignMacro_WithTheFirstSlotTaken_FillsTheNextEmptyOne()
        {
            var key = ModelTokens.CreateKey("a");

            key.SetMacro(1, new Macro());

            Assert.Equal(2, key.AssignMacro(new Macro()));
            Assert.Equal(2, key.MacroCount);
        }

        [Fact]
        public void AssignMacro_OnARestrictedPosition_ReturnsNoSlot()
        {
            var key = ModelTokens.CreateKey("lshft", canAssignMacro: false);

            Assert.Equal(0, key.AssignMacro(new Macro()));
            Assert.False(key.IsMacro);
        }

        [Fact]
        public void AssignMacro_WhenEverySlotIsFull_ReturnsNoSlot()
        {
            var key = ModelTokens.CreateKey("a");

            for (var slot = 1; slot <= KeyboardKey.MacroSlotCount; slot++)
            {
                key.SetMacro(slot, new Macro());
            }

            Assert.Equal(0, key.AssignMacro(new Macro()));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        public void GetMacro_ForASlotOutsideTheModel_Throws(int slot)
        {
            var key = ModelTokens.CreateKey("a");

            Assert.Throws<ArgumentOutOfRangeException>(() => key.GetMacro(slot));
        }

        [Fact]
        public void AssignMacro_OnADeviceWithoutPerKeySlots_ReturnsNoSlot()
        {
            var key = ModelTokens.CreateKey("a", TokenDialect.Gen2, usesMacroSlots: false);
            var macro = new Macro();

            // 06 §1: the Advantage360 keeps every macro in the layout's flat list, where
            // Macro.MacroIndex is 0 — stamping a slot number on it would corrupt that.
            Assert.Equal(0, key.AssignMacro(macro));
            Assert.False(key.IsMacro);
            Assert.Equal(0, macro.MacroIndex);
        }

        [Fact]
        public void SetMacro_OnADeviceWithoutPerKeySlots_IsStillStoredForTolerantLoading()
        {
            var key = ModelTokens.CreateKey("a", TokenDialect.Gen2, usesMacroSlots: false);

            key.SetMacro(1, new Macro());

            Assert.True(key.IsMacro);
        }

        [Fact]
        public void Macros_WhenACallerCastsItBackToAWritableList_RefusesTheWrite()
        {
            var key = ModelTokens.CreateKey("a");

            // The slot array must not escape: writing through it would skip SetMacro's slot
            // stamping and leave Macro.MacroIndex naming a slot the key does not hold.
            Assert.Throws<NotSupportedException>(() => ((IList<Macro?>)key.Macros)[0] = new Macro());
        }

        [Fact]
        public void Reset_AfterEveryKindOfEdit_ClearsThemButKeepsTheColour()
        {
            var key = CreateFullyEditedKey();

            key.KeyColor = new KeyColor(255, 128, 0);
            key.Reset();

            Assert.False(key.IsModified);
            Assert.Null(key.ModifiedKey);
            Assert.False(key.IsTapAndHold);
            Assert.Null(key.MultiModifiers);
            Assert.False(key.IsMacro);
            Assert.Equal(1, key.ActiveMacroIndex);
            Assert.Equal(new KeyColor(255, 128, 0), key.KeyColor);
        }

        [Fact]
        public void CopyFrom_WithKeyDataScope_CopiesTheRemapAndLeavesMacrosAlone()
        {
            var source = CreateFullyEditedKey();
            var target = Advantage360Key("b", index: 1);

            target.CopyFrom(source, KeyCopyScopes.KeyData);

            Assert.True(target.IsModified);
            Assert.Equal(source.ModifiedKey, target.ModifiedKey);
            Assert.True(target.IsTapAndHold);
            Assert.Equal(source.TimingDelay, target.TimingDelay);
            Assert.Equal(source.MultiModifiers, target.MultiModifiers);
            Assert.False(target.IsMacro);
        }

        [Fact]
        public void CopyFrom_WithMacrosScope_DeepCopiesTheSlotsAndLeavesKeyDataAlone()
        {
            var source = CreateFullyEditedKey();
            var target = Advantage360Key("b", index: 1);

            target.CopyFrom(source, KeyCopyScopes.Macros);

            Assert.False(target.IsModified);
            Assert.False(target.IsTapAndHold);
            Assert.Null(target.MultiModifiers);
            Assert.Equal(source.MacroCount, target.MacroCount);
            Assert.NotSame(source.Macro1, target.Macro1);
            Assert.True(target.Macro1!.IsEquivalentTo(source.Macro1));
        }

        [Fact]
        public void CopyFrom_WithAllScope_CopiesKeyDataAndMacros()
        {
            var source = CreateFullyEditedKey();
            var target = Advantage360Key("b", index: 1);

            target.CopyFrom(source, KeyCopyScopes.All);

            Assert.True(target.IsModified);
            Assert.True(target.IsTapAndHold);
            Assert.Equal(source.MultiModifiers, target.MultiModifiers);
            Assert.Equal(source.MacroCount, target.MacroCount);
            Assert.Equal(source.ActiveMacroIndex, target.ActiveMacroIndex);
        }

        [Fact]
        public void CopyFrom_WithNoneScope_CopiesNothing()
        {
            var source = CreateFullyEditedKey();
            var target = Advantage360Key("b", index: 1);

            target.CopyFrom(source, KeyCopyScopes.None);

            Assert.False(target.IsModified);
            Assert.False(target.IsTapAndHold);
            Assert.Null(target.MultiModifiers);
            Assert.False(target.IsMacro);
        }

        [Fact]
        public void CopyFrom_WithAllScope_NeverCopiesGeometryFacts()
        {
            var source = CreateFullyEditedKey();
            var target = Advantage360Key("b", index: 1);

            target.CopyFrom(source, KeyCopyScopes.All);

            Assert.Equal(1, target.Index);
            Assert.Equal(ModelTokens.Key("b", TokenDialect.Gen2), target.OriginalKey);
        }

        [Fact]
        public void CopyFrom_OntoALockedPosition_CopiesNoKeyData()
        {
            var source = CreateFullyEditedKey();
            var target = ModelTokens.CreateKey(
                "ss",
                TokenDialect.Gen2,
                index: 1,
                canEdit: false,
                canAssignMacro: false,
                supportsMultiModifiers: true);

            target.CopyFrom(source, KeyCopyScopes.All);

            // 05 §5.3: a locked position never produces a layout line, so copying is a user action
            // it must refuse just like Remap/SetTapAndHold/TrySetMultiModifiers do.
            Assert.False(target.IsModified);
            Assert.Null(target.ModifiedKey);
            Assert.False(target.IsTapAndHold);
            Assert.Null(target.MultiModifiers);
        }

        [Fact]
        public void CopyFrom_OntoAPositionThatRejectsMacros_StillCopiesThem()
        {
            var source = CreateFullyEditedKey();
            var target = ModelTokens.CreateKey("lshf", TokenDialect.Gen2, index: 1, canAssignMacro: false);

            target.CopyFrom(source, KeyCopyScopes.Macros);

            // Unlike the key-data half this stays unchecked on purpose: the state is already
            // visible, because Validate() reports MacroOnRestrictedKey for it.
            Assert.True(target.IsMacro);
        }

        [Fact]
        public void CopyFrom_OntoADeviceWithoutMultiModifiers_ClearsTheTargetCode()
        {
            var source = CreateFullyEditedKey();
            var target = ModelTokens.CreateKey("b", TokenDialect.Gen2, index: 1);

            target.ApplyMultiModifiers("cxxs");
            target.CopyFrom(source, KeyCopyScopes.All);

            // A key-data copy replaces the target's key data outright. Keeping a stale code the
            // device cannot hold would leave the target with two single-key rules — the one thing
            // no editor path may produce — and the code was already reported before the copy.
            Assert.True(target.IsModified);
            Assert.Null(target.MultiModifiers);
        }

        [Fact]
        public void CopyFrom_WithKeyDataScope_NeverLeavesMoreRulesThanTheSourceHad()
        {
            var source = ModelTokens.CreateKey("a", TokenDialect.Gen2);
            var target = ModelTokens.CreateKey("b", TokenDialect.Gen2, index: 1);

            source.Remap(ModelTokens.Key("z", TokenDialect.Gen2));
            target.ApplyMultiModifiers("caws");
            target.CopyFrom(source, KeyCopyScopes.KeyData);

            Assert.True(target.IsModified);
            Assert.False(target.IsTapAndHold);
            Assert.Null(target.MultiModifiers);
        }

        private static KeyboardKey Advantage360Key(string defaultToken, int index = 0)
        {
            return ModelTokens.CreateKey(
                defaultToken,
                TokenDialect.Gen2,
                index: index,
                supportsMultiModifiers: true);
        }

        /// <summary>
        /// A key holding every kind of state at once. The editor paths cannot produce this — the
        /// three single-key rules of 04 §2 share one slot — so it is built through the tolerant load
        /// paths, which is exactly the shape a hand-edited field file can carry.
        /// </summary>
        private static KeyboardKey CreateFullyEditedKey()
        {
            var key = Advantage360Key("caps");
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));

            key.ApplyRemap(ModelTokens.Key("lwin", TokenDialect.Gen2));
            key.ApplyTapAndHold(ModelTokens.Key("a", TokenDialect.Gen2), ModelTokens.Key("lctr", TokenDialect.Gen2), 300);
            key.ApplyMultiModifiers("caws");
            key.SetMacro(1, macro);
            key.ActiveMacroIndex = 1;

            return key;
        }
    }
}
