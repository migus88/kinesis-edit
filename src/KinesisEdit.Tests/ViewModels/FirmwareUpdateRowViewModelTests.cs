using KinesisEdit.Core.Firmware;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// One row of the "Check for Updates" dialog. Every label and button caption is pinned
    /// verbatim from specs/09-firmware.md §3, including the spaced colons of the row labels.
    /// </summary>
    public class FirmwareUpdateRowViewModelTests
    {
        private const string KeyboardLabel = "Keyboard Firmware :";
        private const string LightingLabel = "Lighting Firmware :";
        private const string AppLabel = "SmartSet App :";
        private const string CheckingCaption = "Checking for update...";
        private const string UpdateNowCaption = "Update Now";
        private const string UpToDateCaption = "No update available";
        private const string ReadErrorCaption = "Error reading firmware file";
        private const string KeyboardFetchErrorCaption = "Error fetching keyboard firmware";
        private const string LightingFetchErrorCaption = "Error fetching lighting firmware";
        private const string AppFetchErrorCaption = "Error fetching app version";
        private const string ConnectionErrorCaption = "Check connection";

        [Theory]
        [InlineData(UpdateRowKind.Keyboard, KeyboardLabel)]
        [InlineData(UpdateRowKind.Lighting, LightingLabel)]
        [InlineData(UpdateRowKind.App, AppLabel)]
        public void Label_PerRowKind_UsesTheSpecWording(UpdateRowKind kind, string expected)
        {
            Assert.Equal(expected, FirmwareUpdateRowViewModel.GetLabel(kind));
            Assert.Equal(expected, CreateRow(kind, UpdateRowState.Checking).Label);
        }

        [Theory]
        [InlineData(UpdateRowState.Checking, CheckingCaption)]
        [InlineData(UpdateRowState.UpdateAvailable, UpdateNowCaption)]
        [InlineData(UpdateRowState.UpToDate, UpToDateCaption)]
        [InlineData(UpdateRowState.LocalUnreadable, ReadErrorCaption)]
        [InlineData(UpdateRowState.ConnectionError, ConnectionErrorCaption)]
        public void ButtonCaption_PerState_UsesTheSpecWording(UpdateRowState state, string expected)
        {
            Assert.Equal(expected, CreateRow(UpdateRowKind.Keyboard, state).ButtonCaption);
            Assert.Equal(expected, CreateRow(UpdateRowKind.Lighting, state).ButtonCaption);
            Assert.Equal(expected, CreateRow(UpdateRowKind.App, state).ButtonCaption);
        }

        [Theory]
        [InlineData(UpdateRowKind.Keyboard, KeyboardFetchErrorCaption)]
        [InlineData(UpdateRowKind.Lighting, LightingFetchErrorCaption)]
        [InlineData(UpdateRowKind.App, AppFetchErrorCaption)]
        public void ButtonCaption_WhenTheRemoteValueIsMissing_NamesTheRow(UpdateRowKind kind, string expected)
        {
            Assert.Equal(expected, CreateRow(kind, UpdateRowState.RemoteMissing).ButtonCaption);
        }

        [Fact]
        public void IsActionable_OnlyForAnAvailableUpdate_IsTrue()
        {
            foreach (var state in Enum.GetValues<UpdateRowState>())
            {
                var row = CreateRow(UpdateRowKind.Keyboard, state);

                Assert.Equal(state == UpdateRowState.UpdateAvailable, row.IsActionable);
                Assert.Equal(state == UpdateRowState.UpdateAvailable, row.OpenCommand.CanExecute(null));
            }
        }

        [Fact]
        public void IsActionable_WithoutATargetPage_IsFalse()
        {
            var row = new FirmwareUpdateRowViewModel(
                new UpdateCheckRow
                {
                    Kind = UpdateRowKind.Keyboard,
                    State = UpdateRowState.UpdateAvailable,
                    TargetUrl = null
                },
                new FakeUrlLauncher());

            Assert.False(row.IsActionable);
        }

        [Fact]
        public void OpenCommand_ForAnAvailableUpdate_OpensTheSupportPage()
        {
            var launcher = new FakeUrlLauncher();
            var row = new FirmwareUpdateRowViewModel(
                new UpdateCheckRow
                {
                    Kind = UpdateRowKind.Keyboard,
                    State = UpdateRowState.UpdateAvailable,
                    TargetUrl = "https://example.test/#firmware"
                },
                launcher);

            row.OpenCommand.Execute(null);

            Assert.Equal("https://example.test/#firmware", Assert.Single(launcher.OpenedUrls));
        }

        [Fact]
        public void OpenCommand_ForANonActionableRow_OpensNothing()
        {
            var launcher = new FakeUrlLauncher();

            foreach (var state in Enum.GetValues<UpdateRowState>())
            {
                if (state == UpdateRowState.UpdateAvailable)
                {
                    continue;
                }

                new FirmwareUpdateRowViewModel(
                    new UpdateCheckRow
                    {
                        Kind = UpdateRowKind.Keyboard,
                        State = state,
                        TargetUrl = "https://example.test/#firmware"
                    },
                    launcher).OpenCommand.Execute(null);
            }

            Assert.Empty(launcher.OpenedUrls);
        }

        private static FirmwareUpdateRowViewModel CreateRow(UpdateRowKind kind, UpdateRowState state)
        {
            return new FirmwareUpdateRowViewModel(
                new UpdateCheckRow
                {
                    Kind = kind,
                    State = state,
                    TargetUrl = "https://example.test/#firmware"
                },
                new FakeUrlLauncher());
        }
    }
}
