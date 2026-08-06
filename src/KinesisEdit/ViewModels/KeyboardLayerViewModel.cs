using System.Globalization;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
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
        /// <summary>How the layer shortcut is spelled on macOS: the Option glyph.</summary>
        public const string MacShortcutPrefix = "⌥";

        /// <summary>How it is spelled everywhere else — ⌥ is Alt on every other platform.</summary>
        public const string ShortcutPrefix = "Alt+";

        /// <summary>
        /// The layer's shortcut legend. <paramref name="isMacOs"/> is a parameter rather than a
        /// read of <see cref="KeyCaption.IsMacOs"/> so both platforms are testable on one machine —
        /// the same shape <see cref="KeyCaption.For"/> uses. ⌥ is Alt on every platform
        /// (docs/design/handoff.md § "Interactions": "map ⌘→Ctrl and ⌥→Alt on Windows/Linux"), so
        /// only the spelling changes, never which physical key it names.
        /// </summary>
        public static string BuildShortcutHint(int layerIndex, bool isMacOs)
        {
            var number = (layerIndex + 1).ToString(CultureInfo.InvariantCulture);

            return isMacOs ? MacShortcutPrefix + number : ShortcutPrefix + number;
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
                    KeyColorOverlay.Build(layout.Device, lighting, layer)));
            }

            return layers;
        }

        /// <summary>The model layer this view model wraps.</summary>
        public KeyboardLayer Layer { get; }

        /// <summary>Layer identity: 0 = top/base, 1 = Fn/keypad, Advantage 360 uses 0..4.</summary>
        public int Index => Layer.Index;

        /// <summary>
        /// The keyboard legend printed on the layer's segment — <c>⌥1</c>… on macOS, <c>Alt+1</c>…
        /// elsewhere (mockup 1f: "annotated with the shortcut ⌥1–5"). It is display text; the
        /// accelerator it promises is kept by the editor's keyboard grammar
        /// (<see cref="Input.EditorShortcuts"/>, docs/app/keyboard-editor.md), which maps ⌥ to
        /// <c>Alt</c> on every platform — so the two agree on which physical key this names, and
        /// only the spelling differs.
        /// </summary>
        public string ShortcutHint { get; }

        /// <summary>What the layer switch calls this layer (see <see cref="LayerCaptions"/>).</summary>
        public string Caption { get; }

        /// <summary>The layer's key caps, in the model's key order.</summary>
        public IReadOnlyList<KeyboardKeyViewModel> Keys { get; }

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

        private bool _isSelected;

        /// <summary>Joins one model layer to the device's board picture.</summary>
        public KeyboardLayerViewModel(
            KeyboardLayer layer,
            KeyboardVisual visual,
            TokenDialect dialect,
            string caption,
            IReadOnlyDictionary<int, string>? colorOverlays = null)
        {
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));

            ArgumentNullException.ThrowIfNull(visual);
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Caption = caption;
            ShortcutHint = BuildShortcutHint(layer.Index, KeyCaption.IsMacOs);
            BoardWidth = visual.Width;
            BoardHeight = visual.Height;
            Keys = BuildKeys(layer, visual, dialect, colorOverlays);
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
        }

        /// <summary>
        /// Re-paints the layer's colours from a freshly built <see cref="KeyColorOverlay"/> map. A
        /// key the map does not mention goes back to unlit, so this is the whole-layer form and not
        /// a merge — erasing a colour has to be visible too.
        /// <para>
        /// The overlay cannot come from <see cref="RefreshFromModel"/>: it lives in the lighting
        /// model, which no layout parser ever writes into <see cref="KeyboardKey.KeyColor"/>.
        /// </para>
        /// <para>
        /// This says nothing about whether an LED row is <b>drawn</b>. One layer view model is
        /// rendered by two pictures — the editor's Keys tab and the Lighting tab's board — so
        /// "lighting is on screen" is not a fact this object could hold; it belongs to the picture
        /// (<c>KeyboardView.ShowsLedStrips</c>). Here a key is either lit or not.
        /// </para>
        /// </summary>
        public void ApplyColorOverlays(IReadOnlyDictionary<int, string>? colorOverlays)
        {
            foreach (var key in Keys)
            {
                key.ColorOverlayHex = colorOverlays is not null && colorOverlays.TryGetValue(key.Index, out var hex)
                    ? hex
                    : null;
            }
        }

        private static IReadOnlyList<KeyboardKeyViewModel> BuildKeys(
            KeyboardLayer layer,
            KeyboardVisual visual,
            TokenDialect dialect,
            IReadOnlyDictionary<int, string>? colorOverlays)
        {
            var keys = new List<KeyboardKeyViewModel>(layer.Keys.Count);

            foreach (var key in layer.Keys)
            {
                if (!visual.TryGetKey(key.Index, out var keyVisual))
                {
                    continue;
                }

                string? overlay = null;

                if (colorOverlays is not null && colorOverlays.TryGetValue(key.Index, out var assignedColor))
                {
                    overlay = assignedColor;
                }

                keys.Add(new KeyboardKeyViewModel(key, keyVisual, dialect, overlay));
            }

            return keys;
        }
    }
}
