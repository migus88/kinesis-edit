using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class MessageBoxViewModelTests
    {
        [Fact]
        public void IsSuppressionAvailable_WithoutSuppressionKey_IsFalse()
        {
            var viewModel = new MessageBoxViewModel(CreateRequest());

            Assert.False(viewModel.IsSuppressionAvailable);
        }

        [Fact]
        public void IsSuppressionAvailable_WithSuppressionKey_IsTrue()
        {
            var viewModel = new MessageBoxViewModel(CreateRequest(NotificationKeys.Save));

            Assert.True(viewModel.IsSuppressionAvailable);
            Assert.Equal("Hide this notification?", MessageBoxViewModel.SuppressionCaption);
        }

        [Fact]
        public void OkCommand_WhenExecuted_CompletesWithOk()
        {
            var viewModel = new MessageBoxViewModel(CreateRequest());
            MessageBoxOutcome? completed = null;
            viewModel.Completed += outcome => completed = outcome;

            viewModel.OkCommand.Execute(null);

            Assert.Equal(MessageBoxResult.Ok, viewModel.Outcome!.Result);
            Assert.False(viewModel.Outcome.SuppressRequested);
            Assert.Same(viewModel.Outcome, completed);
        }

        [Fact]
        public void Complete_WithCheckedSuppression_ReportsSuppressRequested()
        {
            var viewModel = new MessageBoxViewModel(CreateRequest(NotificationKeys.Save))
            {
                IsSuppressChecked = true
            };

            var outcome = viewModel.Complete(MessageBoxResult.Ok);

            Assert.True(outcome.SuppressRequested);
        }

        [Fact]
        public void Complete_WithCheckedSuppressionButNoKey_DoesNotReportSuppressRequested()
        {
            var viewModel = new MessageBoxViewModel(CreateRequest())
            {
                IsSuppressChecked = true
            };

            var outcome = viewModel.Complete(MessageBoxResult.Ok);

            Assert.False(outcome.SuppressRequested);
        }

        [Fact]
        public void CustomButtonCommand_WhenExecuted_ReportsTheButtonId()
        {
            var button = new MessageBoxButton
            {
                Id = "update-firmware",
                Caption = "Update Firmware"
            };
            var viewModel = new MessageBoxViewModel(CreateRequest() with
            {
                CustomButtons = [button]
            });

            viewModel.CustomButtonCommand.Execute(button);

            Assert.Equal(MessageBoxResult.Custom, viewModel.Outcome!.Result);
            Assert.Equal("update-firmware", viewModel.Outcome.CustomButtonId);
        }

        [Fact]
        public void Complete_CalledTwice_KeepsTheFirstOutcome()
        {
            var viewModel = new MessageBoxViewModel(CreateRequest());
            var completions = 0;
            viewModel.Completed += _ => completions++;

            var first = viewModel.Complete(MessageBoxResult.Yes);
            var second = viewModel.Complete(MessageBoxResult.No);

            Assert.Same(first, second);
            Assert.Equal(1, completions);
        }

        [Theory]
        [InlineData(MessageBoxButtons.Ok, true, false, false, false)]
        [InlineData(MessageBoxButtons.OkCancel, true, true, false, false)]
        [InlineData(MessageBoxButtons.YesNo, false, false, true, true)]
        [InlineData(MessageBoxButtons.YesNoCancel, false, true, true, true)]
        public void ButtonVisibility_PerButtonSet_MatchesTheStandardSets(
            MessageBoxButtons buttons,
            bool showsOk,
            bool showsCancel,
            bool showsYes,
            bool showsNo)
        {
            var viewModel = new MessageBoxViewModel(CreateRequest() with
            {
                Buttons = buttons
            });

            Assert.Equal(showsOk, viewModel.ShowsOk);
            Assert.Equal(showsCancel, viewModel.ShowsCancel);
            Assert.Equal(showsYes, viewModel.ShowsYes);
            Assert.Equal(showsNo, viewModel.ShowsNo);
        }

        [Fact]
        public void AcceptAndEscapeResults_PerButtonSet_FollowTheEnterAndEscapeRule()
        {
            var okOnly = new MessageBoxViewModel(CreateRequest());
            var yesNoCancel = new MessageBoxViewModel(CreateRequest() with
            {
                Buttons = MessageBoxButtons.YesNoCancel
            });

            Assert.Equal(MessageBoxResult.Ok, okOnly.AcceptResult);
            Assert.Equal(MessageBoxResult.None, okOnly.EscapeResult);
            Assert.True(okOnly.HasAcceptButton);
            Assert.Equal(MessageBoxResult.Yes, yesNoCancel.AcceptResult);
            Assert.Equal(MessageBoxResult.Cancel, yesNoCancel.EscapeResult);
            Assert.True(yesNoCancel.HasAcceptButton);
        }

        [Fact]
        public void AcceptResult_WithCustomButtonsOnly_HasNothingToActivate()
        {
            // Spec 11.9's 'Update Firmware' shape: no standard buttons, so Enter must not answer
            // the dialog with a result none of its visible buttons offers.
            var viewModel = new MessageBoxViewModel(CreateRequest() with
            {
                Buttons = MessageBoxButtons.None,
                CustomButtons =
                [
                    new MessageBoxButton
                    {
                        Id = "update-firmware",
                        Caption = "Update Firmware"
                    }
                ]
            });

            Assert.False(viewModel.HasAcceptButton);
            Assert.Equal(MessageBoxResult.None, viewModel.AcceptResult);
            Assert.False(viewModel.ShowsOk);
            Assert.False(viewModel.ShowsYes);
        }

        private static MessageBoxRequest CreateRequest(string? suppressionKey = null)
        {
            return new MessageBoxRequest
            {
                Title = "Save",
                Message = "Profile 1 Saved",
                SuppressionKey = suppressionKey
            };
        }
    }
}
