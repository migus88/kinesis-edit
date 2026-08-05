using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The keystroke list of the macro under edit and the four ways it changes: appending a
    /// recorded keystroke, dropping the last one, dropping an arbitrary one, and emptying it
    /// (specs/06-macros.md §1). Kept apart from <see cref="MacroPanelViewModel"/> so the list's
    /// rules are one small, separately testable thing.
    /// <para>
    /// <b>Backspace here is a command, never a captured key.</b> specs/06-macros.md §2.2 makes
    /// every physical key recordable macro content, so the Backspace key types a backspace into
    /// the macro; removing the previous step is the panel's <c>Backspace</c> button, which is
    /// this class's <see cref="RemoveLast"/>.
    /// </para>
    /// </summary>
    public sealed class MacroStepListViewModel : ViewModelBase
    {
        /// <summary>The macro whose keystrokes are shown, or null when nothing is being edited.</summary>
        public Macro? Macro
        {
            get => _macro;
            private set => SetProperty(ref _macro, value);
        }

        /// <summary>The macro's keystrokes in playback order.</summary>
        public IReadOnlyList<MacroStepViewModel> Items
        {
            get => _steps;
            private set
            {
                if (SetProperty(ref _steps, value))
                {
                    OnPropertyChanged(nameof(HasItems));
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        /// <summary>Whether the macro under edit has any keystrokes.</summary>
        public bool HasItems => _steps.Count > 0;

        /// <summary>Number of keystrokes in the macro under edit.</summary>
        public int Count => _steps.Count;

        private readonly TokenDialect _dialect;
        private IReadOnlyList<MacroStepViewModel> _steps = [];
        private Macro? _macro;

        /// <summary>Creates an empty list rendering its keys in <paramref name="dialect"/>.</summary>
        public MacroStepListViewModel(TokenDialect dialect)
        {
            _dialect = dialect;
        }

        /// <summary>Shows <paramref name="macro"/>'s keystrokes; null empties the list.</summary>
        public void Load(Macro? macro)
        {
            Macro = macro;

            Rebuild();
        }

        /// <summary>Appends a keystroke; false when no macro is being edited.</summary>
        public bool Append(Keystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);

            if (_macro is null)
            {
                return false;
            }

            _macro.AddKeystroke(keystroke);

            Rebuild();

            return true;
        }

        /// <summary>Drops the last keystroke — the panel's Backspace button; false when there is none.</summary>
        public bool RemoveLast()
        {
            return RemoveAt(_steps.Count - 1);
        }

        /// <summary>Drops the keystroke at <paramref name="index"/>; false when it is out of range.</summary>
        public bool RemoveAt(int index)
        {
            if (_macro is null || index < 0 || index >= _macro.Keystrokes.Count)
            {
                return false;
            }

            _macro.RemoveKeystrokeAt(index);

            Rebuild();

            return true;
        }

        /// <summary>Empties the macro, keeping its trigger metadata; false when it is already empty.</summary>
        public bool Clear()
        {
            if (_macro is null || _macro.Keystrokes.Count == 0)
            {
                return false;
            }

            _macro.ClearKeystrokes();

            Rebuild();

            return true;
        }

        /// <summary>Re-reads the macro after something outside this list wrote to it.</summary>
        public void RefreshFromModel()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (_macro is null || _macro.Keystrokes.Count == 0)
            {
                Items = [];

                return;
            }

            var steps = new List<MacroStepViewModel>(_macro.Keystrokes.Count);

            for (var index = 0; index < _macro.Keystrokes.Count; index++)
            {
                steps.Add(new MacroStepViewModel(_macro.Keystrokes[index], index + 1, _dialect));
            }

            Items = steps;
        }
    }
}
