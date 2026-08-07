using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The key inspector's locked-key panel (mockup <c>2h</c>): the verbatim copy, the three dead
    /// tabs and their reasons, the per-device facts, and the copy asymmetry.
    /// </summary>
    public class LockedKeyPanelViewModelTests
    {
        [Fact]
        public void TheStateLineAndExplanation_AreMockup2HsVerbatimCopy()
        {
            // Copy is final (docs/design/README.md). The last clause of the explanation is the
            // load-bearing one: this is one position, not a device-wide refusal.
            Assert.Equal("Locked position", LockedKeyPanelViewModel.StateLine);
            Assert.Equal(
                "This key is the board's own configuration key. Its behaviour lives in firmware, not "
                + "in the layout files, so nothing here can be written — including on a device where "
                + "every other position is free.",
                LockedKeyPanelViewModel.Explanation);
            // A section label is authored uppercase at the call site: Avalonia has no
            // text-transform, and the mock reaches this exact string through a CSS one.
            Assert.Equal("WHAT IT DOES ON THE BOARD", LockedKeyPanelViewModel.HotkeySectionTitle);
        }

        [Fact]
        public void TheThreeTabs_AreDisabledEachWithItsReason()
        {
            // The design's own exception to "absent features are not shown, not disabled" — and the
            // reason is what makes it legal, so it is asserted and not merely allowed.
            var panel = Create();

            Assert.Equal(
                new[] { KeyInspectorMode.Remap, KeyInspectorMode.TapAndHold, KeyInspectorMode.Macro },
                panel.Tabs.Select(tab => tab.Mode));

            Assert.All(panel.Tabs, tab => Assert.False(tab.IsEnabled));
            Assert.All(panel.Tabs, tab => Assert.Equal(KeyInspectorTabViewModel.NotWritableReason, tab.DisabledReason));

            Assert.Equal(
                new[] { "Remap — not writable", "Tap & hold — not writable", "Macro — not writable" },
                panel.Tabs.Select(tab => tab.DisplayCaption));
        }

        [Fact]
        public void ADeadTabCannotBeBuiltWithoutAReason()
        {
            // The guard behind the law: a bare grey tab is the promise-and-refusal the design bans.
            Assert.Throws<ArgumentException>(
                () => KeyInspectorTabViewModel.Disabled(KeyInspectorMode.Remap, "  "));
        }

        [Fact]
        public void TheBoardFacts_ComeFromTheDeviceRatherThanFromTheView()
        {
            var panel = Create();

            panel.Refresh(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            Assert.True(panel.HasHotkeys);
            Assert.Equal(DeviceHotkeyCatalog.ForDevice(DeviceId.FreestyleEdgeRgb), panel.Hotkeys);
            Assert.Contains(panel.Hotkeys, fact => fact.Text == "hold + F8 mount the v-Drive");
        }

        [Fact]
        public void TheBoardFacts_FollowTheBoard()
        {
            // The whole reason DeviceHotkeyCatalog exists: a panel that hard-coded one board's
            // answer would teach the wrong shortcut on the next one.
            var panel = Create();

            panel.Refresh(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            var rgb = panel.Hotkeys;

            panel.Refresh(KeyboardLayout.Create(DeviceId.Tko));

            Assert.NotEqual(rgb, panel.Hotkeys);
        }

        [Fact]
        public void WithNothingLoaded_TheSectionIsAbsentRatherThanEmpty()
        {
            var panel = Create();

            panel.Refresh(layout: null);

            Assert.Empty(panel.Hotkeys);
            Assert.False(panel.HasHotkeys);
        }

        [Fact]
        public void TheCopyAsymmetry_DisablesOneDirectionRatherThanTheWholeAction()
        {
            // "A locked key can still be a copy source, never a target." Only the source direction
            // is wired to a command here; the refused direction is drawn dead beside its reason,
            // which the view asserts.
            var copy = new RelayCommand(() => { });
            var panel = new LockedKeyPanelViewModel(copy);

            Assert.Same(copy, panel.CopyToCommand);
            Assert.Equal("Copy from…", LockedKeyPanelViewModel.CopyFromCaption);
            Assert.Equal("Copy to…", LockedKeyPanelViewModel.CopyToCaption);
            Assert.Equal(
                "A locked key can still be a copy source, never a target.",
                LockedKeyPanelViewModel.CopyDirectionReason);
        }

        [Fact]
        public void ThePanelRunsTheEditorsOwnCopyCommand_NotASecondCopyPath()
        {
            var runs = 0;
            var panel = new LockedKeyPanelViewModel(new RelayCommand(() => runs++));

            panel.CopyToCommand.Execute(null);

            Assert.Equal(1, runs);
        }

        [Fact]
        public void ThePanelNeedsACopyCommand()
        {
            Assert.Throws<ArgumentNullException>(() => new LockedKeyPanelViewModel(null!));
        }

        private static LockedKeyPanelViewModel Create()
        {
            return new LockedKeyPanelViewModel(new RelayCommand(() => { }));
        }
    }
}
