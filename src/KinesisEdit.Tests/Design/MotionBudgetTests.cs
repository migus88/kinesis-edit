using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The motion budget of docs/design/mockups.md (mockup 2b) is a budget, not a suggestion:
    /// nothing may animate for longer than the table says, and nothing may animate that is not in
    /// it. This suite pins the durations, proves the two deliberate zeros — the tab swap and the
    /// layer swap — and proves the reduced set really is fades only.
    /// </summary>
    public class MotionBudgetTests
    {
        private const string MotionSource = "avares://KinesisEdit/Themes/Motion.axaml";

        private const string ReducedSuffix = "Reduced";

        private const string FullSuffix = "Full";

        /// <summary>
        /// <see cref="ITransition"/> hides both of these behind an explicit implementation; only
        /// the generic <c>Transition&lt;T&gt;</c> that implements it declares them publicly, and
        /// the collection is untyped. Reading them by name is the only way to inspect a set of
        /// transitions without knowing which value type each one animates.
        /// </summary>
        private const string DurationPropertyName = "Duration";

        /// <inheritdoc cref="DurationPropertyName" />
        private const string AnimatedPropertyName = "Property";

        /// <summary>
        /// The properties a fade may touch. Everything else — a transform, a size, a margin — is a
        /// movement, which is exactly what "fades only, no rise" rules out.
        /// </summary>
        private static readonly AvaloniaProperty[] _fadeProperties =
        [
            Visual.OpacityProperty,
            Border.BackgroundProperty,
            Border.BorderBrushProperty,
            TextBlock.ForegroundProperty
        ];

        [AvaloniaTheory]
        [InlineData("DurationStatusCrossFade", 220)]
        [InlineData("DurationListInsert", 160)]
        [InlineData("DurationTabSwap", 0)]
        [InlineData("DurationPopoverIn", 120)]
        [InlineData("DurationModalIn", 140)]
        [InlineData("DurationToast", 180)]
        [InlineData("DurationToastDwell", 5000)]
        [InlineData("DurationRecordingPulse", 1400)]

        // The lighting preview's FRAME INTERVAL — not a transition and not an animation, but a
        // budget entry all the same: the Lighting board repaints ~95 caps on a timer, and
        // "nothing may animate that is not in the table" cannot hold if the table has no row for
        // the one thing in the app that animates every frame. ~30fps, which is past the point
        // where a wash across a keyboard reads as continuous.
        [InlineData("DurationLightingPreviewFrame", 33)]
        public void Duration_IsTheBudgetsExactValue(string key, double milliseconds)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(
                    TimeSpan.FromMilliseconds(milliseconds),
                    (TimeSpan)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaFact]
        public void TheTabAndLayerSwap_IsZero()
        {
            // "Layer switching is deliberately unanimated - the spec says it will be used
            //  constantly, and 200 ms x 200 switches is the difference between an instrument and a
            //  website."
            Assert.Equal(TimeSpan.Zero, (TimeSpan)DesignTokens.Resolve("DurationTabSwap", ThemeVariant.Dark));
        }

        [AvaloniaFact]
        public void NoTabSwapTransitionsResource_Exists()
        {
            // A zero-duration transition is not the same thing as no transition: it still costs a
            // frame of bookkeeping per switch. There must be nothing for a container to bind.
            Assert.False(DesignTokens.TryResolve("TabSwapTransitions", ThemeVariant.Dark, out _));
            Assert.False(DesignTokens.TryResolve("TabSwapTransitionsFull", ThemeVariant.Dark, out _));
            Assert.False(DesignTokens.TryResolve("LayerSwapTransitions", ThemeVariant.Dark, out _));
        }

        [AvaloniaFact]
        public async Task NoLayerOrTabContainerInTheEditor_CarriesATransitions()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var containers = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(IsLayerOrTabContainer)
                .ToArray();

            // A guard that matched nothing would pass for the wrong reason, and what it has to match
            // changed when the switch became a ListBox and the strip a TabStrip: the trough, the
            // strip, and one generated container per layer and per tab.
            Assert.True(
                containers.Length >= 6,
                $"Only {containers.Length} layer/tab containers were found; the guard is not reaching them.");

            var animated = containers
                .Where(control => control.Transitions is { Count: > 0 })
                .Select(Describe)
                .ToArray();

            Assert.True(animated.Length == 0, $"These carry a Transitions: {string.Join(", ", animated)}.");
        }

        [AvaloniaFact]
        public void NoAuthoredMarkup_DeclaresAPageTransition()
        {
            // The section strip is a TabStrip and the layer switch a ListBox, over a Panel whose
            // children toggle IsVisible: neither is a content host, so neither could carry a page
            // transition. Carousel is the only Avalonia container that owns one, so the guard is
            // that neither it nor the property it exposes appears anywhere.
            var offenders = AuthoredXaml.Files()
                .Select(file => (file.Key, Markup: AuthoredXaml.WithoutComments(file.Value)))
                .Where(file => file.Markup.Contains("PageTransition", StringComparison.Ordinal)
                    || file.Markup.Contains("<Carousel", StringComparison.Ordinal))
                .Select(file => file.Key)
                .ToArray();

            Assert.True(offenders.Length == 0, $"Page transitions appear in: {string.Join(", ", offenders)}.");
        }

        [AvaloniaFact]
        public void TheLightingPreviewsFrameInterval_HasNoTransitionsOfEitherFlavour()
        {
            // It is a repaint, not a property change, so there is nothing for a Transitions to
            // interpolate and no MotionResourceBinder alias to point — the same shape as the
            // spinner's rotation and the recording pulse. A `...Full`/`...Reduced` pair appearing
            // here would mean somebody had turned the preview into a transition, which is not what
            // a per-frame board repaint is.
            //
            // The freeze under reduce-motion is therefore NOT expressed as an empty reduced set:
            // the tab reads IMotionSettings.ReduceMotion and does not start its timer at all. That
            // half is the view model's and is asserted there.
            foreach (var suffix in new[] { FullSuffix, ReducedSuffix })
            {
                Assert.False(
                    DesignTokens.TryResolve("LightingPreviewTransitions" + suffix, ThemeVariant.Dark, out _),
                    $"The lighting preview grew a {suffix} transition set; it is a frame interval, not a transition.");
            }

            Assert.False(DesignTokens.TryResolve("LightingPreviewTransitions", ThemeVariant.Dark, out _));
        }

        [AvaloniaFact]
        public void EveryMotion_ShipsInBothAFullAndAReducedFlavour()
        {
            var motions = MotionKeys()
                .Where(key => key.EndsWith(FullSuffix, StringComparison.Ordinal))
                .Select(key => key[..^FullSuffix.Length])
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.NotEmpty(motions);

            foreach (var motion in motions)
            {
                Assert.True(
                    DesignTokens.TryResolve(motion + ReducedSuffix, ThemeVariant.Dark, out _),
                    $"{motion} has a full set but no reduced one.");
            }
        }

        [AvaloniaFact]
        public void TheReducedBudget_ContainsNoRiseSlideOrHeightAnimation()
        {
            var movements = new List<string>();

            foreach (var key in MotionKeys().Where(key => key.EndsWith(ReducedSuffix, StringComparison.Ordinal)))
            {
                var transitions = (Transitions)DesignTokens.Resolve(key, ThemeVariant.Dark);

                foreach (var transition in transitions)
                {
                    var animated = AnimatedPropertyOf(transition);

                    if (!_fadeProperties.Contains(animated))
                    {
                        movements.Add($"{key} animates {animated.Name}");
                    }
                }
            }

            Assert.True(movements.Count == 0, string.Join("; ", movements));
        }

        [AvaloniaFact]
        public void TheFullBudget_RisesAndChangesHeightWhereTheDesignSaysSo()
        {
            // The mirror of the test above: the reduced set is only meaningful if the full one
            // actually carries the movement it drops.
            var listInsert = (Transitions)DesignTokens.Resolve("ListInsertTransitionsFull", ThemeVariant.Dark);
            var popover = (Transitions)DesignTokens.Resolve("PopoverTransitionsFull", ThemeVariant.Dark);

            Assert.Contains(listInsert, transition => AnimatedPropertyOf(transition) == Layoutable.HeightProperty);
            Assert.Contains(popover, transition => AnimatedPropertyOf(transition) == Visual.RenderTransformProperty);
        }

        [AvaloniaTheory]
        [InlineData("StatusFillTransitionsFull", 220)]
        [InlineData("ListInsertTransitionsFull", 160)]
        [InlineData("PopoverTransitionsFull", 120)]
        [InlineData("ModalTransitionsFull", 140)]
        [InlineData("ScrimTransitionsFull", 140)]
        [InlineData("ToastTransitionsFull", 180)]
        [InlineData("StatusFillTransitionsReduced", 220)]
        [InlineData("PopoverTransitionsReduced", 120)]
        [InlineData("ModalTransitionsReduced", 140)]
        [InlineData("ScrimTransitionsReduced", 140)]
        [InlineData("ToastTransitionsReduced", 180)]
        public void EveryTransitionOfASet_RunsForTheBudgetedDuration(string key, double milliseconds)
        {
            var transitions = (Transitions)DesignTokens.Resolve(key, ThemeVariant.Dark);

            Assert.NotEmpty(transitions);
            Assert.All(transitions, transition => Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), DurationOf(transition)));
        }

        [AvaloniaFact]
        public void TheReducedListInsert_IsEmptyRatherThanMissing()
        {
            // Assigning an empty Transitions is how a control ends up with no animation while the
            // alias key stays resolvable; dropping the resource would leave the setter unset.
            var transitions = (Transitions)DesignTokens.Resolve("ListInsertTransitionsReduced", ThemeVariant.Dark);

            Assert.Empty(transitions);
        }

        [AvaloniaFact]
        public void MotionResourceBinder_WhenTheFlagFlips_RePointsEveryAlias()
        {
            var application = Application.Current!;
            var aliases = MotionKeys()
                .Where(key => key.EndsWith(FullSuffix, StringComparison.Ordinal))
                .Select(key => key[..^FullSuffix.Length])
                .ToArray();

            var restore = aliases.ToDictionary(
                alias => alias,
                alias => application.Resources.TryGetValue(alias, out var value) ? value : null);

            try
            {
                MotionResourceBinder.Apply(application, CreateMotionSettings(reduceMotion: false));

                foreach (var alias in aliases)
                {
                    Assert.Same(
                        DesignTokens.Resolve(alias + FullSuffix, ThemeVariant.Dark),
                        DesignTokens.Resolve(alias, ThemeVariant.Dark));
                }

                MotionResourceBinder.Apply(application, CreateMotionSettings(reduceMotion: true));

                foreach (var alias in aliases)
                {
                    Assert.Same(
                        DesignTokens.Resolve(alias + ReducedSuffix, ThemeVariant.Dark),
                        DesignTokens.Resolve(alias, ThemeVariant.Dark));
                }
            }
            finally
            {
                foreach (var (alias, value) in restore)
                {
                    application.Resources[alias] = value;
                }
            }
        }

        [AvaloniaFact]
        public void MotionResourceBinder_AtStartup_HasAlreadyBoundEveryAlias()
        {
            // App.OnFrameworkInitializationCompleted runs it before any window exists, which is
            // what makes the bare aliases resolvable at all. The headless session boots the real
            // App, so this is the app's own startup being asserted, not the test's setup.
            foreach (var alias in MotionKeys()
                .Where(key => key.EndsWith(FullSuffix, StringComparison.Ordinal))
                .Select(key => key[..^FullSuffix.Length]))
            {
                Assert.True(
                    DesignTokens.TryResolve(alias, ThemeVariant.Dark, out var value) && value is Transitions,
                    $"'{alias}' is not bound to a Transitions.");
            }
        }

        private static IMotionSettings CreateMotionSettings(bool reduceMotion)
        {
            return new MotionSettings(new FakeReduceMotionDetector(reduceMotion));
        }

        /// <summary>Every key <c>Themes/Motion.axaml</c> declares.</summary>
        private static IReadOnlyList<string> MotionKeys()
        {
            var motion = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(MotionSource));

            return motion.Keys.Select(key => key.ToString() ?? string.Empty).ToArray();
        }

        /// <inheritdoc cref="DurationPropertyName" />
        private static TimeSpan DurationOf(ITransition transition)
        {
            return (TimeSpan)ReadProperty(transition, DurationPropertyName);
        }

        /// <inheritdoc cref="DurationPropertyName" />
        private static AvaloniaProperty AnimatedPropertyOf(ITransition transition)
        {
            return (AvaloniaProperty)ReadProperty(transition, AnimatedPropertyName);
        }

        private static object ReadProperty(ITransition transition, string name)
        {
            var property = transition.GetType().GetProperty(name)
                ?? throw new InvalidOperationException($"{transition.GetType().Name} has no {name}.");

            return property.GetValue(transition)
                ?? throw new InvalidOperationException($"{transition.GetType().Name}.{name} is null.");
        }

        /// <summary>
        /// A control that switches a layer or a tab: the container generated for one of them, or the
        /// <see cref="ItemsControl"/> holding the strip — which is now the segmented control's
        /// trough and the section strip, both of which are <see cref="ItemsControl"/>s themselves.
        /// </summary>
        private static bool IsLayerOrTabContainer(Control control)
        {
            if (control.DataContext is KeyboardLayerViewModel or EditorTabViewModel)
            {
                return true;
            }

            return control is ItemsControl items
                && items.ItemsSource is IEnumerable<object> source
                && source.Any(item => item is KeyboardLayerViewModel or EditorTabViewModel);
        }

        private static string Describe(Control control)
        {
            return control.DataContext switch
            {
                KeyboardLayerViewModel layer => $"{control.GetType().Name} (layer '{layer.Caption}')",
                EditorTabViewModel tab => $"{control.GetType().Name} (tab '{tab.Caption}')",
                _ => control.GetType().Name
            };
        }
    }
}
