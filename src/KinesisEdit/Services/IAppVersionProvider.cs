namespace KinesisEdit.Services
{
    /// <summary>
    /// The app's own version, as the "SmartSet App :" row of specs/09-firmware.md §3 step 2
    /// compares it: four numeric components formatted <c>major.minor.revision.build</c>. An
    /// interface so the update dialog can be tested against a pinned version instead of whatever
    /// the test host's assembly happens to carry.
    /// </summary>
    public interface IAppVersionProvider
    {
        /// <summary>The four-component version text; never null, empty only when nothing could be read.</summary>
        string Version { get; }
    }
}
