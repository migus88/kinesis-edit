namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// One of the seven programmable inputs of the Savant Elite2 control module
    /// (specs/12-savant-elite.md §1): three pedals plus four accessory jacks. "Your device will
    /// only have some of these inputs" — the file always carries all seven.
    /// <para>
    /// The values follow the registration order of
    /// <see cref="Keys.KeyRegistry.PedalPositionTokens"/> (specs/05-key-model.md §3.14), which is
    /// also the fixed save order of 12 §4.6; the file tokens themselves live in
    /// <see cref="PedalInputs"/> and are never duplicated here.
    /// </para>
    /// </summary>
    public enum PedalInputId
    {
        /// <summary>Not an input — a file line that addresses no pedal (12 §4.1: preserved verbatim).</summary>
        None = 0,

        /// <summary>The left pedal, file token <c>lpedal</c>.</summary>
        LeftPedal = 1,

        /// <summary>The middle pedal, file token <c>mpedal</c>.</summary>
        MiddlePedal = 2,

        /// <summary>The right pedal, file token <c>rpedal</c>.</summary>
        RightPedal = 3,

        /// <summary>Accessory jack 1, file token <c>jack1</c>.</summary>
        Jack1 = 4,

        /// <summary>Accessory jack 2, file token <c>jack2</c>.</summary>
        Jack2 = 5,

        /// <summary>Accessory jack 3, file token <c>jack3</c>.</summary>
        Jack3 = 6,

        /// <summary>Accessory jack 4, file token <c>jack4</c>.</summary>
        Jack4 = 7
    }
}
