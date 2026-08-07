using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the mode rail calls each lighting mode. It is <see cref="LightingModeCatalog"/>'s
    /// <see cref="LightingModeDefinition.DisplayName"/> for every mode except the three the design
    /// renames, and it is the <b>only</b> place in the app that spells a mode name.
    /// <para>
    /// <b>A deliberate design-vs-Core deviation, and a presentation-only one.</b> Core's display
    /// names are specs/07-lighting.md §3's legacy captions and stay untouched — they are what the
    /// spec says the menu read, and nothing that parses or serializes a led file may drift with a
    /// label. Design mockup 2f renames three of them for the rail: "Monochrome" is not what a
    /// solid colour is called anywhere else in the app, "Disable" is a verb where the row is a
    /// state, and "PitchBlack" needs a space. Every <i>parameter</i> of a row still comes from
    /// <see cref="Core.Lighting.Preview.LightingModeParameters"/>; only the word changes.
    /// </para>
    /// </summary>
    public static class LightingModeCaptions
    {
        /// <summary>Mockup 2f's name for <see cref="LightingMode.Monochrome"/> ("Monochrome" in §3).</summary>
        public const string SolidCaption = "Solid";

        /// <summary>Mockup 2f's name for <see cref="LightingMode.Disabled"/> ("Disable" in §3).</summary>
        public const string OffCaption = "Off";

        /// <summary>Mockup 2f's name for <see cref="LightingMode.PitchBlack"/> ("PitchBlack" in §3).</summary>
        public const string PitchBlackCaption = "Pitch black";

        /// <summary>What the rail calls <paramref name="mode"/>.</summary>
        public static string For(LightingMode mode)
        {
            return mode switch
            {
                LightingMode.Monochrome => SolidCaption,
                LightingMode.Disabled => OffCaption,
                LightingMode.PitchBlack => PitchBlackCaption,
                _ => LightingModeCatalog.Find(mode).DisplayName
            };
        }
    }
}
