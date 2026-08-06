using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Core.Input;
using KinesisEdit.Input;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The overlay's two hard claims, driven through Avalonia's own input pipeline rather than
    /// through a property: that it swallows <b>nothing</b> when nothing is up, and that it swallows
    /// <b>everything</b> when something modal is. The first is the one a green suite hides — the
    /// root used to be <c>IsHitTestVisible="False"</c> outright, and taking that off makes any
    /// full-bleed child of it a dashboard-wide dead zone that no rendering test can see.
    /// <para>
    /// It also guards the toast lifetime: an early <c>×</c> has to <i>cancel</i> the pending dwell,
    /// not race it.
    /// </para>
    /// </summary>
    public class NotificationOverlayTests
    {
        /// <summary>Long enough for a 140/180 ms fade to have finished on the wall clock.</summary>
        private const int FadeSettleMilliseconds = 400;

        [AvaloniaFact]
        public void WithNothingUp_TheOverlaySwallowsNoClickAimedAtTheContentBehindIt()
        {
            // The regression this exists for: one full-bleed transparent element in the overlay and
            // every button on the dashboard stops responding, silently, with every test still green.
            using var scene = Build(out var overlay, out var target);

            Click(scene, target);

            Assert.True(target.IsFocused, "The click never reached the content under the overlay.");
            Assert.False(overlay.IsHitTestVisible is false, "The overlay is back to refusing input wholesale.");
        }

        [AvaloniaFact]
        public async Task WhileAMessageBoxIsUp_TheScrimSwallowsAClickAimedAtTheContentBehindIt()
        {
            using var scene = Build(out var overlay, out var target);

            var answer = overlay.PresentAsync(CreateRequest());

            await SettleAsync();

            Click(scene, target);

            Assert.False(target.IsFocused, "A click reached the shell from behind a modal dialog.");
            Assert.False(answer.IsCompleted, "The box answered itself.");

            overlay.MessageBox!.Complete(MessageBoxResult.Cancel);

            Assert.Equal(MessageBoxResult.Cancel, (await answer).Result);
        }

        [AvaloniaFact]
        public async Task WhileTheLoadingCardIsUp_TheScrimBlocksToo()
        {
            // Mockup 1k calls it "Loading · blocking": the same scrim, so the app takes no input
            // while a save or a load is in flight.
            using var scene = Build(out var overlay, out var target);
            var notifications = new FakeNotificationService();

            overlay.Attach(notifications);
            notifications.ShowLoading("Loading Advantage 360…");

            await SettleAsync();

            Click(scene, target);

            Assert.False(target.IsFocused, "A click reached the shell from behind the loading card.");

            notifications.HideLoading();

            await SettleAsync();

            Click(scene, target);

            Assert.True(target.IsFocused, "The scrim stayed up after the loading card came down.");
        }

        [AvaloniaFact]
        public async Task AnAnsweredMessageBox_CompletesTheAwaitingCallerWithItsOutcome()
        {
            using var scene = Build(out var overlay, out _);

            var answer = overlay.PresentAsync(CreateRequest());

            await SettleAsync();

            Assert.NotNull(overlay.MessageBox);

            overlay.MessageBox!.IsSuppressChecked = true;
            overlay.MessageBox.YesCommand.Execute(null);

            var outcome = await answer;

            Assert.Equal(MessageBoxResult.Yes, outcome.Result);
            Assert.True(outcome.SuppressRequested);
            Assert.Null(overlay.MessageBox);
        }

        [AvaloniaFact]
        public async Task ASecondMessageBox_IsQueuedBehindTheFirstRatherThanStackedOnIt()
        {
            // Two cards centred on one scrim would hide one another, and answering a box nobody can
            // see is worse than making them answer the first one.
            using var scene = Build(out var overlay, out _);

            var first = overlay.PresentAsync(CreateRequest("First"));
            var second = overlay.PresentAsync(CreateRequest("Second"));

            await SettleAsync();

            Assert.Equal("First", overlay.MessageBox!.Title);
            Assert.False(second.IsCompleted);

            overlay.MessageBox.YesCommand.Execute(null);

            Assert.Equal(MessageBoxResult.Yes, (await first).Result);
            Assert.Equal("Second", overlay.MessageBox!.Title);

            overlay.MessageBox.NoCommand.Execute(null);

            Assert.Equal(MessageBoxResult.No, (await second).Result);
        }

        [AvaloniaFact]
        public async Task AToastDismissedEarly_LeavesAtOnceAndTakesItsPendingDwellWithIt()
        {
            // The trap: the dwell and the fade-out are two chained one-shot timers. A `×` that only
            // hid the toast would leave the dwell to fire afterwards and schedule a second removal.
            using var scene = Build(out var overlay, out _);
            var notifications = new FakeNotificationService();

            overlay.Attach(notifications);

            notifications.ShowToast(new ToastRequest
            {
                Title = "Dismissed",
                Message = "Closed by hand.",
                Timeout = TimeSpan.FromMilliseconds(120)
            });

            var dismissed = Assert.Single(overlay.CornerToasts);

            dismissed.Dismiss();

            Assert.False(dismissed.IsShown, "The `×` did not start the fade.");
            Assert.Contains(dismissed, overlay.CornerToasts);

            // A second toast, added while the first is still fading, must survive the first one's
            // now-cancelled dwell.
            notifications.ShowToast(new ToastRequest
            {
                Title = "Saved",
                Message = "Written to the v-Drive."
            });

            await SettleAsync();

            var survivor = Assert.Single(overlay.CornerToasts);

            Assert.Equal("Saved", survivor.Title);
        }

        [AvaloniaFact]
        public async Task AToastLeftAlone_DwellsThenLeavesOnItsOwn()
        {
            using var scene = Build(out var overlay, out _);
            var notifications = new FakeNotificationService();

            overlay.Attach(notifications);

            notifications.ShowToast(new ToastRequest
            {
                Message = "Safe To Remove Hardware",
                Timeout = TimeSpan.FromMilliseconds(60)
            });

            Assert.Single(overlay.CornerToasts);

            await SettleAsync();

            Assert.Empty(overlay.CornerToasts);
        }

        [AvaloniaFact]
        public void InTheShell_TheOverlaySpansBothRowsSoAModalCoversTheAppBar()
        {
            // Left in row 1 the overlay would dim the Home / Settings / Help pills and the status
            // chip but leave them clickable, and a dialog you can navigate out from behind is not
            // modal.
            using var scenes = new ViewSceneFactory();
            var window = new MainWindow(scenes.CreateShell(), scenes.Notifications);

            using var host = ThemedHost.Show(window, ThemeVariant.Dark);

            var overlay = window.GetControl<NotificationOverlay>("Notifications");

            Assert.Equal(0, Grid.GetRow(overlay));
            Assert.Equal(2, Grid.GetRowSpan(overlay));
        }

        [AvaloniaFact]
        public async Task InTheShell_TheAppBarIsClickableAtRestAndUnreachableUnderAModal()
        {
            // The row span above is the mechanism; this is the fact. Home is the pill to drive it
            // with — Settings and Help are still their own issue — and it has to survive both
            // ways: the overlay covering the bar must not cost the bar its clicks at rest.
            using var scenes = new ViewSceneFactory();
            var window = new MainWindow(scenes.CreateShell(), scenes.Notifications);

            using var host = ThemedHost.Show(window, ThemeVariant.Dark);

            var overlay = window.GetControl<NotificationOverlay>("Notifications");
            var home = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Classes.Contains("navPill") && (button.Content as string) == "Home");

            Click(host, home);

            Assert.True(home.IsFocused, "The overlay swallowed a click on the app bar with nothing up.");

            var answer = overlay.PresentAsync(CreateRequest());

            await SettleAsync();

            // Focus is on the box's own affirmative by now; the click must not move it.
            Click(host, home);

            Assert.False(home.IsFocused, "A nav pill took a click from behind a modal dialog.");
            Assert.False(answer.IsCompleted, "The box answered itself.");

            overlay.MessageBox!.Complete(MessageBoxResult.Cancel);

            await answer;
        }

        [AvaloniaFact]
        public async Task WhileCaptureIsLive_AMessageBoxStillAnswersToEscape()
        {
            // The regression the move off a second Window created. The capture service previews the
            // shell's TopLevel in the tunnel phase with `handledEventsToo`, so with a key listening
            // for a remap it swallows the dialog's Escape before the card's own handler runs — and
            // assigns it to the listening key instead of closing the box. MessageBoxPresenter is
            // where that is bracketed, so this drives the box through it rather than through the
            // overlay directly.
            using var scene = Build(out var overlay, out _);
            using var capture = new AvaloniaKeystrokeCaptureService(scene.Window);

            var captured = new List<CapturedKeystroke>();

            capture.KeystrokeCaptured += captured.Add;
            capture.Start();

            var presenter = new MessageBoxPresenter(() => overlay, () => capture);
            var answer = presenter.PresentAsync(CreateRequest());

            await SettleAsync();

            Assert.True(capture.IsSuspended, "The box went up with capture still eating the keyboard.");

            scene.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            scene.Window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(MessageBoxResult.Cancel, (await answer).Result);
            Assert.Empty(captured);

            // ...and the editor gets its keyboard back the moment the card is gone.
            Assert.False(capture.IsSuspended);
            Assert.True(capture.IsCapturing);
        }

        private static MessageBoxRequest CreateRequest(string title = "Copy macros too?")
        {
            return new MessageBoxRequest
            {
                Title = title,
                Message = "You're copying F3 onto F8. F3 carries 3 macro slots.",
                Icon = MessageBoxIcon.Confirmation,
                Buttons = MessageBoxButtons.YesNoCancel,
                SuppressionKey = NotificationKeys.Save
            };
        }

        /// <summary>
        /// The shell's own shape in miniature: something clickable, with the overlay laid over the
        /// whole of it.
        /// <para>
        /// The target sits in the <b>top-left corner</b>, and that is the whole point of it. A
        /// centred target is covered by the message box's own 330px card and by the loading card
        /// too, so every "the scrim swallowed it" assertion would pass on a build with no scrim at
        /// all — the card ate the click. Up here nothing but the scrim is ever over the button, so
        /// the assertions are about the thing they name. It is also clear of the bottom-right
        /// toasts.
        /// </para>
        /// </summary>
        private static ThemedHost Build(out NotificationOverlay overlay, out Button target)
        {
            var button = new Button
            {
                Classes = { "secondary" },
                Content = "Configure",
                Width = 200,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12)
            };
            var notifications = new NotificationOverlay();
            var root = new Panel
            {
                Children = { button, notifications }
            };

            var host = ThemedHost.Show(root, ThemeVariant.Dark);

            overlay = notifications;
            target = button;

            return host;
        }

        private static void Click(ThemedHost host, Control target)
        {
            var centre = target.TranslatePoint(
                    new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
                    host.Window)
                ?? throw new InvalidOperationException("The target is not in the window's visual tree.");

            host.Window.MouseMove(centre);
            host.Window.MouseDown(centre, MouseButton.Left);
            host.Window.MouseUp(centre, MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>
        /// Waits out a fade on the <b>wall clock</b>. Transitions and one-shot dispatcher timers run
        /// on real elapsed time; ticking the render timer does not advance either
        /// (docs/app/design-system.md, "Rendered-frame capture").
        /// </summary>
        private static async Task SettleAsync()
        {
            await Task.Delay(FadeSettleMilliseconds);

            Dispatcher.UIThread.RunJobs();
        }
    }
}
