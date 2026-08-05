namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// An RGB color value of the lighting system: three decimal components 0-255
    /// (specs/07-lighting.md §2.1). Black is the "no color" value — per-key maps omit
    /// black-colored keys on save, and "no color" serializes as <c>[0][0][0]</c>.
    /// </summary>
    public readonly record struct LedColor
    {
        /// <summary>The "no color" value (specs/07-lighting.md §2.1, §6).</summary>
        public static LedColor Black { get; } = new(0, 0, 0);

        /// <summary>The default effect color, lime green RGB(0,255,0) (specs/07-lighting.md §6).</summary>
        public static LedColor DefaultEffectColor { get; } = new(0, 255, 0);

        /// <summary>Red component, 0-255.</summary>
        public byte Red { get; init; }

        /// <summary>Green component, 0-255.</summary>
        public byte Green { get; init; }

        /// <summary>Blue component, 0-255.</summary>
        public byte Blue { get; init; }

        /// <summary>Whether this is the black "no color" value (specs/07-lighting.md §2.1).</summary>
        public bool IsBlack => Red == 0 && Green == 0 && Blue == 0;

        /// <summary>Creates a color from its three decimal components.</summary>
        public LedColor(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }
    }
}
