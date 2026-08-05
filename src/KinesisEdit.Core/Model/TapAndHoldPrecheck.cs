using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The four pre-dialog checks each app runs before opening the Assign Tap and Hold Action
    /// dialog (specs/11-feature-dialogs.md §11.1, "Pre-dialog validation in the calling apps"),
    /// evaluated against the model so the UI only has to render the verdict. The refusal wording
    /// is quoted verbatim, the same "messages are spec text" pattern as
    /// <see cref="ModelViolation.Message"/> and
    /// <c>KinesisEdit.Core.Profiles.ProfileSaveMessageCatalog</c>.
    /// <para>
    /// Scope is the four checks only. Whether the device supports tap-and-hold at all
    /// (<see cref="TapAndHoldCapability.IsSupported"/>) and whether its firmware clears the gate
    /// (<c>KinesisEdit.Core.Firmware.FirmwareGateService</c>, which owns the "please download and
    /// install the latest firmware" refusal) are separate questions the caller asks first.
    /// </para>
    /// </summary>
    public static class TapAndHoldPrecheck
    {
        /// <summary>Index of the top/base layer (specs/05-key-model.md §1.4).</summary>
        public const int TopLayerIndex = 0;

        private const string SameKeyInBothLayersMessage =
            "You cannot assign a Tap and Hold Action to the same key in both layers.";

        private const string MaximumReachedMessage =
            "You have reached the maximum number of Tap and Hold actions for this Profile.";

        private const string MacroTriggerKeyMessage =
            "You cannot assign a Tap and Hold Action to a macro trigger key.";

        private const string AlphanumericOnTopLayerMessage =
            "You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.";

        /// <summary>
        /// Returns the first check of §11.1 that refuses <paramref name="key"/> on
        /// <paramref name="layer"/>, or <see cref="TapAndHoldRefusal.None"/> when the dialog may
        /// open. The order is the spec's, so a key breaching several checks reports the first one
        /// the legacy app would have shown.
        /// </summary>
        public static TapAndHoldRefusal Evaluate(KeyboardLayout layout, KeyboardLayer layer, KeyboardKey key)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(layer);
            ArgumentNullException.ThrowIfNull(key);

            if (HasTapAndHoldOnAnotherLayer(layout, layer, key))
            {
                return TapAndHoldRefusal.SameKeyInBothLayers;
            }

            if (HasReachedMaximum(layout, key))
            {
                return TapAndHoldRefusal.MaximumReached;
            }

            if (IsMacroTrigger(layout, layer, key))
            {
                return TapAndHoldRefusal.MacroTriggerKey;
            }

            if (IsAlphanumericOnTopLayer(layer, key))
            {
                return TapAndHoldRefusal.AlphanumericOnTopLayer;
            }

            return TapAndHoldRefusal.None;
        }

        /// <summary>
        /// The message §11.1 shows for <paramref name="refusal"/>, verbatim;
        /// <see cref="TapAndHoldRefusal.None"/> has none and returns an empty string.
        /// </summary>
        public static string MessageFor(TapAndHoldRefusal refusal)
        {
            return refusal switch
            {
                TapAndHoldRefusal.None => string.Empty,
                TapAndHoldRefusal.SameKeyInBothLayers => SameKeyInBothLayersMessage,
                TapAndHoldRefusal.MaximumReached => MaximumReachedMessage,
                TapAndHoldRefusal.MacroTriggerKey => MacroTriggerKeyMessage,
                TapAndHoldRefusal.AlphanumericOnTopLayer => AlphanumericOnTopLayerMessage,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(refusal), refusal, "No tap-and-hold refusal message is defined for this value.")
            };
        }

        /// <summary>
        /// Whether the same physical position carries a tap-and-hold on a different layer. Key
        /// indices are dense and identify the same position on every layer of a device
        /// (05 §7.4), so the position is looked up by index.
        /// </summary>
        private static bool HasTapAndHoldOnAnotherLayer(KeyboardLayout layout, KeyboardLayer layer, KeyboardKey key)
        {
            foreach (var candidate in layout.Layers)
            {
                if (candidate.Index == layer.Index)
                {
                    continue;
                }

                if (candidate.FindByIndex(key.Index) is { IsTapAndHold: true })
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the layout already holds <see cref="TapAndHoldCapability.MaxPerLayout"/>
        /// assignments. A device whose capability states no maximum cannot reach one.
        /// <para>
        /// <paramref name="key"/> is excluded from the count. §11.1 caps how many tap-and-hold
        /// actions a profile <em>has</em> ("Maximum of 10 tap-and-hold actions per profile
        /// reached"), and re-opening the dialog on a key that already carries one cannot produce an
        /// eleventh — it rewrites the one that is there. Counting it would lock the last ten
        /// assignments of a full profile out of ever being edited again, which no legacy app
        /// behaviour the spec records asks for.
        /// </para>
        /// </summary>
        private static bool HasReachedMaximum(KeyboardLayout layout, KeyboardKey key)
        {
            if (layout.Device.TapAndHold.MaxPerLayout is not { } maximum)
            {
                return false;
            }

            var otherAssignments = key.IsTapAndHold ? layout.TapAndHoldCount - 1 : layout.TapAndHoldCount;

            return otherAssignments >= maximum;
        }

        /// <summary>
        /// Whether the key hosts a macro, in either store: the per-key slots of the slot-based
        /// families, or the Advantage360 flat list keyed by layer plus trigger key (06 §1).
        /// </summary>
        private static bool IsMacroTrigger(KeyboardLayout layout, KeyboardLayer layer, KeyboardKey key)
        {
            return key.IsMacro || layout.FindMacros(layer.Index, key.TriggerKey.Code).Count > 0;
        }

        /// <summary>
        /// Whether the position is one of the A-Z / 0-9 keys on the top layer. The test is on the
        /// position's factory-default action — §11.1 names the physical keys, not whatever a remap
        /// currently puts on them — and the A-Z / 0-9 set is exactly key table §3.1
        /// (<see cref="KeyTable.LettersAndDigits"/>).
        /// </summary>
        private static bool IsAlphanumericOnTopLayer(KeyboardLayer layer, KeyboardKey key)
        {
            return layer.Index == TopLayerIndex && key.OriginalKey.Table == KeyTable.LettersAndDigits;
        }
    }
}
