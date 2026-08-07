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

        /// <summary>
        /// Raised whenever the selected set moved, <b>from any source</b> — a click on a cap, a
        /// shift-click, "Select all", a zone button, an emptying, a layer change.
        /// <para>
        /// It exists because things outside this object have to follow the selection rather than
        /// merely be told to change it (issue #124): a zone button lights up when every one of its
        /// keys happens to be selected, however they got selected, and the Apply control is enabled
        /// exactly while something is selected. Both used to be unanswerable without reaching into
        /// this class from <see cref="LightingTabViewModel"/> at every call site that touches the
        /// selection — a list somebody would have to keep complete.
        /// </para>
        /// <para>
        /// <c>PropertyChanged</c> would have carried it too, but this is not a property moving: it
        /// is one notification for a set, raised once per gesture even when the gesture touched
        /// ninety-five caps.
        /// </para>
        /// </summary>
        public event EventHandler? Changed;

        private readonly List<KeyboardKeyViewModel> _selected = [];
        private readonly Dictionary<int, KeyboardKeyViewModel> _byKeyCode = [];
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

            // The index the key-code API answers from. Built here rather than searched per call
            // because a zone is up to ninety-five codes and the zone buttons re-ask on every
            // selection change; it is the layer's own caps, so it is exactly as fresh as _layerKeys.
            _byKeyCode.Clear();

            foreach (var key in _layerKeys)
            {
                _byKeyCode.TryAdd(key.Key.OriginalKey.Code, key);
            }

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
        /// Adds every cap of this layer whose <b>memory key code</b> is in
        /// <paramref name="keyCodes"/> — what a zone button does (issue #124).
        /// <para>
        /// The codes arrive already resolved for the shown layer:
        /// <see cref="LightingTabViewModel"/> re-resolves a zone's top-layer codes through the
        /// layout before calling in (specs/07-lighting.md §2.4 item 6), because that resolution
        /// needs the layout layers and this object holds only caps.
        /// </para>
        /// </summary>
        public void SelectByKeyCode(IEnumerable<int> keyCodes)
        {
            ArgumentNullException.ThrowIfNull(keyCodes);

            foreach (var keyCode in keyCodes)
            {
                if (_byKeyCode.TryGetValue(keyCode, out var key))
                {
                    Add(key);
                }
            }

            Notify();
        }

        /// <summary>
        /// Takes those same caps back out — the other half of a zone button's toggle. The anchor is
        /// left alone: a zone is not a place on the board, so it is not somewhere a shift-click
        /// should extend from.
        /// </summary>
        public void DeselectByKeyCode(IEnumerable<int> keyCodes)
        {
            ArgumentNullException.ThrowIfNull(keyCodes);

            foreach (var keyCode in keyCodes)
            {
                if (_byKeyCode.TryGetValue(keyCode, out var key) && _selected.Remove(key))
                {
                    key.IsLightingSelected = false;
                }
            }

            Notify();
        }

        /// <summary>
        /// Whether every cap this layer has for <paramref name="keyCodes"/> is currently selected —
        /// which is what makes a zone button show its selected state, and what decides which way its
        /// next click toggles.
        /// <para>
        /// <b>A set with no cap on this layer answers false</b>, not the vacuous true. A zone whose
        /// codes all resolved to nothing has nothing selected, and lighting its button would tell the
        /// user the opposite.
        /// </para>
        /// </summary>
        public bool ContainsAll(IEnumerable<int> keyCodes)
        {
            ArgumentNullException.ThrowIfNull(keyCodes);

            var found = false;

            foreach (var keyCode in keyCodes)
            {
                if (!_byKeyCode.TryGetValue(keyCode, out var key))
                {
                    continue;
                }

                if (!_selected.Contains(key))
                {
                    return false;
                }

                found = true;
            }

            return found;
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

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
