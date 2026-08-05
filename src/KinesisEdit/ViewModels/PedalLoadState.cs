namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Where the seven rows of <see cref="SavantElitePedalViewModel"/> came from. Only
    /// <see cref="Loaded"/> means the device's <c>active/pedals.txt</c> was actually read; the
    /// other three are the expected ways there is nothing to read, and the view colors its note
    /// from this value rather than from a brush the view model would have to know about.
    /// </summary>
    public enum PedalLoadState
    {
        /// <summary>Nothing has been attempted yet.</summary>
        None = 0,

        /// <summary>The file was read and parsed (specs/12-savant-elite.md §4.2).</summary>
        Loaded = 1,

        /// <summary>Demo mode: no drive was touched at all (03 §3.5, 12 §3).</summary>
        DemoMode = 2,

        /// <summary>The drive is there but carries no <c>pedals.txt</c> yet (12 §5 step 8).</summary>
        FileMissing = 3,

        /// <summary>The file exists but could not be read.</summary>
        LoadFailed = 4
    }
}
