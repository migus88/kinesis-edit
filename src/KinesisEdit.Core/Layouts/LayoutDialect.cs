namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// One of the four in-scope layout-file dialects of specs/04-layout-file-format.md §4.1.
    /// Finer-grained than <see cref="Keys.TokenDialect"/> on purpose: the token dialect cannot
    /// tell the old Freestyle format (one persisted co-trigger, three persisted macro slots, no
    /// line-validity tracking in the legacy app) from the Gen1 RGB/TKO format, nor the
    /// Advantage2 format from the SE2 pedal file — which is not a layout dialect at all and
    /// lives in <see cref="SavantElite.PedalFileParser"/> (spec 12 §4).
    /// </summary>
    public enum LayoutDialect
    {
        /// <summary>No layout dialect — the device has no in-scope layout files.</summary>
        None = 0,

        /// <summary>FS Edge / FS Pro: <c>fn </c> layer prefix, 3 persisted macro slots, 1 persisted co-trigger (04 §4.1).</summary>
        Freestyle = 1,

        /// <summary>Edge RGB / TKO: <c>fn </c> layer prefix, full line-validity tracking, 5 macro slots (04 §4.1).</summary>
        Gen1Rgb = 2,

        /// <summary>Advantage360: <c>&lt;...&gt;</c> layer headers, flat macro list (04 §4.1, §3.3).</summary>
        Gen2 = 3,

        /// <summary>Advantage2: <c>kp-</c> token prefix, <c>{speedN}</c> macro speed syntax (04 §4.1, §3.2).</summary>
        Advantage2 = 4
    }
}
