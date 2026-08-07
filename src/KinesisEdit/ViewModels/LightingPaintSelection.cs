using System.Globalization;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The lighting board's selection of keys — the set a colour, or a Clear, applies to (design
    /// mockup 2f: "Paint · 2 keys selected", with "Select all" and "Clear").
    /// <para>
    /// <b>It is not the editor's selection.</b> <c>KeyboardEditorViewModel.SelectedKey</c> and
    /// <see cref="KeyboardKeyViewModel.IsSelected"/> are the Keys tab's <i>single</i> selection,
    /// the one the key inspector rail follows; this is a multi-selection on a board that is about
    /// colour and has no inspector. Both boards render the very same cap view models, so the two
    /// need two flags — see <see cref="KeyboardKeyViewModel.IsLightingSelected"/>.
    /// </para>
    /// <para>
    /// It owns no lighting rule: it holds caps and answers "which ones", and every write into the
    /// lighting model is <see cref="LightingTabViewModel"/>'s.
    /// </para>
    /// </summary>
    public sealed class LightingPaintSelection : ViewModelBase
    {
        /// <summary>What the line under the board is called (mockup 2f).</summary>
        public const string CaptionPrefix = "Paint · ";

        /// <summary>The caption's tail when exactly one key is selected.</summary>
        public const string SingularCaptionSuffix = "1 key selected";

        /// <summary>The caption's tail when nothing is selected.</summary>
        public const string EmptyCaptionSuffix = "no keys selected";

        /// <summary>Caption of the button that selects every key of the layer (mockup 2f).</summary>
        public const string SelectAllCaption = "Select all";

        /// <summary>Caption of the button that turns the selected keys off (mockup 2f).</summary>
        public const string ClearCaption = "Clear";

        /// <summary>The selected caps, in the order they joined the selection.</summary>
        public IReadOnlyList<KeyboardKeyViewModel> Keys => _selected;

        /// <summary>How many keys are selected.</summary>
        public int Count => _selected.Count;

        /// <summary>Whether anything is selected at all.</summary>
        public bool HasSelection => _selected.Count > 0;

        /// <summary>
        /// The line under the board — <c>Paint · 2 keys selected</c>, and its singular
        /// (<c>1 key</c>) and empty (<c>no keys</c>) forms.
        /// </summary>
        public string Caption
        {
            get
            {
                var count = _selected.Count;

                return count switch
                {
                    0 => CaptionPrefix + EmptyCaptionSuffix,
                    1 => CaptionPrefix + SingularCaptionSuffix,
                    _ => string.Create(CultureInfo.InvariantCulture, $"{CaptionPrefix}{count} keys selected")
                };
            }
        }

        private readonly List<KeyboardKeyViewModel> _selected = [];
        private IReadOnlyList<KeyboardKeyViewModel> _layerKeys = [];
        private KeyboardKeyViewModel? _anchor;

        /// <summary>
        /// Points the selection at another layer's caps and empties it. A selection is a set of
        /// positions on one layer; carrying it across would paint keys the user never clicked
        /// (specs/07-lighting.md §4: the two layers are fully independent).
        /// </summary>
        public void SetLayer(IReadOnlyList<KeyboardKeyViewModel>? keys)
        {
            Deselect();

            _layerKeys = keys ?? [];
            _anchor = null;

            Notify();
        }

        /// <summary>
        /// Adds <paramref name="key"/> to the selection or takes it out again — what a plain click
        /// on the board does. The clicked key becomes the anchor <see cref="Extend"/> reaches from,
        /// whichever way the toggle went.
        /// </summary>
        public void Toggle(KeyboardKeyViewModel? key)
        {
            if (key is null)
            {
                return;
            }

            if (!_selected.Remove(key))
            {
                _selected.Add(key);
                key.IsLightingSelected = true;
            }
            else
            {
                key.IsLightingSelected = false;
            }

            _anchor = key;

            Notify();
        }

        /// <summary>
        /// Extends the selection to <paramref name="key"/> — what a shift-click does.
        /// <para>
        /// <b>"Extends" is over the layer's key order</b>, the order the layout file lists the
        /// positions in (specs/05-key-model.md §7.4) and the order the caps are built in: every key
        /// between the anchor and this one, inclusive, joins the selection. That is a run of
        /// positions and not a rectangle on the board — a rectangle would need a geometry rule of
        /// its own, and the key order is what both halves of a split board already share. With no
        /// anchor yet — the first click of a session, or the first after a layer change — it
        /// behaves as a plain <see cref="Toggle"/>.
        /// </para>
        /// <para>
        /// It only ever adds: shift-clicking twice never empties the selection, which is what makes
        /// a mis-aimed extend recoverable with one more click rather than a restart.
        /// </para>
        /// </summary>
        public void Extend(KeyboardKeyViewModel? key)
        {
            if (key is null)
            {
                return;
            }

            var anchorIndex = _anchor is null ? -1 : IndexOf(_anchor);
            var targetIndex = IndexOf(key);

            if (anchorIndex < 0 || targetIndex < 0)
            {
                Toggle(key);

                return;
            }

            var first = Math.Min(anchorIndex, targetIndex);
            var last = Math.Max(anchorIndex, targetIndex);

            for (var index = first; index <= last; index++)
            {
                Add(_layerKeys[index]);
            }

            _anchor = key;

            Notify();
        }

        /// <summary>Selects every key of the layer (mockup 2f's "Select all").</summary>
        public void SelectAll()
        {
            foreach (var key in _layerKeys)
            {
                Add(key);
            }

            Notify();
        }

        /// <summary>
        /// Empties the selection. It is <b>not</b> mockup 2f's "Clear" button, which erases the
        /// selected keys' colours (<see cref="LightingTabViewModel.ClearKeyColorsCommand"/>) and
        /// leaves them selected.
        /// </summary>
        public void Clear()
        {
            Deselect();

            _anchor = null;

            Notify();
        }

        private int IndexOf(KeyboardKeyViewModel key)
        {
            for (var index = 0; index < _layerKeys.Count; index++)
            {
                if (ReferenceEquals(_layerKeys[index], key))
                {
                    return index;
                }
            }

            return -1;
        }

        private void Add(KeyboardKeyViewModel key)
        {
            if (!_selected.Contains(key))
            {
                _selected.Add(key);
            }

            key.IsLightingSelected = true;
        }

        private void Deselect()
        {
            foreach (var key in _selected)
            {
                key.IsLightingSelected = false;
            }

            _selected.Clear();
        }

        private void Notify()
        {
            OnPropertyChanged(nameof(Keys));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(Caption));
        }
    }
}
