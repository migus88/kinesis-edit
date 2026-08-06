using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using KinesisEdit.Controls;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The one icon that moves: the busy spinner of <c>Styles/Icons.axaml</c> — the scanning mark,
    /// its own 14x14 box, and a 1.1s linear turn that never stops.
    /// <para>
    /// Its duration is written out in the style rather than bound, because an <c>Animation</c> is
    /// not part of the logical tree and a <c>DynamicResource</c> on it resolves to nothing and
    /// silently yields a zero-length loop. <c>DurationSpinnerRotation</c> is the budget's record of
    /// the number; this suite is what keeps the two from drifting apart.
    /// </para>
    /// </summary>
    public class SpinnerMotionTests
    {
        private const string StylesSource = "avares://KinesisEdit/Styles/Icons.axaml";

        private const string MotionSource = "avares://KinesisEdit/Themes/Motion.axaml";

        private const string FullSuffix = "Full";

        private const string DurationKey = "DurationSpinnerRotation";

        /// <summary>Long enough for the 1.1s turn to have moved well past its start.</summary>
        private const int TurnMilliseconds = 300;

        [AvaloniaFact]
        public void TheSpinnersDuration_InEachVariant_IsElevenTenthsOfASecond()
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(TimeSpan.FromSeconds(1.1), (TimeSpan)DesignTokens.Resolve(DurationKey, variant));
            }
        }

        [AvaloniaFact]
        public void TheSpinner_IsADurationOnlyMotion_WithNoFullOrReducedPair()
        {
            // Every *other* motion in the budget ships as a Full/Reduced pair that
            // MotionResourceBinder points a bare alias at. This one is a Style.Animations loop, not
            // a Transitions: there is no property change to interpolate, so there is nothing for a
            // reduced flavour to drop and no alias for a view to bind.
            Assert.False(DesignTokens.TryResolve("SpinnerRotationTransitions", ThemeVariant.Dark, out _));
            Assert.False(DesignTokens.TryResolve("SpinnerRotationTransitionsFull", ThemeVariant.Dark, out _));
            Assert.False(DesignTokens.TryResolve("SpinnerRotationTransitionsReduced", ThemeVariant.Dark, out _));
        }

        [AvaloniaFact]
        public void TheSpinnerStyle_TurnsOnceEveryBudgetedDuration_ForeverAndLinearly()
        {
            var animation = SpinnerAnimation();

            Assert.Equal((TimeSpan)DesignTokens.Resolve(DurationKey, ThemeVariant.Dark), animation.Duration);
            Assert.Equal(IterationType.Infinite, animation.IterationCount.RepeatType);

            // Linear, because any easing on a continuous rotation reads as a stutter once a turn.
            Assert.IsType<LinearEasing>(animation.Easing);
        }

        [AvaloniaFact]
        public void TheSpinnerStyle_RotatesAFullTurn()
        {
            var animation = SpinnerAnimation();

            Assert.Equal(2, animation.Children.Count);
            AssertKeyFrame(animation.Children[0], cue: 0, angle: 0);
            AssertKeyFrame(animation.Children[1], cue: 1, angle: 360);
        }

        [AvaloniaFact]
        public void TheSpinnerClass_CarriesTheScanningMarkAndItsOwnBox()
        {
            // `IconScanning` is the one mark NOT on the 16px grid: it is drawn in a 14x14 box
            // centred on (7,7), because SpinnerSize is 14. A Rect cannot be composed out of a
            // resource, so the box is written out in the style — and guarded here.
            using var host = ShowSpinner(out var icon);

            Assert.Same(DesignTokens.Resolve(IconCatalog.ScanningKey, ThemeVariant.Dark), icon.Data);
            Assert.Equal((double)DesignTokens.Resolve("SpinnerSize", ThemeVariant.Dark), icon.Size);
            Assert.Equal((double)DesignTokens.Resolve("SpinnerStrokeThickness", ThemeVariant.Dark), icon.StrokeThickness);
            Assert.Equal(IconCatalog.SpinnerBox, icon.SourceBox);

            // Rotating about anything but the centre of the box would make the arc orbit.
            Assert.Equal(RelativePoint.Center, icon.RenderTransformOrigin);
        }

        [AvaloniaFact]
        public async Task TheSpinnerClass_ProducesALiveRotation()
        {
            using var host = ShowSpinner(out var icon);

            host.Capture();

            var first = AngleOf(icon);

            Assert.NotNull(first);

            // Animations run on wall-clock time; ticking the render timer only samples it.
            await Task.Delay(TurnMilliseconds).ConfigureAwait(true);

            host.Capture();

            Assert.NotEqual(first, AngleOf(icon));
        }

        [AvaloniaFact]
        public async Task TheSpinner_UnderReduceMotion_KeepsSpinning()
        {
            // Deliberate, and the reason belongs where it is enforced: the spinner is not
            // decoration. It is the only affordance saying the app is still working, and a frozen
            // one reads as a hung app — a worse outcome for the very user reduce-motion exists to
            // protect. Reduced drops rises, slides and height changes; a status affordance that
            // carries information is not in that category.
            var application = Application.Current!;
            var restore = MotionAliases().ToDictionary(
                alias => alias,
                alias => application.Resources.TryGetValue(alias, out var value) ? value : null);

            try
            {
                MotionResourceBinder.Apply(application, new MotionSettings(new FakeReduceMotionDetector(true)));

                using var host = ShowSpinner(out var icon);

                host.Capture();

                var first = AngleOf(icon);

                Assert.NotNull(first);

                await Task.Delay(TurnMilliseconds).ConfigureAwait(true);

                host.Capture();

                Assert.NotEqual(first, AngleOf(icon));
            }
            finally
            {
                foreach (var (alias, value) in restore)
                {
                    application.Resources[alias] = value;
                }
            }
        }

        /// <summary>The <c>controls|Icon.spinner</c> style's single animation, as authored.</summary>
        private static Animation SpinnerAnimation()
        {
            var styles = (Styles)AvaloniaXamlLoader.Load(new Uri(StylesSource));

            var spinner = styles
                .OfType<Style>()
                .Single(style => style.Selector?.ToString()?.Contains(IconCatalog.SpinnerClass, StringComparison.Ordinal) == true);

            return Assert.IsType<Animation>(Assert.Single(spinner.Animations));
        }

        private static void AssertKeyFrame(KeyFrame frame, double cue, double angle)
        {
            Assert.Equal(cue, frame.Cue.CueValue);

            var setter = Assert.IsType<Setter>(Assert.Single(frame.Setters));

            Assert.Equal(RotateTransform.AngleProperty, setter.Property);
            Assert.Equal(angle, Assert.IsType<double>(setter.Value));
        }

        /// <summary>A spinner on screen, in a window just big enough to hold it.</summary>
        private static ThemedHost ShowSpinner(out Icon icon)
        {
            icon = new Icon
            {
                Classes = { IconCatalog.SpinnerClass },
                Stroke = (IBrush)DesignTokens.Resolve("AccentBrush", ThemeVariant.Dark)
            };

            return ThemedHost.Show(icon, ThemeVariant.Dark, 60, 60);
        }

        /// <summary>
        /// The rotation the animator drives. It creates the transform itself when a control has
        /// none, which is why this looks through a group rather than expecting a bare
        /// <see cref="RotateTransform"/>.
        /// </summary>
        private static double? AngleOf(Icon icon)
        {
            return icon.RenderTransform switch
            {
                RotateTransform rotate => rotate.Angle,
                TransformGroup group => group.Children.OfType<RotateTransform>().FirstOrDefault()?.Angle,
                _ => null
            };
        }

        /// <summary>
        /// The bare motion aliases MotionResourceBinder owns, so a test that re-points them can put
        /// them back. Derived from Motion.axaml's <c>...Full</c> keys, which is the same list the
        /// binder is built from and is readable from outside it.
        /// </summary>
        private static IReadOnlyList<string> MotionAliases()
        {
            var motion = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(MotionSource));

            return motion.Keys
                .Select(key => key.ToString() ?? string.Empty)
                .Where(key => key.EndsWith(FullSuffix, StringComparison.Ordinal))
                .Select(key => key[..^FullSuffix.Length])
                .ToArray();
        }
    }
}
