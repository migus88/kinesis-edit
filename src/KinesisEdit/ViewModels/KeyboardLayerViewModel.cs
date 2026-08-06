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
        /// Re-paints the layer's colour strips from a freshly built
        /// <see cref="KeyColorOverlay"/> map. A key the map does not mention loses its strip, so
        /// this is the whole-layer form and not a merge — erasing a colour has to be visible too.
        /// <para>
        /// The overlay cannot come from <see cref="RefreshFromModel"/>: it lives in the lighting
        /// model, which no layout parser ever writes into <see cref="KeyboardKey.KeyColor"/>.
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
