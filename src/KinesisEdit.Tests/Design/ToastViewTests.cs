using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The toast card: the two variants mockup 1k draws and the <c>×</c> that closes one early.
    /// <para>
    /// The severity is the reason this suite exists rather than a view-model assertion. Both
    /// variants reach their colour through <b>class-order style precedence</b> —
    /// <c>Border.toast.advisory</c> and <c>Border.toastDot.advisory</c> are declared after the
    /// <c>overlayCard</c> and <c>toastDot</c> setters they have to beat — and a reordered stylesheet
    /// would silently paint an advisory in the success colours with every view-model test still
    /// green. Only a realised control answers that.
    /// </para>
    /// </summary>
    public class ToastViewTests
    {
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheSuccessToast_WearsTheOkDotAndTheOrdinaryCardLine(string variantName)
        {
            var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            using var scenes = new ViewSceneFactory();
            using var host = Show(scenes.CreateToast(), variant, out var view);

            Assert.Equal(DesignTokens.Resolve("StatusOkBrush", variant), Dot(view).Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), Card(view).BorderBrush);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheAdvisoryToast_WearsTheAmberDotAndTheAmberLine(string variantName)
        {
            // Mockup 1k's second toast, "Saved with 3 advisories". Amber is the only warning colour
            // in this app and an advisory never blocks — the card is the same card, dismissing
            // itself on the same dwell; only the dot and the line move.
            var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            using var scenes = new ViewSceneFactory();
            using var host = Show(scenes.CreateAdvisoryToast(), variant, out var view);

            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryBrush", variant), Dot(view).Background);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTintBorderBrush", variant), Card(view).BorderBrush);

            // ...and it is never the error ramp: a failure is a message box, not a notice that
            // vanishes after five seconds.
            Assert.NotEqual(DesignTokens.Resolve("StatusErrorBrush", variant), Dot(view).Background);
        }

        [AvaloniaFact]
        public void TheDismissMark_ClosesTheNoticeEarly()
        {
            using var scenes = new ViewSceneFactory();

            var toast = scenes.CreateToast();

            using var host = Show(toast, ThemeVariant.Dark, out var view);

            var dismissed = new List<ToastViewModel>();

            toast.DismissRequested += dismissed.Add;

            var close = view.GetVisualDescendants().OfType<Button>().Single();

            Assert.Equal(
                DesignTokens.Resolve("IconClose", ThemeVariant.Dark),
                ((Icon)close.Content!).Data);

            close.Command!.Execute(close.CommandParameter);

            Assert.Equal([toast], dismissed);
        }

        private static Border Card(ToastView view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("toast"));
        }

        private static Border Dot(ToastView view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("toastDot"));
        }

        private static ThemedHost Show(ToastViewModel viewModel, ThemeVariant variant, out ToastView view)
        {
            var card = new ToastView
            {
                DataContext = viewModel
            };

            var host = ThemedHost.Show(card, variant);

            view = card;

            return host;
        }
    }
}
