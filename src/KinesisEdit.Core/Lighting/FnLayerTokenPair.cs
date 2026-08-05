namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// One Fn-layer save-token exception (specs/07-lighting.md §2.4 item 7;
    /// specs/05-key-model.md §5.5): the in-memory key code of the media/navigation action
    /// living on the Fn layer, and the key code of the top-layer position token it is written
    /// under. Expressed as key codes, not strings — the file tokens come from the key registry.
    /// </summary>
    /// <param name="MemoryKeyCode">In-memory key code (e.g. VK_VOLUME_MUTE).</param>
    /// <param name="FileKeyCode">Key code whose file token is written instead (e.g. VK_F1).</param>
    public readonly record struct FnLayerTokenPair(int MemoryKeyCode, int FileKeyCode);
}
