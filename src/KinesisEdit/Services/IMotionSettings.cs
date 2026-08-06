namespace KinesisEdit.Services
{
    /// <summary>
    /// Whether the app animates. The design's motion budget (docs/design/mockups.md, mockup 2b)
    /// ships in two flavors — the full budget and a reduced one that is fades only, with no rise,
    /// no slide and no height animation — and this is the single switch that says which one the
    /// app is using.
    /// <para>
    /// The value is resolved once at startup from the OS accessibility setting and then handed to
    /// <see cref="MotionResourceBinder"/>, which points the motion alias resources at the matching
    /// set. The setter exists so a later issue can add a preference or another platform's
    /// detection without reshaping anything; re-run <see cref="MotionResourceBinder.Apply"/> after
    /// changing it, because the alias resources are not recomputed on their own.
    /// </para>
    /// </summary>
    public interface IMotionSettings
    {
        /// <summary>True when the app must animate with the reduced budget.</summary>
        bool ReduceMotion { get; set; }
    }
}
