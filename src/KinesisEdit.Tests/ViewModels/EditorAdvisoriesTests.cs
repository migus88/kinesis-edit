using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The advisory set behind the editor's amber strip: what each source produces, where it is
    /// anchored, and what the counts say. Every source already reports without enforcing
    /// (docs/app/keyboard-model.md, invariant 1), and this suite is where "the advisory system adds
    /// nothing that blocks" is pinned — a clean layout has none, and nothing built here is ever
    /// asked whether an edit may happen.
    /// </summary>
    public sealed class EditorAdvisoriesTests
    {
        [Fact]
        public void AFactoryLayout_CarriesNoAdvisories()
        {
            var advisories = EditorAdvisories.Build(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            Assert.Equal(0, advisories.Total);
            Assert.Empty(advisories.All);
            Assert.Equal(string.Empty, advisories.SummaryFor(EditorTab.Keys, 0));
        }

        [Fact]
        public void NoLayoutAtAll_BuildsTheEmptySet()
        {
            Assert.Equal(0, EditorAdvisories.Build(null).Total);
            Assert.Equal(0, EditorAdvisories.Empty.Total);
        }

        [Fact]
        public void AFactoryDuplicate_IsNotReported()
        {
            // The board ships with both Shifts, both Ctrls and a column of hotkeys sharing tokens.
            // Reporting those would flood a stock profile with notes the user did not cause.
            Assert.Empty(EditorAdvisories.Build(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb)).All);
        }

        [Fact]
        public void ATapAndHoldCountPastTheDeviceLimit_IsAnchoredToTheLayoutTab()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var limit = layout.Device.TapAndHold.MaxPerLayout!.Value;

            AssignTapAndHold(layout, limit + 1);

            var advisories = EditorAdvisories.Build(layout);
            var advisory = Assert.Single(advisories.ForTab(EditorTab.Keys));

            Assert.Equal(EditorTab.Keys, advisory.Tab);
            Assert.Equal(StatusSeverity.Warning, advisory.Severity);
            Assert.Equal(AdvisoryText.TapAndHoldCount(limit + 1, limit), advisory.Message);
            Assert.Equal(AdvisoryText.TapAndHoldCountDetail(limit + 1, limit), advisory.Detail);

            // Layout-wide: the count is a property of the profile, not of one layer or one key.
            Assert.Null(advisory.LayerIndex);
            Assert.Null(advisory.KeyIndex);
        }

        [Fact]
        public void ALayoutWideAdvisory_IsShownOnEveryLayer()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var limit = layout.Device.TapAndHold.MaxPerLayout!.Value;

            AssignTapAndHold(layout, limit + 1);

            var advisories = EditorAdvisories.Build(layout);

            Assert.Single(advisories.ForTab(EditorTab.Keys, 0));
            Assert.Single(advisories.ForTab(EditorTab.Keys, 1));
        }

        [Fact]
        public void ADuplicateToken_IsAnchoredToEveryPositionCarryingIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];

            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layer.Keys[0].OriginalKey);

            var finding = Assert.Single(DuplicateKeyScan.Find(layout));
            var advisories = EditorAdvisories.Build(layout);
            var anchored = advisories.ForTab(EditorTab.Keys, layer.Index);

            // One advisory per position of the group, so every cap in it carries the note and the
            // strip's "n keys carry advisory notes" counts what it says it counts.
            Assert.Equal(finding.KeyIndexes.Count, anchored.Count);
            Assert.Equal(
                finding.KeyIndexes.Select(index => (int?)index),
                anchored.Select(advisory => advisory.KeyIndex));
            Assert.Contains(TestLayouts.RgbDigitOneKeyIndex, finding.KeyIndexes);
            Assert.All(anchored, advisory => Assert.Equal(layer.Index, advisory.LayerIndex));
            Assert.All(anchored, advisory => Assert.EndsWith(
                AdvisoryText.DuplicatesAreAllowed,
                advisory.Message,
                StringComparison.Ordinal));

            Assert.True(advisories.HasAdvisoryForKey(layer.Index, TestLayouts.RgbDigitOneKeyIndex));
            Assert.False(advisories.HasAdvisoryForKey(layer.Index, TestLayouts.RgbDigitTwoKeyIndex));
            Assert.Equal(finding.KeyIndexes.Count, advisories.CountForLayer(layer.Index));
            Assert.Equal(0, advisories.CountForLayer(1));
        }

        [Fact]
        public void ADuplicateToken_IsNamedInTheDevicesOwnDialect()
        {
            // DuplicateKeyScan.Find(layer) defaults to TokenDialect.None, which spells the token
            // with whichever dialect the key-table entry carries first — a token the user's file
            // may not contain. Build passes the layout's dialect, so the sentence names what the
            // file names.
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var layer = layout.Layers[0];
            var source = FindTokenedKey(layer, layout.Dialect, skip: 0);
            var target = FindTokenedKey(layer, layout.Dialect, skip: 1);

            target.Remap(source.OriginalKey);

            var advisory = Assert.Single(EditorAdvisories.Build(layout).ForKey(layer.Index, target.Index));

            Assert.Equal(TokenDialect.Gen2, layout.Dialect);
            Assert.Contains(
                "[" + source.OriginalKey.GetToken(TokenDialect.Gen2) + "]",
                advisory.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void AMacroPastItsCharacterBudget_IsAnchoredToItsSlot()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var limit = layout.Device.Macros.MaxCharactersPerMacro!.Value;
            var key = FindMacroKey(layout);

            key.SetMacro(1, CreateMacro(layout, limit + 1));

            var advisories = EditorAdvisories.Build(layout);
            var advisory = Assert.Single(
                advisories.ForTab(EditorTab.Macros),
                candidate => candidate.Message.Contains("characters", StringComparison.Ordinal));

            Assert.Equal(EditorTab.Macros, advisory.Tab);
            Assert.Equal(0, advisory.LayerIndex);
            Assert.Equal(key.Index, advisory.KeyIndex);
            Assert.Equal(1, advisory.MacroIndex);
            Assert.Equal(AdvisoryText.MacroCharacters(limit + 1, limit), advisory.Message);

            Assert.True(advisories.HasAdvisoryForMacro(0, key.Index, 1));
            Assert.False(advisories.HasAdvisoryForMacro(0, key.Index, 2));

            // A flat-list row names no key, and Validate gives no per-macro identity for one.
            Assert.False(advisories.HasAdvisoryForMacro(0, null, 0));
        }

        [Fact]
        public void AProfilePastTheKeystrokeBudget_IsALayoutWideMacroAdvisory()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var budget = layout.Device.Macros.MaxTotalKeystrokes!.Value;
            var perMacro = layout.Device.Macros.MaxCharactersPerMacro!.Value;

            foreach (var key in layout.Layers[0].Keys)
            {
                if (layout.TotalKeystrokes > budget)
                {
                    break;
                }

                if (key.CanAssignMacro && !key.IsMacro)
                {
                    key.SetMacro(1, CreateMacro(layout, perMacro));
                }
            }

            var advisories = EditorAdvisories.Build(layout);
            var advisory = Assert.Single(
                advisories.ForTab(EditorTab.Macros),
                candidate => candidate.Message.Contains("layout keystroke budget", StringComparison.Ordinal));

            Assert.Equal(AdvisoryText.LayoutKeystrokeBudget(layout.TotalKeystrokes, budget), advisory.Message);
            Assert.Null(advisory.KeyIndex);
        }

        [Fact]
        public void AMacroPastTheCoTriggerBudget_IsAnchoredToItsSlot()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var limit = layout.Device.Macros.MaxCoTriggersPerMacro!.Value;
            var key = FindMacroKey(layout);
            var macro = layout.CreateMacro();

            foreach (var token in new[] { "lshft", "rshft", "lctrl", "rctrl", "lalt", "ralt" }.Take(limit + 1))
            {
                macro.AddCoTrigger(TestLayouts.Gen1Key(token));
            }

            key.SetMacro(1, macro);

            var advisory = Assert.Single(
                EditorAdvisories.Build(layout).ForTab(EditorTab.Macros),
                candidate => candidate.Message.Contains("co-trigger", StringComparison.Ordinal));

            Assert.Equal(AdvisoryText.CoTriggers(limit + 1, limit), advisory.Message);
            Assert.Equal(key.Index, advisory.KeyIndex);
            Assert.Equal(1, advisory.MacroIndex);
        }

        [Fact]
        public void AViolationThatIsNotABudget_IsNoAdvisory()
        {
            // A remap on a locked position cannot be written faithfully — that is a failure the
            // save path reports, not something the amber strip should reassure the user about.
            var layout = TestLayouts.CreateLockedKeyLayout();

            // The tolerant load path, which is the only way a locked position ever holds a remap.
            layout.Layers[0].Keys[1].ApplyRemap(TestLayouts.Gen1Key("a"));

            Assert.NotEmpty(layout.Validate());
            Assert.Equal(0, EditorAdvisories.Build(layout).Total);
        }

        [Fact]
        public void CountsPerTabAndPerLayer_AnswerAboutTheirOwnScope()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var limit = layout.Device.Macros.MaxCharactersPerMacro!.Value;

            layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layout.Layers[0].Keys[0].OriginalKey);
            layout.Layers[1].Keys[TestLayouts.RgbDigitTwoKeyIndex].Remap(layout.Layers[1].Keys[0].OriginalKey);
            FindMacroKey(layout).SetMacro(1, CreateMacro(layout, limit + 1));

            var advisories = EditorAdvisories.Build(layout);
            var onLayerZero = advisories.ForTab(EditorTab.Keys, 0).Count;
            var onLayerOne = advisories.ForTab(EditorTab.Keys, 1).Count;

            Assert.True(onLayerZero >= 2, $"Layer 0 reported {onLayerZero} advisories.");
            Assert.True(onLayerOne >= 2, $"Layer 1 reported {onLayerOne} advisories.");

            // No advisory here is layout-wide, so the two layers partition the Layout tab exactly.
            Assert.Equal(onLayerZero + onLayerOne, advisories.CountForTab(EditorTab.Keys));
            Assert.Equal(onLayerZero, advisories.CountForLayer(0));
            Assert.Equal(onLayerOne, advisories.CountForLayer(1));

            Assert.Equal(1, advisories.CountForTab(EditorTab.Macros));
            Assert.Equal(0, advisories.CountForTab(EditorTab.Lighting));
            Assert.Equal(0, advisories.CountForTab(EditorTab.Settings));
            Assert.Equal(advisories.CountForTab(EditorTab.Keys) + 1, advisories.Total);
        }

        [Fact]
        public void TheLayoutTabsSummary_CountsPositionsAndCarriesTheReassurance()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];
            var limit = layout.Device.TapAndHold.MaxPerLayout!.Value;

            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layer.Keys[0].OriginalKey);
            AssignTapAndHold(layout, limit + 1);

            var advisories = EditorAdvisories.Build(layout);
            var summary = advisories.SummaryFor(EditorTab.Keys, 0);

            // Mockup 1e's shape: the position count, then the layout-wide fact, then the
            // reassurance — the layout-wide advisory is reported first, so it supplies the detail.
            Assert.Equal(
                AdvisoryText.LayerSummary(advisories.CountForLayer(0), AdvisoryText.TapAndHoldCountDetail(limit + 1, limit)),
                summary);
            Assert.Contains("keys carry advisory notes on this layer", summary, StringComparison.Ordinal);
            Assert.EndsWith(AdvisoryText.OlderFirmwareReassurance, summary, StringComparison.Ordinal);
        }

        [Fact]
        public void ASingleAdvisory_SpeaksForItselfInTheStrip()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var limit = layout.Device.TapAndHold.MaxPerLayout!.Value;

            AssignTapAndHold(layout, limit + 1);

            var advisories = EditorAdvisories.Build(layout);

            Assert.Equal(advisories.All[0].Message, advisories.SummaryFor(EditorTab.Keys, 0));
        }

        [Fact]
        public void ASectionWithNothingToSay_HasAnEmptySummary()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layout.Layers[0].Keys[0].OriginalKey);

            var advisories = EditorAdvisories.Build(layout);

            Assert.Equal(string.Empty, advisories.SummaryFor(EditorTab.Macros));
            Assert.Equal(string.Empty, advisories.SummaryFor(EditorTab.Settings));
            Assert.NotEqual(string.Empty, advisories.SummaryFor(EditorTab.Keys, 0));
        }

        [Fact]
        public void RebuildingAfterAMutation_ChangesTheSet()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];

            Assert.Equal(0, EditorAdvisories.Build(layout).Total);

            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layer.Keys[0].OriginalKey);

            Assert.True(EditorAdvisories.Build(layout).Total >= 2);

            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].ClearRemap();

            Assert.Equal(0, EditorAdvisories.Build(layout).Total);
        }

        [Fact]
        public void EveryAdvisory_IsAdvisorySeverity()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var limit = layout.Device.Macros.MaxCharactersPerMacro!.Value;

            layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].Remap(layout.Layers[0].Keys[0].OriginalKey);
            FindMacroKey(layout).SetMacro(1, CreateMacro(layout, limit + 1));

            var advisories = EditorAdvisories.Build(layout);

            Assert.NotEmpty(advisories.All);
            Assert.All(advisories.All, advisory => Assert.Equal(StatusSeverity.Warning, advisory.Severity));
        }

        [Fact]
        public async Task TheEditor_RebuildsTheSetOnEveryMutationPath()
        {
            // RefreshCounters is the funnel every layout write already ends in, so hooking it is
            // what keeps the strip from going stale the moment the user edits.
            using var fixture = new EditorFixture();

            var editor = await fixture.LoadAsync();
            var layer = editor.SelectedLayer!;

            Assert.Equal(0, editor.Advisories.Total);
            Assert.False(editor.AdvisoryStrip.HasAdvisories);

            fixture.Remap(editor, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            Assert.True(editor.Advisories.Total >= 2);
            Assert.True(editor.AdvisoryStrip.HasAdvisories);
            Assert.Equal(editor.AdvisoryStrip.AdvisoryCount, editor.Advisories.ForTab(EditorTab.Keys, layer.Index).Count);
            Assert.Equal(AdvisoryText.Review(editor.AdvisoryStrip.AdvisoryCount), editor.AdvisoryStrip.ReviewCaption);

            editor.ResetLayoutCommand.Execute(null);

            Assert.Equal(0, editor.Advisories.Total);
            Assert.False(editor.AdvisoryStrip.HasAdvisories);
            Assert.Equal(string.Empty, editor.AdvisoryStrip.AdvisorySummary);
        }

        [Fact]
        public async Task TheEditor_MarksTheCapsTheAdvisoriesAnchorTo()
        {
            // Exposed for issue #91's cap badge and deliberately not rendered here — but it has to
            // be true, or the badge lands on the wrong caps the day it is drawn.
            using var fixture = new EditorFixture();

            var editor = await fixture.LoadAsync();
            var layer = editor.SelectedLayer!;

            Assert.All(layer.Keys, key => Assert.False(key.HasAdvisory));

            fixture.Remap(editor, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            Assert.True(layer.FindByIndex(TestLayouts.RgbDigitOneKeyIndex)!.HasAdvisory);
            Assert.True(layer.Keys[0].HasAdvisory);
            Assert.False(layer.FindByIndex(TestLayouts.RgbDigitTwoKeyIndex)!.HasAdvisory);

            editor.ResetLayoutCommand.Execute(null);

            Assert.All(layer.Keys, key => Assert.False(key.HasAdvisory));
        }

        [Fact]
        public async Task ReviewAdvisories_SelectsTheAnchoredKeysInTurnWithoutArmingTheKeyboard()
        {
            using var fixture = new EditorFixture();

            var editor = await fixture.LoadAsync();
            var layer = editor.SelectedLayer!;

            fixture.Remap(editor, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            var anchored = editor.Advisories
                .ForTab(EditorTab.Keys, layer.Index)
                .Select(advisory => advisory.KeyIndex!.Value)
                .ToArray();

            foreach (var expected in anchored)
            {
                editor.AdvisoryStrip.ReviewAdvisoriesCommand.Execute(null);

                Assert.Equal(expected, editor.SelectedKey?.Index);
                Assert.False(editor.IsListening);
            }

            editor.AdvisoryStrip.ReviewAdvisoriesCommand.Execute(null);

            Assert.Equal(anchored[0], editor.SelectedKey?.Index);
        }

        [Fact]
        public async Task ReviewAdvisories_WithNothingAnchored_IsANoOpRatherThanARefusal()
        {
            using var fixture = new EditorFixture();

            var editor = await fixture.LoadAsync();

            // No CanExecute: an advisory never disables anything, so the button is live and the
            // press simply does nothing.
            Assert.True(editor.AdvisoryStrip.ReviewAdvisoriesCommand.CanExecute(null));

            editor.AdvisoryStrip.ReviewAdvisoriesCommand.Execute(null);

            Assert.Null(editor.SelectedKey);
        }

        [Fact]
        public async Task ASaveWithAdvisories_StillSucceedsAndTheToastCountsThem()
        {
            using var fixture = new EditorFixture();

            var editor = await fixture.LoadAsync();
            var layer = editor.SelectedLayer!;

            fixture.Remap(editor, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            Assert.True(editor.Advisories.Total > 0);
            Assert.True(editor.SaveCommand.CanExecute(null));

            await editor.SaveCommand.ExecuteAsync(null);

            // Nothing blocked: the profile went to the drive, and no message box was raised.
            Assert.Equal(1, fixture.Session.SaveCallCount);
            Assert.Empty(fixture.Notifications.MessageBoxes);
            Assert.False(editor.IsDirty);

            var toast = Assert.Single(fixture.Notifications.Toasts);

            Assert.Equal(KeyboardEditorViewModel.SaveTitle, toast.Title);
            Assert.Equal(AdvisoryText.SavedWithAdvisories(editor.Advisories.Total), toast.Message);
        }

        [Fact]
        public async Task ASaveWithAdvisories_AppendsTheCountToTheDevicesOwnPostSaveWording()
        {
            using var fixture = new EditorFixture();

            fixture.PostSaveMessage = "Profile 1 written.";

            var editor = await fixture.LoadAsync();
            var layer = editor.SelectedLayer!;

            fixture.Remap(editor, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            await editor.SaveCommand.ExecuteAsync(null);

            var toast = Assert.Single(fixture.Notifications.Toasts);

            Assert.Equal(
                "Profile 1 written. " + AdvisoryText.SavedWithAdvisories(editor.Advisories.Total),
                toast.Message);
        }

        [Fact]
        public async Task ACleanSave_SaysNothingAboutAdvisories()
        {
            using var fixture = new EditorFixture();

            fixture.PostSaveMessage = "Profile 1 written.";

            var editor = await fixture.LoadAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal("Profile 1 written.", Assert.Single(fixture.Notifications.Toasts).Message);
        }

        [Fact]
        public async Task ASaveWithAdvisories_ToastsOnTheAmberFace()
        {
            // Mockup 1k draws two toasts, and "saved with n advisories" is the amber one. Sending
            // that sentence on the green success face said "all clear" while counting warnings.
            using var fixture = new EditorFixture();

            var editor = await fixture.LoadAsync();
            var layer = editor.SelectedLayer!;

            fixture.Remap(editor, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            Assert.True(editor.Advisories.Total > 0);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(ToastSeverity.Advisory, Assert.Single(fixture.Notifications.Toasts).Severity);
        }

        [Fact]
        public async Task ACleanSave_ToastsOnTheSuccessFace()
        {
            // The other half of the pair: nothing to remark on, so the device's own post-save
            // wording arrives green.
            using var fixture = new EditorFixture();

            fixture.PostSaveMessage = "Profile 1 written.";

            var editor = await fixture.LoadAsync();

            Assert.Equal(0, editor.Advisories.Total);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(ToastSeverity.Success, Assert.Single(fixture.Notifications.Toasts).Severity);
        }

        [Fact]
        public async Task TheStripsProjection_FollowsTheOpenTabAndTheShownLayer()
        {
            using var fixture = new EditorFixture();

            var editor = await fixture.LoadAsync();
            var layer = editor.SelectedLayer!;

            fixture.Remap(editor, TestLayouts.RgbDigitOneKeyIndex, layer.Keys[0].Key.OriginalKey);

            var onLayout = editor.AdvisoryStrip.AdvisoryCount;

            Assert.True(onLayout >= 2);

            editor.SelectedTab = EditorTab.Macros;

            Assert.Equal(0, editor.AdvisoryStrip.AdvisoryCount);
            Assert.Equal(string.Empty, editor.AdvisoryStrip.AdvisorySummary);

            editor.SelectedTab = EditorTab.Keys;

            Assert.Equal(onLayout, editor.AdvisoryStrip.AdvisoryCount);

            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            Assert.Equal(0, editor.AdvisoryStrip.AdvisoryCount);

            editor.SelectLayerCommand.Execute(editor.Layers[0]);

            Assert.Equal(onLayout, editor.AdvisoryStrip.AdvisoryCount);
        }

        private static void AssignTapAndHold(KeyboardLayout layout, int count)
        {
            var assigned = 0;
            var hold = TestLayouts.Gen1Key("lshft");
            var delay = layout.Device.TapAndHold.DelayMilliseconds!.Default;

            foreach (var key in layout.Layers[0].Keys)
            {
                if (assigned >= count)
                {
                    return;
                }

                if (key.SetTapAndHold(key.OriginalKey, hold, delay))
                {
                    assigned++;
                }
            }
        }

        private static Macro CreateMacro(KeyboardLayout layout, int keystrokes)
        {
            var macro = layout.CreateMacro();
            var key = TestLayouts.Gen1Key("a");

            for (var index = 0; index < keystrokes; index++)
            {
                macro.AddKeystroke(new Keystroke(key));
            }

            return macro;
        }

        private static KeyboardKey FindMacroKey(KeyboardLayout layout)
        {
            foreach (var key in layout.Layers[0].Keys)
            {
                if (key.CanAssignMacro && !key.IsMacro)
                {
                    return key;
                }
            }

            throw new InvalidOperationException("The device has no free position that accepts a macro.");
        }

        private static KeyboardKey FindTokenedKey(KeyboardLayer layer, TokenDialect dialect, int skip)
        {
            foreach (var key in layer.Keys)
            {
                if (!key.CanEdit || key.OriginalKey.GetToken(dialect).Length == 0)
                {
                    continue;
                }

                if (skip-- == 0)
                {
                    return key;
                }
            }

            throw new InvalidOperationException("The layer has too few editable positions with a token.");
        }

        /// <summary>
        /// One loaded Freestyle Edge RGB editor over the app-layer fakes, plus the two things these
        /// tests need to drive it: a remap through the real capture path, and a staged post-save
        /// message.
        /// </summary>
        private sealed class EditorFixture : IDisposable
        {
            /// <summary>Everything the editor put on screen — the save toast, in practice.</summary>
            public FakeNotificationService Notifications { get; } = new();

            /// <summary>The session the editor loaded; its <c>SaveCallCount</c> is what "it saved" means.</summary>
            public FakeProfileSession Session { get; }

            /// <summary>The device's own post-save wording, or null for the common case of none.</summary>
            public string? PostSaveMessage
            {
                get => Session.ResultToReturn.PostSaveMessage;
                set => Session.ResultToReturn = Session.ResultToReturn with { PostSaveMessage = value };
            }

            private readonly FakeProfileSessionFactory _profiles = new();
            private readonly FakeSettingsService _settings = new();
            private readonly FakeKeystrokeCaptureService _capture = new();
            private readonly FakeFolderPickerService _folderPicker = new();
            private readonly FakeFilePickerService _filePicker = new();
            private readonly FakeVDriveFileService _files = new();
            private readonly FakeUrlLauncher _urlLauncher = new();
            private readonly KeyboardEditorViewModel _editor;

            public EditorFixture()
            {
                Session = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

                _profiles.SessionToReturn = Session;

                _editor = new KeyboardEditorViewModel(
                    TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                    _profiles,
                    _settings,
                    _capture,
                    Notifications,
                    _folderPicker,
                    _filePicker,
                    _files,
                    _urlLauncher);
            }

            public async Task<KeyboardEditorViewModel> LoadAsync()
            {
                await _editor.LoadAsync();

                return _editor;
            }

            /// <summary>Select, listen, receive — the editor's own remap workflow.</summary>
            public void Remap(KeyboardEditorViewModel editor, int keyIndex, KeyDefinition assignment)
            {
                var key = editor.SelectedLayer!.FindByIndex(keyIndex)
                    ?? throw new InvalidOperationException($"The layer has no position {keyIndex}.");

                editor.SelectKeyCommand.Execute(key);
                editor.BeginRemapCommand.Execute(null);

                _capture.RaiseKeystroke(assignment);
            }

            public void Dispose()
            {
                _editor.Dispose();
            }
        }
    }
}
