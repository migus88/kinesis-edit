using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// <b>The only way this suite builds a file-backed host-preferences store.</b> It hands out a
    /// real <see cref="HostPreferencesPathProvider"/> over a throwaway root under the temp
    /// directory, and asserts <i>in its own constructor</i> that the root it chose is not the real
    /// per-user configuration directory — so a test that forgets is not a test that quietly writes
    /// to the developer's own preferences file.
    /// </summary>
    internal sealed class TemporaryHostPreferences : IDisposable
    {
        /// <summary>The throwaway configuration root, always under the temp directory.</summary>
        public string Root { get; }

        /// <summary>The preferences file inside <see cref="Root"/>. Does not exist until written.</summary>
        public string FilePath { get; }

        /// <summary>The provider every store built here goes through.</summary>
        public IHostPreferencesPathProvider Paths { get; }

        public TemporaryHostPreferences()
        {
            Root = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Paths = new HostPreferencesPathProvider(Root);
            FilePath = Paths.GetFilePath();

            var realPath = HostPreferencesPathProvider.CreateForCurrentPlatform().GetFilePath();

            Assert.NotEqual(realPath, FilePath);
            Assert.StartsWith(Path.GetTempPath(), FilePath, StringComparison.Ordinal);
        }

        /// <summary>A store over <see cref="FilePath"/>.</summary>
        public JsonHostPreferencesStore CreateStore()
        {
            return new JsonHostPreferencesStore(Paths);
        }

        /// <summary>Puts <paramref name="content"/> at <see cref="FilePath"/>, verbatim.</summary>
        public void WriteFile(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, content);
        }

        /// <summary>The file's text, or null when it does not exist.</summary>
        public string? ReadFile()
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
