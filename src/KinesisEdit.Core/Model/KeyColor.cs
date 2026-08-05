namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Per-key LED colour of a key position (specs/05-key-model.md §1.3 <c>KeyColor</c>), as the
    /// three decimal 0-255 components led files carry (specs/07-lighting.md §2.1, §4). A key with
    /// no colour is modelled as a null <see cref="KeyboardKey.KeyColor"/>, not as black — led
    /// files omit uncoloured keys. Core has no UI/System.Drawing dependency, so this value type
    /// is the model's own colour representation.
    /// </summary>
    /// <param name="Red">Red component, 0-255.</param>
    /// <param name="Green">Green component, 0-255.</param>
    /// <param name="Blue">Blue component, 0-255.</param>
    public readonly record struct KeyColor(byte Red, byte Green, byte Blue);
}
