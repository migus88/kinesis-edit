using KinesisEdit.Core.Geometry.Visual;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One drawn panel of the keyboard picture: a <see cref="KeyboardSection"/> of the board joined
    /// to the caps that sit in it. A split board is two of these — mockup 1e draws the hotkey
    /// column plus the left typing half in one bordered box and the right half in another, with the
    /// design's split gutter between them.
    /// <para>
    /// The caps are the <b>same instances</b> as <see cref="KeyboardLayerViewModel.Keys"/>, not
    /// copies: the editor looks a key up through the flat list and
    /// <see cref="KeyboardLayerViewModel.ApplyColorOverlays"/> writes through it, so a second set of
    /// objects would leave the picture silently stale.
    /// </para>
    /// <para>
    /// Geometry is in <b>key units</b> and stays board-absolute — <see cref="OriginX"/>/
    /// <see cref="OriginY"/> say where the panel starts inside the whole board, which is what lets a
    /// renderer place each cap at <c>key.X - OriginX</c> without any coordinate in Core being
    /// re-based. Nothing here changes after construction, so this view model raises no notification
    /// and deliberately does not derive from <see cref="ViewModelBase"/>.
    /// </para>
    /// </summary>
    public sealed class KeyboardSectionViewModel
    {
        /// <summary>0-based panel ordinal, dense across the board.</summary>
        public int Index { get; }

        /// <summary>The panel's left edge in key units, board-absolute.</summary>
        public double OriginX { get; }

        /// <summary>The panel's top edge in key units, board-absolute.</summary>
        public double OriginY { get; }

        /// <summary>The panel's width in key units — what the view scales by.</summary>
        public double BoardWidth { get; }

        /// <summary>The panel's height in key units — what the view scales by.</summary>
        public double BoardHeight { get; }

        /// <summary>The panel's key caps, in the board's authoring order.</summary>
        public IReadOnlyList<KeyboardKeyViewModel> Keys { get; }

        /// <summary>Joins one board panel to the caps of the layer that fall inside it.</summary>
        public KeyboardSectionViewModel(KeyboardSection section, IReadOnlyList<KeyboardKeyViewModel> keys)
        {
            ArgumentNullException.ThrowIfNull(section);
            ArgumentNullException.ThrowIfNull(keys);

            Index = section.Index;
            OriginX = section.X;
            OriginY = section.Y;
            BoardWidth = section.Width;
            BoardHeight = section.Height;
            Keys = keys;
        }
    }
}
