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
            // expects. On a box that names no destructive answer every other answer is secondary;
            // the claim that survives a red Discard is the one about the accent, which is why the
            // test below asserts only that half on the destructive card. Red is the error ramp,
            // not the accent.
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
        public void TheCard_TakesTheWideWidthOnlyWhenTheRequestAsksForIt()
        {
            // 330 is the ordinary box, 420 the leave-with-unsaved modal (docs/design/handoff.md).
            // Measured off the laid-out card rather than off its Width property: the width comes
            // from a style now, and a bridge that stopped matching would leave the property unset
            // and the card stretched to the window.
            using var ordinaryHost = Show(CreateViewModel(), out var ordinary);
            using var wideHost = Show(new MessageBoxViewModel(CreateUnsavedChangesRequest()), out var wide);

            Assert.Equal(330, Card(ordinary).Bounds.Width);
            Assert.Equal(420, Card(wide).Bounds.Width);
        }

        [AvaloniaFact]
        public void TheDestructiveAnswer_IsRedWhileTheAffirmativeKeepsTheAccent()
        {
            // Mockup 1f's "Cancel · Discard · Save": Discard loses data, so it is the app's one red
            // button, and Save still commits — it must not lose the accent to it.
            using var host = Show(new MessageBoxViewModel(CreateUnsavedChangesRequest()), out var view);

            var buttons = Buttons(view);

            Assert.Equal(["Cancel", "Discard", "Save"], buttons.Select(button => button.Content as string));

            var discard = buttons[1];
            var save = buttons[^1];

            Assert.Contains("discard", discard.Classes);
            Assert.DoesNotContain("secondary", discard.Classes);
            Assert.Same(Theme("DiscardButton"), discard.Theme);

            Assert.Contains("primaryAction", save.Classes);
            Assert.DoesNotContain("discard", save.Classes);
            Assert.Same(Theme("PrimaryActionButton"), save.Theme);

            // The way out is neither: a Cancel that loses nothing may not read as the one that does.
            Assert.Contains("secondary", buttons[0].Classes);
            Assert.DoesNotContain("discard", buttons[0].Classes);
        }

        [AvaloniaFact]
        public void TheSuppressionCaption_FollowsTheRequest()
        {
            using var host = Show(new MessageBoxViewModel(CreateUnsavedChangesRequest()), out var view);

            var suppression = view.GetVisualDescendants().OfType<CheckBox>().Single();

            Assert.Equal("Don't ask again — always save on leaving", suppression.Content as string);
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

        /// <summary>Mockup 1f's leave-with-unsaved modal — the wide card with a destructive answer.</summary>
        private static MessageBoxRequest CreateUnsavedChangesRequest()
        {
            return new MessageBoxRequest
            {
                Title = "Save changes before leaving?",
                Message = "You've edited 7 keys across 2 layers.",
                Icon = MessageBoxIcon.Warning,
                Buttons = MessageBoxButtons.YesNoCancel,
                YesCaption = "Save",
                NoCaption = "Discard",
                IsWide = true,
                DestructiveResult = MessageBoxResult.No,
                SuppressionKey = NotificationKeys.UnsavedChanges,
                SuppressionCaption = "Don't ask again — always save on leaving",
                SuppressionResult = MessageBoxResult.Yes
            };
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

        /// <summary>The card itself — the one border carrying the message box's own width class.</summary>
        private static Border Card(MessageBoxView view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("messageBoxCard"));
        }

        private static ControlTheme Theme(string key)
        {
            return (ControlTheme)DesignTokens.Resolve(key, ThemeVariant.Dark);
        }
    }
}
