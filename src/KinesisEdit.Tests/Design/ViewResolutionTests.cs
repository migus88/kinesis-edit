using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// <see cref="ViewLocator"/> pairs a view model with its view by <b>name</b>, and a name that
    /// no longer matches degrades to a diagnostic <see cref="TextBlock"/> rather than to a build
    /// error. This suite is what notices: every concrete view model either resolves to a view type
    /// that genuinely loads, or is listed below with the reason it is not resolved by name.
    /// <para>
    /// The rename hazard is wider than the suffix, because the locator replaces <i>every</i>
    /// occurrence of "ViewModel" — which is how <c>KinesisEdit.ViewModels.XxxViewModel</c> becomes
    /// <c>KinesisEdit.Views.XxxView</c>. A view model moved to another namespace, or a view moved
    /// out of <c>Views</c>, stops resolving even though neither type was renamed.
    /// </para>
    /// </summary>
    public class ViewResolutionTests
    {
        /// <summary>
        /// The view models that are deliberately not resolved by name, each with the reason. They
        /// fall into three groups: the shell's own window, which the composition root builds
        /// directly; the row and leaf view models rendered through an explicit
        /// <c>DataTemplate</c> in their parent view; and the few that are bound into a parent's
        /// markup by property rather than rendered as content at all.
        /// </summary>
        private static readonly Dictionary<Type, string> _excluded = new()
        {
            // Built by the composition root, which passes it to `new MainWindow(...)`.
            [typeof(MainWindowViewModel)] = "App.axaml.cs constructs MainWindow directly.",

            // A Window, not a View: the message box is shown modally by MessageBoxPresenter.
            [typeof(MessageBoxViewModel)] = "MessageBoxPresenter constructs MessageBoxWindow directly.",

            // Rendered by an explicit DataTemplate in the view named after their owner.
            [typeof(KeyboardKeyViewModel)] = "DataTemplate in Controls/KeyboardView.axaml (-> KeyCapView).",
            [typeof(KeyboardLayerViewModel)] = "DataTemplate in Views/KeyboardEditorView.axaml (the layer pills).",
            [typeof(EditorTabViewModel)] = "DataTemplate in Views/KeyboardEditorView.axaml (the section tabs).",
            [typeof(MacroPanelViewModel)] = "DataTemplate in Views/KeyboardEditorView.axaml (the macro panel).",
            [typeof(MacroSlotViewModel)] = "DataTemplate in Views/KeyboardEditorView.axaml (the slot pills).",
            [typeof(MacroStepViewModel)] = "DataTemplate in Views/KeyboardEditorView.axaml (the recorded steps).",
            [typeof(MacroCoTriggerViewModel)] = "DataTemplate in Views/KeyboardEditorView.axaml (the co-trigger pills).",
            [typeof(MacroListEntryViewModel)] = "DataTemplate in Views/KeyboardEditorView.axaml (the profile's macros).",
            [typeof(LightingLayerViewModel)] = "DataTemplate in Views/LightingTabView.axaml (the layer pills).",
            [typeof(LightingModeViewModel)] = "DataTemplate in Views/LightingTabView.axaml (the mode menu).",
            [typeof(LightingDirectionViewModel)] = "DataTemplate in Views/LightingTabView.axaml (the direction pills).",
            [typeof(SettingsSliderRowViewModel)] = "DataTemplate in Views/KeyboardSettingsView.axaml.",
            [typeof(SettingsToggleRowViewModel)] = "DataTemplate in Views/KeyboardSettingsView.axaml.",
            [typeof(SettingsChoiceRowViewModel)] = "DataTemplate in Views/KeyboardSettingsView.axaml.",
            [typeof(CustomColorSlotViewModel)] = "DataTemplate in Controls/ColorPickerView.axaml.",
            [typeof(PedalInputRowViewModel)] = "DataTemplate in Views/SavantElitePedalView.axaml.",
            [typeof(PedalInvalidLineViewModel)] = "DataTemplate in Views/SavantElitePedalView.axaml.",
            [typeof(PedalSpecialActionViewModel)] = "ControlTheme in Views/SavantElitePedalView.axaml (a MenuItem).",
            [typeof(PedalSpecialActionGroupViewModel)] = "ControlTheme in Views/SavantElitePedalView.axaml (a MenuItem).",

            // Bound into their parent's markup by property; never hosted as content.
            [typeof(ColorPickerViewModel)] = "Views/LightingTabView.axaml hosts Controls/ColorPickerView explicitly.",
            [typeof(LightingColorSlotViewModel)] = "Bound as EffectColor / BaseColor inside Views/LightingTabView.axaml.",
            [typeof(MacroBudgetViewModel)] = "Bound as Budget.* inside the macro panel's template.",
            [typeof(MacroStepListViewModel)] = "Bound as Steps.Items inside the macro panel's template."
        };

        /// <summary>Every concrete view model the app declares.</summary>
        public static TheoryData<string> ViewModelNames()
        {
            var names = new TheoryData<string>();

            foreach (var type in ConcreteViewModels())
            {
                names.Add(type.FullName!);
            }

            return names;
        }

        [AvaloniaTheory]
        [MemberData(nameof(ViewModelNames))]
        public void ViewModel_EitherResolvesToAViewThatLoads_OrIsExcluded(string viewModelName)
        {
            var viewModelType = typeof(ViewModelBase).Assembly.GetType(viewModelName, throwOnError: true)!;

            if (_excluded.ContainsKey(viewModelType))
            {
                return;
            }

            var viewType = ViewLocator.ResolveViewType(viewModelType);

            Assert.True(
                viewType is not null,
                $"{viewModelName} resolves to no view. Rename the view to match, or add it to the "
                + "exclusion list with the reason it is rendered another way.");
            Assert.True(
                typeof(Control).IsAssignableFrom(viewType),
                $"{viewModelName} resolves to {viewType!.FullName}, which is not a Control.");

            // Constructing it runs InitializeComponent, which is where a broken StaticResource, a
            // missing DataTemplate type or an unparseable geometry actually throws.
            var view = Assert.IsAssignableFrom<Control>(Activator.CreateInstance(viewType!));

            Assert.IsType(viewType!, view);
        }

        [AvaloniaTheory]
        [MemberData(nameof(ViewModelNames))]
        public void ViewModel_NotExcluded_DoesNotFallBackToTheDiagnosticPlaceholder(string viewModelName)
        {
            var viewModelType = typeof(ViewModelBase).Assembly.GetType(viewModelName, throwOnError: true)!;

            if (_excluded.ContainsKey(viewModelType))
            {
                return;
            }

            // The locator's own fallback is a TextBlock reading "View not found for ...". Nothing
            // in the app may reach it, and only this assertion would ever notice if it did.
            var built = new ViewLocator().Build(CreateUninitialized(viewModelType));

            Assert.False(
                built is TextBlock textBlock && textBlock.Text?.StartsWith("View not found", StringComparison.Ordinal) == true,
                $"{viewModelName} falls back to the diagnostic placeholder.");
        }

        [AvaloniaFact]
        public void TheExclusionList_HasNoStaleEntries()
        {
            var concrete = ConcreteViewModels().ToHashSet();
            var stale = _excluded.Keys
                .Where(type => !concrete.Contains(type))
                .Select(type => type.FullName)
                .Order(StringComparer.Ordinal);

            Assert.True(
                !stale.Any(),
                $"These are no longer concrete view models and should leave the exclusion list: {string.Join(", ", stale)}.");
        }

        [AvaloniaFact]
        public void EveryExclusion_StatesWhy()
        {
            Assert.All(_excluded, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
        }

        [AvaloniaFact]
        public void TheShellsOwnViewModel_IsExcludedBecauseItResolvesToNothing()
        {
            // The reason the list gives has to stay true: if a MainWindowView ever appears, the
            // exclusion is wrong rather than merely redundant.
            Assert.Null(ViewLocator.ResolveViewType(typeof(MainWindowViewModel)));
        }

        private static IEnumerable<Type> ConcreteViewModels()
        {
            return typeof(ViewModelBase).Assembly
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(ViewModelBase).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);
        }

        /// <summary>
        /// An instance of <paramref name="viewModelType"/> without running its constructor: the
        /// locator only reads <c>GetType()</c>, and most of these view models need collaborators
        /// this test has no interest in building.
        /// </summary>
        private static object CreateUninitialized(Type viewModelType)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(viewModelType);
        }
    }
}
