namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Lighting hardware of a device, per the master device table in specs/02-devices.md:
    /// the lighting kind, the TKO's 33-LED case-edge strip (9 left, 15 bottom, 9 right),
    /// and the Advantage 360's 6 indicator LEDs.
    /// </summary>
    public sealed record LightingCapability
    {
        /// <summary>A capability for devices without lighting.</summary>
        public static LightingCapability None { get; } = new();

        /// <summary>The kind of lighting hardware.</summary>
        public LightingKind Kind { get; init; }

        /// <summary>Number of status indicator LEDs (Advantage 360: 6, 3 per side); 0 otherwise.</summary>
        public int IndicatorLedCount { get; init; }

        /// <summary>Case-edge LEDs on the left edge (TKO: 9); 0 otherwise.</summary>
        public int EdgeLedLeftCount { get; init; }

        /// <summary>Case-edge LEDs on the bottom edge (TKO: 15); 0 otherwise.</summary>
        public int EdgeLedBottomCount { get; init; }

        /// <summary>Case-edge LEDs on the right edge (TKO: 9); 0 otherwise.</summary>
        public int EdgeLedRightCount { get; init; }

        /// <summary>Total case-edge LEDs (TKO: 33).</summary>
        public int EdgeLedTotalCount => EdgeLedLeftCount + EdgeLedBottomCount + EdgeLedRightCount;

        /// <summary>Whether the device has a case-edge LED strip in addition to per-key lighting (TKO only).</summary>
        public bool HasEdgeLighting => EdgeLedTotalCount > 0;
    }
}
