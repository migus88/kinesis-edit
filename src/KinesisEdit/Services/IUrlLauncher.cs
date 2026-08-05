namespace KinesisEdit.Services
{
    /// <summary>
    /// Opens an external page in the user's browser — the "Troubleshooting Tips" link of
    /// specs/11-feature-dialogs.md §11.8.
    /// </summary>
    public interface IUrlLauncher
    {
        /// <summary>Opens <paramref name="url"/>; returns false when the platform refused to open it.</summary>
        bool Open(string url);
    }
}
