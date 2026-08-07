using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The one-line explanation printed under every control of the Lighting rail — <b>per mode and
    /// per property</b>, because that is the whole point of it: the question this exists to answer
    /// is "I don't understand what is the difference between Effect and Base color in reactive
    /// mode", and one sentence per <i>property</i> could not answer it.
    /// <para>
    /// <b>It is UI copy, not file format</b>, which is why it lives in the app layer: Core answers
    /// what a mode <i>accepts</i> (<see cref="Core.Lighting.Preview.LightingModeParameters"/>) and
    /// nothing here re-decides that. This class only says, in English, what the accepted thing
    /// means. Every entry cites the spec section it was written from.
    /// </para>
    /// <para>
    /// <b>It describes the hardware's effect as specs/07-lighting.md §3 describes it — never the
    /// preview sampler.</b> docs/app/lighting.md records that the firmware's effect algorithms are
    /// undocumented and that the on-screen previews are "representative approximations, not
    /// simulations", so a sentence here may go no further than §3's own "Visual behavior:"
    /// paragraph and the §2.2 line grammar. Where mockup 2f's parameter lists disagree with §3,
    /// §3 wins.
    /// </para>
    /// </summary>
    public static class LightingHintCatalog
    {
        /// <summary>
        /// What the picker does while the selected mode has a colour swatch of its own: it edits
        /// whichever swatch is highlighted, and — because a colour always paints the current paint
        /// selection (specs/07-lighting.md §4) — the keys picked on the board too.
        /// </summary>
        public const string PickerWithSwatchHint =
            "Edits the highlighted swatch above, and paints any keys selected on the board.";

        /// <summary>
        /// What it does while the mode has none. That is <b>Spectrum, Wave and Pulse</b>: §2.2
        /// writes no colour of their own, but they do write a layer, and the per-key colours are
        /// the <i>layer's</i> — so the picker is still the colour a paint gesture uses. Off and
        /// Pitch Black have no swatch either and once read this line too, wrongly: they write
        /// nothing at all (§2.2), so the picker is not drawn in them and this never speaks for
        /// them.
        /// </summary>
        public const string PickerPaintOnlyHint =
            "This mode supplies its own colours. What you pick here paints the keys you select on "
            + "the board — those colours stay on file and show through under the effect.";

        /// <summary>What the mode does, from §3's "Visual behavior:" paragraph and §2.2's grammar.</summary>
        public static string ForMode(LightingMode mode)
        {
            return mode switch
            {
                // §2.2: nothing is written for the layer; §6: choosing Disable writes an empty
                // section.
                LightingMode.Disabled => "The backlight is off for this layer, and nothing is written to the file for it.",

                // §2.2/§4: pure per-key colouring — the file body is nothing but per-key lines.
                LightingMode.Freestyle => "Every key simply keeps the colour you paint on it. Nothing animates.",

                // §2.2: a single [mono] line; §3: "1 effect color", no speed, no direction.
                LightingMode.Monochrome => "One colour, held steady across the whole board.",

                // §3 Visual behavior: "Breathe and Pulse fade the whole board in and out";
                // §2.2: a speed header followed by per-key colour lines.
                LightingMode.Breathe => "Fades the whole board in and out, over the colours you paint on the keys.",

                // §3 Visual behavior: "Spectrum cycles hue board-wide"; §3 table: auto colour cycle.
                LightingMode.Spectrum => "Cycles the hue across the whole board. The colours are the effect's, not yours.",

                // §3 Visual behavior: "Wave scrolls a rainbow across the board"; auto rainbow.
                LightingMode.Wave => "Scrolls a rainbow across the board, the way the direction says.",

                // §3 table: per-LED colours, static. Edge-only, so it is never in the RGB menu.
                LightingMode.FrozenWave => "A wave held still: every LED keeps the colour you give it.",

                // §3 Visual behavior: "Reactive lights keys on key-press over the base color";
                // §2.2: a [mono] base line, then the effect line.
                LightingMode.Reactive => "Lights a key as you strike it, over the resting colour of the board.",

                // §3 Visual behavior: "Ripple expands rings".
                LightingMode.Ripple => "Expands a ring out from each key you strike, over the resting colour.",

                // §3 Visual behavior: "Fireball shoots across a row".
                LightingMode.Fireball => "Shoots a streak across a row, over the resting colour.",

                // §3 Visual behavior: "Starlight twinkles random keys".
                LightingMode.Starlight => "Twinkles random keys, over the resting colour of the board.",

                // §3 Visual behavior: "Rebound bounces a bar"; §3: its two directions are
                // horizontal/vertical.
                LightingMode.Rebound => "Bounces a bar across the board, along whichever axis you choose.",

                // §3 Visual behavior: "Loop sweeps in the chosen direction".
                LightingMode.Loop => "Sweeps a band across the board in the direction you choose.",

                // §3 Visual behavior: "Breathe and Pulse fade the whole board in and out"; §3
                // table: Pulse takes no colour.
                LightingMode.Pulse => "Fades the whole board in and out.",

                // §3 Visual behavior: "Rain drops random columns".
                LightingMode.Rain => "Drops colour down random columns, over the resting colour.",

                // §2.2: reserved token, neither written nor read by the legacy apps — so it is
                // never offered on a keyboard's menu, and this line only ever shows if one appears.
                _ => "A reserved firmware mode. The app never writes it."
            };
        }

        /// <summary>
        /// What the mode's <b>effect colour</b> is. For the per-key modes it is not file state at
        /// all — §2.2 writes no effect-colour line for Freestyle, Breathe or Frozen Wave — so it is
        /// described as what it really is there: the colour a paint gesture uses.
        /// </summary>
        public static string ForEffectColor(LightingMode mode)
        {
            return mode switch
            {
                // §2.2/§4: the per-key modes. The swatch is the picker's colour, stored per key
                // rather than as a line of its own — see LightingColorSlotViewModel.EffectCaptionFor.
                LightingMode.Freestyle => "The colour the keys you select are painted with. It is stored per key.",
                LightingMode.Breathe => "The colour the keys you select are painted with; Breathe fades those colours.",
                LightingMode.FrozenWave => "The colour the LEDs you select are painted with. It is stored per LED.",

                // §2.2: [mono]>[R][G][B] — the one colour the board is lit with.
                LightingMode.Monochrome => "The one colour the whole board is lit with.",

                // §2.2: [reactive]>[R][G][B][spdN], over a [mono] base line. This pair is the
                // literal answer to "what is the difference between Effect and Base color".
                LightingMode.Reactive => "The colour a key flashes when you strike it.",

                // §2.2: [ripple]>… over a [mono] base line; §3: "Ripple expands rings".
                LightingMode.Ripple => "The colour of the ring that expands from the key you strike.",

                // §2.2: [fireball]>… over a [mono] base line; §3: "Fireball shoots across a row".
                LightingMode.Fireball => "The colour of the streak that shoots across the row.",

                // §2.2: [star]>… over a [mono] base line; §3: "Starlight twinkles random keys".
                LightingMode.Starlight => "The colour of the keys that twinkle.",

                // §2.2: [rebound]>… over a [mono] base line; §3: "Rebound bounces a bar".
                LightingMode.Rebound => "The colour of the bar that bounces across the board.",

                // §2.2: [loop]>… over a [mono] base line; §3: "Loop sweeps in the chosen direction".
                LightingMode.Loop => "The colour of the band that sweeps across the board.",

                // §2.2: [rain]>… over a [mono] base line; §3: "Rain drops random columns".
                LightingMode.Rain => "The colour of the drops that fall down the board.",

                // Spectrum, Wave, Pulse, Off and Pitch Black accept no effect colour at all (§3),
                // so the swatch is not drawn and this line is never read.
                _ => "The colour this effect is drawn in."
            };
        }

        /// <summary>
        /// What the mode's <b>base colour</b> is: the <c>[mono]</c> line §2.2 emits immediately
        /// before the effect line of a two-line effect — "the effect's background/base color".
        /// </summary>
        public static string ForBaseColor(LightingMode mode)
        {
            return mode switch
            {
                // §2.2 "Base color line"; §3 Visual behavior for each of the seven two-line effects.
                LightingMode.Reactive => "The colour every key rests at between strikes.",
                LightingMode.Ripple => "The colour every key rests at under the rings.",
                LightingMode.Fireball => "The colour every key rests at under the streak.",
                LightingMode.Starlight => "The colour the keys that are not twinkling rest at.",
                LightingMode.Rebound => "The colour every key rests at under the bar.",
                LightingMode.Loop => "The colour every key rests at under the band.",
                LightingMode.Rain => "The colour every key rests at under the drops.",

                // No other mode writes a base line (§2.2), so the swatch is not drawn for one.
                _ => "The colour the board rests at under the effect."
            };
        }

        /// <summary>
        /// What the nine-step speed knob moves. §2.1 fixes the range at 1-9; §3 lists which modes
        /// carry one. Only the modes whose §3 sentence names a motion get a sentence of their own —
        /// the rest take the general one rather than inventing firmware behaviour specs/ does not
        /// document.
        /// </summary>
        public static string ForSpeed(LightingMode mode)
        {
            return mode switch
            {
                // §3 Visual behavior: "Breathe and Pulse fade the whole board in and out".
                LightingMode.Breathe or LightingMode.Pulse => "How fast the board fades in and out. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Spectrum cycles hue board-wide".
                LightingMode.Spectrum => "How fast the hue cycles. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Wave scrolls a rainbow across the board".
                LightingMode.Wave => "How fast the rainbow scrolls. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Ripple expands rings".
                LightingMode.Ripple => "How fast the rings expand. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Fireball shoots across a row".
                LightingMode.Fireball => "How fast the streak travels. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Starlight twinkles random keys".
                LightingMode.Starlight => "How fast the keys twinkle. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Rebound bounces a bar".
                LightingMode.Rebound => "How fast the bar bounces. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Loop sweeps in the chosen direction".
                LightingMode.Loop => "How fast the band sweeps. 1 is slowest, 9 fastest.",

                // §3 Visual behavior: "Rain drops random columns".
                LightingMode.Rain => "How fast the drops fall. 1 is slowest, 9 fastest.",

                // Reactive carries a speed (§3) but §3 says nothing about what it times, and the
                // firmware's algorithm is undocumented — so this stays general on purpose.
                _ => "How fast the effect runs. 1 is slowest, 9 fastest."
            };
        }

        /// <summary>
        /// What the direction arrows move. §3 gives the RGB a direction panel for Wave, Rebound and
        /// Loop only (the TKO adds Fireball), and replaces Rebound's four arrows with
        /// Horizontal/Vertical.
        /// </summary>
        public static string ForDirection(LightingMode mode)
        {
            return mode switch
            {
                // §3 Visual behavior: "Wave scrolls a rainbow across the board".
                LightingMode.Wave => "Which way the rainbow travels across the board.",

                // §3 Visual behavior: "Loop sweeps in the chosen direction".
                LightingMode.Loop => "Which way the band sweeps.",

                // §3: Rebound's valid values are left (horizontal) and up (vertical).
                LightingMode.Rebound => "Which axis the bar bounces along.",

                // §3: Fireball's direction panel exists on the TKO only, left/right.
                LightingMode.Fireball => "Which way the streak travels.",

                _ => "Which way the effect travels."
            };
        }

        /// <summary>
        /// What the colour picker under the swatches writes. Two answers, because a mode with no
        /// colour of its own still has a picker: the per-key colours belong to the <i>layer</i>
        /// (§4), not to the effect running over them. Neither answer is asked for in Off or Pitch
        /// Black — the rail draws no picker where nothing is written
        /// (<see cref="Core.Lighting.Preview.LightingModeParameters.AcceptsPaint"/>).
        /// </summary>
        /// <param name="acceptsAnyColor">
        /// <see cref="Core.Lighting.Preview.LightingModeParameters.AcceptsAnyColor"/> — Core's
        /// answer, never re-derived here.
        /// </param>
        public static string ForPicker(bool acceptsAnyColor)
        {
            return acceptsAnyColor ? PickerWithSwatchHint : PickerPaintOnlyHint;
        }
    }
}
