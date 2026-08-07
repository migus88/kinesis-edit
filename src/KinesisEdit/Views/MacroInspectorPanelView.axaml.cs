using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The key inspector's Macro panel, resolved from <see cref="MacroInspectorPanelViewModel"/> by
    /// <see cref="ViewLocator"/>. Everything it shows is bound; the only code here is the <b>drag</b>
    /// half of mockup <c>2i</c>'s "drag ⠿ · ⌥↑↓". The keyboard half is the editor's own grammar
    /// (<c>Input/EditorShortcuts</c> → <c>MoveMacroStepUp/DownCommand</c>) and needs nothing from
    /// this file.
    /// <para>
    /// <b>It is a press-and-release gesture, not <c>DragDrop.DoDragDrop</c>.</b> The platform drag
    /// source is a shell service — it does not exist on the headless platform the UI suite runs on,
    /// and its API is obsolete in this Avalonia version — while a reorder inside one list needs
    /// nothing from the OS. Press on a row, release on another: the two 1-based positions are handed
    /// to <see cref="MacroInspectorStepsViewModel.MoveStep"/>, which is the same one method
    /// <c>⌥↑↓</c> runs a step at a time. Every reorder rule (a step moves with the delay folded
    /// behind it) is the view model's and is covered without a pointer.
    /// </para>
    /// </summary>
    public partial class MacroInspectorPanelView : UserControl
    {
        /// <summary>Creates the panel view.</summary>
        public MacroInspectorPanelView()
        {
            InitializeComponent();

            // Bubbling, and on this control rather than on each row: the release lands on whatever
            // is under the pointer, which is the row being dropped onto and not the one the drag
            // started from.
            AddHandler(PointerReleasedEvent, OnPointerReleasedOverStep, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        /// <summary>1-based position of the row the pointer went down on, or 0 while none is held.</summary>
        private int _dragFromPosition;

        /// <summary>
        /// Arms the drag. Left button only, and only on a real step row — the grip is what says the
        /// row can be dragged, but the whole row is the target, because a 12 px mark is not one.
        /// </summary>
        private void OnStepPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _dragFromPosition = 0;

            if (sender is not Control row
                || row.DataContext is not MacroInspectorStepViewModel step
                || !e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _dragFromPosition = step.Position;
        }

        /// <summary>
        /// Lands the drag on the row under the pointer. A release on the row it started from is not
        /// a reorder — it is the ordinary click that selects the step — and
        /// <see cref="MacroInspectorStepsViewModel.MoveStep"/> already answers false for it.
        /// </summary>
        private void OnPointerReleasedOverStep(object? sender, PointerReleasedEventArgs e)
        {
            var fromPosition = _dragFromPosition;

            _dragFromPosition = 0;

            if (fromPosition == 0
                || DataContext is not MacroInspectorPanelViewModel panel
                || FindStep(e.Source as Control) is not { } target)
            {
                return;
            }

            panel.Steps.MoveStep(fromPosition - 1, target.Position - 1);
        }

        /// <summary>
        /// The step row a hit landed in, found by walking up from whatever was hit — the grip, a
        /// caption or the row itself. It reads the <c>DataContext</c> rather than a template part,
        /// which is what keeps this out of somebody else's template.
        /// </summary>
        private static MacroInspectorStepViewModel? FindStep(Control? hit)
        {
            for (var control = hit; control is not null; control = control.Parent as Control)
            {
                if (control.DataContext is MacroInspectorStepViewModel step)
                {
                    return step;
                }
            }

            return null;
        }
    }
}
