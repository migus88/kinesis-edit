namespace KinesisEdit.Services
{
    /// <summary>
    /// Where this user's host-preferences file lives. Abstracted so each platform's answer is
    /// isolated in one implementation — and, just as importantly, <b>so a test can never resolve
    /// to the real user configuration directory</b>. <see cref="JsonHostPreferencesStore"/> takes
    /// one of these and resolves no path of its own; there is deliberately no
    /// <c>CreateForCurrentPlatform</c> shortcut on the store, so the only way to reach the real
    /// location is to name <see cref="HostPreferencesPathProvider.CreateForCurrentPlatform"/>
    /// explicitly, which the composition root does and nothing else may.
    /// </summary>
    public interface IHostPreferencesPathProvider
    {
        /// <summary>
        /// The full path of the preferences file. The directory need not exist yet — the store
        /// creates it on first write.
        /// </summary>
        string GetFilePath();
    }
}
