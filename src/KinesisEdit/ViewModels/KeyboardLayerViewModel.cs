using System.Globalization;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One layer of the open profile as the keyboard picture renders it: the layer's caption and
    /// its key caps, built by joining <see cref="KeyboardLayer.Keys"/> to a
    /// <see cref="KeyboardVisual"/> <b>by key index</b> (specs/05-key-model.md §7.4 — the same
    /// position keeps its index across every layer, which is why one visual serves them all).
    /// <para>
    /// A key with no authored placement is skipped rather than throwing, and a placement with no
    /// model key simply produces no cap: the Core tests already assert the two index sets match
    /// exactly for every authored device, so a mismatch is a data bug that must degrade into a
    /// missing cap, never into a crashed editor.
    /// </para>
    /// </summary>
    public sealed class KeyboardLayerViewModel : ViewModelBase
    {
        /// <summary>How the shortcut is spelled off macOS — ⌥ is Alt on every other platform.</summary>
        public const string ShortcutPrefix = "Alt+";

        /// <summary>
        /// The <b>text</b> half of the layer's shortcut legend: <c>1</c> on macOS, <c>Alt+1</c>
        /// elsewhere. The Option mark macOS puts in front of it is <b>not</b> here and is not a
        /// string at all — U+2325 is in neither embedded IBM Plex family, so the character would
        /// draw as tofu. The view pairs this text with the <c>IconOption</c> mark, shown when
        /// <see cref="ShowsOptionMark"/> is set (docs/app/design-system.md, "Icons and device art").
        /// <para>
        /// <paramref name="isMacOs"/> is a parameter rather than a read of
        /// <see cref="KeyCaption.IsMacOs"/> so both platforms are testable on one machine — the same
        /// shape <see cref="KeyCaption.For"/> uses. The Option key is Alt on every platform
        /// (docs/design/handoff.md § "Interactions": "map ⌘→Ctrl and ⌥→Alt on Windows/Linux"), so
        /// only the spelling changes, never which physical key it names.
        /// </para>
        /// </summary>
        public static string BuildShortcutHint(int layerIndex, bool isMacOs)
        {
            var number = (layerIndex + 1).ToString(CultureInfo.InvariantCulture);

            return isMacOs ? number : ShortcutPrefix + number;
        }

        /// <summary>
        /// Builds one view model per layer of <paramref name="layout"/> over the device's board
        /// picture, resolving each layer's colour overlays from <paramref name="lighting"/>.
        /// </summary>
        public static IReadOnlyList<KeyboardLayerViewModel> BuildAll(
            KeyboardLayout layout,
            KeyboardVisual visual,
            object? lighting)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(visual);

            var layers = new List<KeyboardLayerViewModel>(layout.Layers.Count);

            foreach (var layer in layout.Layers)
            {
                layers.Add(new KeyboardLayerViewModel(
                    layer,
                    visual,
                    layout.Dialect,
                    LayerCaptions.ForLayer(layer, layout.Dialect),
                    KeyColorOverlay.BuildPaint(layout.Device, lighting, layer)));
            }

            return layers;
        }

        /// <summary>The model layer this view model wraps.</summary>
        public KeyboardLayer Layer { get; }

        /// <summary>Layer identity: 0 = top/base, 1 = Fn/keypad, Advantage 360 uses 0..4.</summary>
        public int Index => Layer.Index;

        /// <summary>
        /// The text of the keyboard legend printed on the layer's segment — <c>1</c>… on macOS,
        /// where <see cref="ShowsOptionMark"/> puts a drawn ⌥ in front of it, and <c>Alt+1</c>…
        /// elsewhere (mockup 1f: "annotated with the shortcut ⌥1–5"). It is display text; the
        /// accelerator it promises is kept by the editor's keyboard grammar
        /// (<see cref="Input.EditorShortcuts"/>, docs/app/keyboard-editor.md), which maps ⌥ to
        /// <c>Alt</c> on every platform — so the two agree on which physical key this names, and
        /// only the spelling differs.
        /// </summary>
        public string ShortcutHint { get; }

        /// <summary>
        /// Whether the segment draws the Option mark before <see cref="ShortcutHint"/> — true on
        /// macOS only. It is a flag rather than a glyph in the string because the mark is geometry
        /// (<c>IconOption</c>): neither embedded IBM Plex family carries U+2325, so a view that
        /// printed the character would draw tofu on the one platform that uses it.
        /// </summary>
        public bool ShowsOptionMark { get; }

        /// <summary>What the layer switch calls this layer (see <see cref="LayerCaptions"/>).</summary>
        public string Caption { get; }

        /// <summary>The layer's key caps, in the model's key order.</summary>
        public IReadOnlyList<KeyboardKeyViewModel> Keys { get; }

        /// <summary>
        /// The board's drawn panels, ordered by <see cref="KeyboardSection.Index"/> — two of them on
        /// a split board. Every entry of <see cref="Keys"/> appears in exactly one section, as the
        /// <b>same instance</b>: the editor resolves a cap through the flat list and
        /// <see cref="ApplyLighting"/> writes through it, so a copy would leave the picture
        /// showing state nothing updates. A section the layer has no key for is still listed, so the
        /// panel roster is the board's and not the layout file's.
        /// </summary>
        public IReadOnlyList<KeyboardSectionViewModel> Sections { get; }

        /// <summary>Board width in key units — what the view scales by.</summary>
        public double BoardWidth { get; }

        /// <summary>Board height in key units — what the view scales by.</summary>
        public double BoardHeight { get; }

        /// <summary>Whether this is the layer the editor is showing.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// How many of <b>this layer's</b> keys carry a remap — the legend row's "Remapped N"
        /// (mockups 1e/2a). Recomputed by <see cref="RefreshCounts"/>, never by a key's own
        /// notification: the counts are read once per edit, not once per cap.
        /// </summary>
        public int RemappedCount
        {
            get => _remappedCount;
            private set => SetProperty(ref _remappedCount, value);
        }

        /// <summary>How many of this layer's keys carry at least one macro — "Macro N".</summary>
        public int MacroCount
        {
            get => _macroCount;
            private set => SetProperty(ref _macroCount, value);
        }

        /// <summary>How many of this layer's keys carry a tap-and-hold assignment — "Tap-and-hold N".</summary>
        public int TapAndHoldCount
        {
            get => _tapAndHoldCount;
            private set => SetProperty(ref _tapAndHoldCount, value);
        }

        /// <summary>
        /// How many of this layer's positions can never be remapped (§5.3) — "Locked N". It is a
        /// geometry fact and so never moves, but it is recomputed with the others rather than
        /// becoming the one count with a different rule.
        /// </summary>
        public int LockedCount
        {
            get => _lockedCount;
            private set => SetProperty(ref _lockedCount, value);
        }

        /// <summary>
        /// How many of this layer's keys carry an advisory — "Advisory N" (mockup 2a counts it
        /// beside the other four). Settable and <b>pushed in</b>, exactly like
        /// <see cref="KeyboardKeyViewModel.HasAdvisory"/> on the cap: the fact lives in
        /// <see cref="Advisories.EditorAdvisories"/>, outside <c>KeyboardKey</c>, so
        /// <see cref="RefreshCounts"/> has nothing to read it from.
        /// </summary>
        public int AdvisoryCount
        {
            get => _advisoryCount;
            set => SetProperty(ref _advisoryCount, value);
        }

        private bool _isSelected;
        private int _remappedCount;
        private int _macroCount;
        private int _tapAndHoldCount;
        private int _lockedCount;
        private int _advisoryCount;

        /// <summary>Joins one model layer to the device's board picture.</summary>
        public KeyboardLayerViewModel(
            KeyboardLayer layer,
            KeyboardVisual visual,
            TokenDialect dialect,
            string caption,
            IReadOnlyDictionary<int, LedColor>? paintColors = null)
        {
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));

            ArgumentNullException.ThrowIfNull(visual);
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Caption = caption;
            ShowsOptionMark = KeyCaption.IsMacOs;
            ShortcutHint = BuildShortcutHint(layer.Index, KeyCaption.IsMacOs);
            BoardWidth = visual.Width;
            BoardHeight = visual.Height;
            Keys = BuildKeys(layer, visual, dialect, paintColors);
            Sections = BuildSections(visual, Keys);

            RefreshCounts();
        }

        /// <summary>Returns the cap of the position with ordinal <paramref name="index"/>, or null.</summary>
        public KeyboardKeyViewModel? FindByIndex(int index)
        {
            foreach (var key in Keys)
            {
                if (key.Index == index)
                {
                    return key;
                }
            }

            return null;
        }

        /// <summary>Re-reads every cap of the layer — what a layer-wide or layout-wide reset ends with.</summary>
        public void RefreshFromModel()
        {
            foreach (var key in Keys)
            {
                key.RefreshFromModel();
            }

            RefreshCounts();
        }

        /// <summary>
        /// Recomputes the four model-derived counts of the legend row from the caps. Public because
        /// a single-key edit refreshes only that cap (<see cref="KeyboardKeyViewModel.RefreshFromModel"/>)
        /// and the layer's totals still have to follow; <see cref="AdvisoryCount"/> is not here
        /// because nothing on the model carries it.
        /// </summary>
        public void RefreshCounts()
        {
            var remapped = 0;
            var macros = 0;
            var tapAndHold = 0;
            var locked = 0;

            foreach (var key in Keys)
            {
                if (key.IsModified)
                {
                    remapped++;
                }

                if (key.IsMacro)
                {
                    macros++;
                }

                if (key.IsTapAndHold)
                {
                    tapAndHold++;
                }

                if (!key.CanEdit)
                {
                    locked++;
                }
            }

            RemappedCount = remapped;
            MacroCount = macros;
            TapAndHoldCount = tapAndHold;
            LockedCount = locked;
        }

        /// <summary>
        /// Re-draws the whole layer's lighting in one pass: <paramref name="frame"/> is the
        /// previewed effect at this instant and <paramref name="paintColors"/> the colours on file
        /// (<see cref="KeyColorOverlay.BuildPaint"/>), both keyed by <b>memory key code</b>.
        /// <para>
        /// <b>Whole-layer, never a merge.</b> A key neither map mentions goes back to unpainted and
        /// unlit — erasing a colour has to be as visible as assigning one — and the effect layer is
        /// re-pushed in full every frame anyway. A key absent from
        /// <see cref="Core.Lighting.Preview.LightingEffectFrame.Cells"/> is unlit rather than lit at
        /// intensity 0, which is what makes the cap draw its hatch: off is hatched, never black.
        /// </para>
        /// <para>
        /// It cannot come from <see cref="RefreshFromModel"/>: the colours live in the lighting
        /// model, which no layout parser ever writes into <see cref="KeyboardKey.KeyColor"/>. It is
        /// called ~30 times a second while the Lighting tab is open, so nothing here allocates and
        /// each cap raises a change notification only for a value that actually moved
        /// (<see cref="KeyboardKeyViewModel.ApplyEffect"/>).
        /// </para>
        /// <para>
        /// This says nothing about whether the colours are <b>drawn</b>. One layer view model is
        /// rendered by two pictures — the editor's Keys tab and the Lighting tab's board — so
        /// "lighting is on screen" is not a fact this object could hold; it belongs to the picture
        /// (<c>KeyboardView.ShowsLighting</c>).
        /// </para>
        /// </summary>
        public void ApplyLighting(LightingEffectFrame frame, IReadOnlyDictionary<int, LedColor>? paintColors)
        {
            ArgumentNullException.ThrowIfNull(frame);

            foreach (var key in Keys)
            {
                var keyCode = key.Key.OriginalKey.Code;

                key.ApplyPaint(
                    paintColors is not null && paintColors.TryGetValue(keyCode, out var paint) ? paint : null,
                    frame.PaintOpacity);

                if (frame.Cells.TryGetValue(keyCode, out var cell))
                {
                    key.ApplyEffect(cell.Color, cell.Intensity);
                }
                else
                {
                    key.ApplyEffect(null, 0.0);
                }
            }
        }

        private static IReadOnlyList<KeyboardKeyViewModel> BuildKeys(
            KeyboardLayer layer,
            KeyboardVisual visual,
            TokenDialect dialect,
            IReadOnlyDictionary<int, LedColor>? paintColors)
        {
            var keys = new List<KeyboardKeyViewModel>(layer.Keys.Count);

            foreach (var key in layer.Keys)
            {
                if (!visual.TryGetKey(key.Index, out var keyVisual))
                {
                    continue;
                }

                LedColor? paint = null;

                if (paintColors is not null && paintColors.TryGetValue(key.OriginalKey.Code, out var assignedColor))
                {
                    paint = assignedColor;
                }

                keys.Add(new KeyboardKeyViewModel(key, keyVisual, dialect, paint));
            }

            return keys;
        }

        /// <summary>
        /// Distributes the caps just built into the board's panels. It re-uses the instances rather
        /// than building new ones — the whole point of the section list is that it is a second
        /// <i>view</i> of <see cref="Keys"/>, not a second copy of it.
        /// </summary>
        private static IReadOnlyList<KeyboardSectionViewModel> BuildSections(
            KeyboardVisual visual,
            IReadOnlyList<KeyboardKeyViewModel> keys)
        {
            var members = new List<KeyboardKeyViewModel>[visual.Sections.Count];

            for (var index = 0; index < members.Length; index++)
            {
                members[index] = new List<KeyboardKeyViewModel>();
            }

            foreach (var key in keys)
            {
                members[key.Section].Add(key);
            }

            var sections = new KeyboardSectionViewModel[visual.Sections.Count];

            for (var index = 0; index < sections.Length; index++)
            {
                sections[index] = new KeyboardSectionViewModel(visual.Sections[index], members[index]);
            }

            return sections;
        }
    }
}
