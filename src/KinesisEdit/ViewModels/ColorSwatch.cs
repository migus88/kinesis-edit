using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One premixed color of the shared picker (specs/07-lighting.md §4 "Color picker"): a name
    /// and the color itself. Immutable, so it needs no change notification — the picker only ever
    /// reads it. The color travels as a <see cref="LedColor"/> and as a <c>#RRGGBB</c> string,
    /// never as an Avalonia brush (docs/app/app-shell.md, invariant 6).
    /// </summary>
    public sealed record ColorSwatch
    {
        /// <summary>The swatch's name, used as its tooltip.</summary>
        public required string Name { get; init; }

        /// <summary>The swatch's color.</summary>
        public required LedColor Color { get; init; }

        /// <summary>The swatch's color as the <c>#RRGGBB</c> string the view paints with.</summary>
        public string ColorHex => KeyColorOverlay.ToHex(Color);
    }
}
