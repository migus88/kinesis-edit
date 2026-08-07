using System.Reflection;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The path seam. Two things are asserted here, and the second is the reason the seam exists:
    /// that the real location is the platform-appropriate one, and that <b>this suite never
    /// resolves or writes to it</b> — a guarantee that is worth nothing as a convention and is
    /// therefore a test.
    /// </summary>
    public class HostPreferencesPathProviderTests
    {
        [Fact]
        public void GetFilePath_PutsTheFile_UnderTheAppsOwnFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));

            var path = new HostPreferencesPathProvider(root).GetFilePath();

            Assert.Equal(
                Path.Combine(root, HostPreferencesPathProvider.DirectoryName, HostPreferencesPathProvider.FileName),
                path);
        }

        [Fact]
        public void GetFilePath_IsStable_AcrossCalls()
        {
            var provider = new HostPreferencesPathProvider(Path.GetTempPath());

            Assert.Equal(provider.GetFilePath(), provider.GetFilePath());
        }

        [Fact]
        public void Constructor_Rejects_AnEmptyRoot()
        {
            Assert.Throws<ArgumentException>(() => new HostPreferencesPathProvider("   "));
            Assert.Throws<ArgumentNullException>(() => new HostPreferencesPathProvider(null!));
        }

        [Fact]
        public void CreateForCurrentPlatform_ResolvesTheUsersOwnConfigurationDirectory()
        {
            var path = HostPreferencesPathProvider.CreateForCurrentPlatform().GetFilePath();

            Assert.True(Path.IsPathRooted(path), $"'{path}' is not an absolute path.");
            Assert.EndsWith(
                Path.Combine(HostPreferencesPathProvider.DirectoryName, HostPreferencesPathProvider.FileName),
                path,
                StringComparison.Ordinal);

            if (OperatingSystem.IsMacOS())
            {
                // .NET maps SpecialFolder.ApplicationData to ~/.config on macOS, which is why the
                // roots are written out rather than taken from it.
                Assert.Contains(Path.Combine("Library", "Application Support"), path, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheSuite_NeverWrites_ToTheRealUserConfigurationDirectory()
        {
            var realPath = HostPreferencesPathProvider.CreateForCurrentPlatform().GetFilePath();
            var existedBefore = File.Exists(realPath);
            var writtenBefore = existedBefore ? File.GetLastWriteTimeUtc(realPath) : default;

            using (var temporary = new TemporaryHostPreferences())
            {
                var store = temporary.CreateStore();

                store.Update(preferences => preferences with
                {
                    Theme = AppThemePreference.Dark,
                    Window = new WindowGeometry(1000, 680, 1, 2, false)
                });

                // The write landed in the throwaway root, not the user's.
                Assert.True(File.Exists(temporary.FilePath));
                Assert.NotEqual(realPath, temporary.FilePath);
            }

            Assert.Equal(existedBefore, File.Exists(realPath));

            if (existedBefore)
            {
                Assert.Equal(writtenBefore, File.GetLastWriteTimeUtc(realPath));
            }
        }

        [Fact]
        public void TheStore_ResolvesNoPathOfItsOwn()
        {
            // The structural half of the guarantee above: every way to build a store names a path
            // provider, and there is no factory that quietly reaches for the real one. Adding a
            // JsonHostPreferencesStore.CreateForCurrentPlatform() would put the real location one
            // careless call away from every test in the suite.
            var constructors = typeof(JsonHostPreferencesStore).GetConstructors();

            Assert.NotEmpty(constructors);
            Assert.All(
                constructors,
                constructor => Assert.Contains(
                    constructor.GetParameters(),
                    parameter => parameter.ParameterType == typeof(IHostPreferencesPathProvider)));

            Assert.DoesNotContain(
                typeof(JsonHostPreferencesStore).GetMethods(BindingFlags.Public | BindingFlags.Static),
                method => typeof(IHostPreferencesStore).IsAssignableFrom(method.ReturnType));
        }
    }
}
