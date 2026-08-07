namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// One key handed to <see cref="LightingEffectSampler"/>: its memory key code and where it
    /// sits on the board.
    /// <para>
    /// <b>Coordinate contract.</b> <c>X</c> and <c>Y</c> are the key's centre in <i>normalised
    /// board space</i>: 0..1 across the whole board picture, X growing to the right and Y growing
    /// downward (screen convention). Both halves of a split board share the one space — including
    /// the gutter between them — so a travelling effect crosses from one half to the other the way
    /// it does on the hardware. Core never knows where a key sits; the app layer derives these
    /// from the visual geometry (<c>KinesisEdit.Core.Geometry.Visual</c>) and supplies them.
    /// Values outside 0..1 are not rejected, they simply fall outside the effect's travel.
    /// </para>
    /// </summary>
    /// <param name="KeyCode">The memory key code, the same code <see cref="LayerLightingState.KeyColors"/> uses.</param>
    /// <param name="X">The key centre's horizontal position, 0 at the board's left edge, 1 at its right.</param>
    /// <param name="Y">The key centre's vertical position, 0 at the board's top edge, 1 at its bottom.</param>
    public readonly record struct LightingPreviewKey(int KeyCode, double X, double Y);
}
