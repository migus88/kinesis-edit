namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// A firmware-gated feature from the minimum-firmware gate table of specs/09-firmware.md §2.
    /// Values identify what a gate unlocks; the thresholds live in
    /// <see cref="FirmwareGateCatalog"/> and are evaluated by <see cref="FirmwareGateService"/>.
    /// </summary>
    public enum FirmwareFeature
    {
        None = 0,

        /// <summary>FS Edge/Pro: 100 macros per layout instead of 24.</summary>
        ExpandedMacroCount = 1,

        /// <summary>FS Edge/Pro: custom (1-999 ms) and random macro timing delays.</summary>
        CustomMacroDelays = 2,

        /// <summary>Hyper/Meh multimodifier remaps (FS Edge/Pro, Advantage2, RGB).</summary>
        Multimodifiers = 3,

        /// <summary>Tap and Hold actions (FS Edge/Pro, Advantage2, RGB; ungated on TKO and Adv360).</summary>
        TapAndHold = 4,

        /// <summary>RGB: the Ripple and Fireball lighting effects.</summary>
        RippleAndFireballEffects = 5,

        /// <summary>RGB: Fn-layer lighting switch and the per-layer base color panel.</summary>
        LightingLayerCustomization = 6,

        /// <summary>RGB: the one-time "Expansion Pack" dialog after an LED-firmware update.</summary>
        ExpansionPackOffer = 7,

        /// <summary>TKO: the startup macro-firmware warning shown on the original 1.0.0 firmware.</summary>
        MacroFirmwareWarning = 8,

        /// <summary>Advantage 360: macro selection buttons inside the Tap &amp; Hold editor.</summary>
        TapAndHoldMacroActions = 9
    }
}
