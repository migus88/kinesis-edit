using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The message-box card: mockup 1k's button order and roles, the suppression opt-out, and the
    /// keyboard contract of specs/11-feature-dialogs.md §11.9 — which only works because the card
    /// takes focus when it arrives. As an in-app overlay rather than a window it has no title bar
    /// to fall back on, so a card that never received focus would leave Enter and Escape landing on
    /// whatever is behind the scrim.
    /// </summary>
    public class MessageBoxViewTests
    {
        [AvaloniaFact]
        public void TheButtons_AreOrderedCancelFirstThenTheAffirmatives()
        {
            // Mockup 1k, verbatim order: "Cancel", "Key data only", "Include macros" — the way out
            // first, the primary affirmative last.
            using var host = Show(CreateViewModel(), out var view);

            var buttons = Buttons(view);

            Assert.Equal(
                ["Cancel", "Key data only", "Include macros"],
                buttons.Select(button => button.Content as string));
        }

        [AvaloniaFact]
        public void TheAffirmative_IsTheOnlyButtonOnTheAccent()
        {
            // The accent means selection/focus or "you changed this" — here, the answer the dialog
            // expects. Every other answer is a secondary face.
            using var host = Show(CreateViewModel(), out var view);

            var buttons = Buttons(view);
            var affirmative = buttons[^1];
            var others = buttons.Take(buttons.Count - 1).ToArray();

            Assert.Equal("Include macros", affirmative.Content as string);
            Assert.Contains("primaryAction", affirmative.Classes);
            Assert.All(others, button => Assert.Contains("secondary", button.Classes));
            Assert.All(others, button => Assert.DoesNotContain("primaryAction", button.Classes));
        }

        [AvaloniaFact]
        public void TheCard_TakesFocusOnArrival_SoTheKeyboardContractCanReachIt()
        {
            using var host = Show(CreateViewModel(), out var view);

            var focused = Buttons(view).SingleOrDefault(button => button.IsFocused);

            Assert.NotNull(focused);
            Assert.Equal("Include macros", focused!.Content as string);
        }

        [AvaloniaFact]
        public void Escape_ClosesTheDialogWithItsEscapeResult()
        {
            var viewModel = CreateViewModel();

            using var host = Show(viewModel, out _);

            host.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(MessageBoxResult.Cancel, viewModel.Outcome!.Result);
        }

        [AvaloniaFact]
        public void Enter_ActivatesTheAffirmativeWhateverItIsCalled()
        {
            var viewModel = CreateViewModel();

            using var host = Show(viewModel, out _);

            host.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(MessageBoxResult.Yes, viewModel.Outcome!.Result);
        }

        [AvaloniaFact]
        public void TheSuppressionRow_IsPresentOnlyWhenTheRequestCarriesAKey()
        {
            using var withKey = Show(CreateViewModel(), out var boxed);
            using var withoutKey = Show(
                new MessageBoxViewModel(new MessageBoxRequest
                {
                    Title = "Saved",
                    Message = "Profile 1 Saved"
                }),
                out var bare);

            var suppression = boxed.GetVisualDescendants().OfType<CheckBox>().Single();

            Assert.Equal("Don't ask this again", suppression.Content as string);
            Assert.DoesNotContain(bare.GetVisualDescendants().OfType<CheckBox>(), box => box.IsEffectivelyVisible);
        }

        private static MessageBoxViewModel CreateViewModel()
        {
            return new MessageBoxViewModel(new MessageBoxRequest
            {
                Title = "Copy macros too?",
                Message = "You're copying F3 onto F8. F3 carries 3 macro slots.",
                Icon = MessageBoxIcon.Confirmation,
                Buttons = MessageBoxButtons.YesNoCancel,
                YesCaption = "Include macros",
                NoCaption = "Key data only",
                SuppressionKey = NotificationKeys.Save
            });
        }

        private static ThemedHost Show(MessageBoxViewModel viewModel, out MessageBoxView view)
        {
            var card = new MessageBoxView
            {
                DataContext = viewModel
            };

            var host = ThemedHost.Show(card, ThemeVariant.Dark);

            view = card;

            return host;
        }

        /// <summary>
        /// The card's answer buttons, in the order they are laid out. Typed exactly, because
        /// <see cref="CheckBox"/> derives from <see cref="Button"/> and the suppression opt-out is
        /// not one of the answers.
        /// </summary>
        private static IReadOnlyList<Button> Buttons(MessageBoxView view)
        {
            return view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.GetType() == typeof(Button) && button.IsEffectivelyVisible)
                .ToArray();
        }
    }
}
