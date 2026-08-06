namespace KinesisEdit.ViewModels.Advisories
{
    /// <summary>
    /// Where an advisory belongs on screen: the section that shows it, and — where the source knew
    /// — the layer, the key position and the macro slot it is about. Everything is optional but the
    /// tab, because the sources report at different resolutions: the tap-and-hold count is a
    /// property of the whole layout, a duplicate token is a property of one position.
    /// <para>
    /// The indices are the model's own (<see cref="Core.Model.KeyboardLayer.Index"/>,
    /// <see cref="Core.Model.KeyboardKey.Index"/>, and the 1-based macro slot of 06 §1), which is
    /// what lets the editor turn an anchor back into a selection — that is the whole point of the
    /// type. <see cref="MacroIndex"/> is null for a flat-list macro, which names no slot.
    /// </para>
    /// </summary>
    public sealed record AdvisoryAnchor
    {
        /// <summary>The section the advisory is shown in.</summary>
        public required EditorTab Tab { get; init; }

        /// <summary>The layer it sits on, or null when it is layout-wide.</summary>
        public int? LayerIndex { get; init; }

        /// <summary>The key position it sits on, or null when it is not position-specific.</summary>
        public int? KeyIndex { get; init; }

        /// <summary>The 1-based macro slot it sits in, or null (06 §1: a flat-list macro has none).</summary>
        public int? MacroIndex { get; init; }
    }
}
