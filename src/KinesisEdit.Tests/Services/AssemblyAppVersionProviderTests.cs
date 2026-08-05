using System.Reflection;
using System.Reflection.Emit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The app's own version as the "SmartSet App :" row compares it (specs/09-firmware.md
    /// §3 step 2): four numeric components, of which the comparison keeps the first three.
    /// </summary>
    public class AssemblyAppVersionProviderTests
    {
        [Theory]
        [InlineData(2, 1, 3, 17, "2.1.3.17")]
        [InlineData(2, 0, 20, 0, "2.0.20.0")]
        public void Format_WithEveryComponent_UsesTheSpecFormat(int major, int minor, int build, int revision, string expected)
        {
            Assert.Equal(expected, AssemblyAppVersionProvider.Format(new Version(major, minor, build, revision)));
        }

        [Fact]
        public void Format_WithUnspecifiedComponents_PadsThemWithZeroes()
        {
            Assert.Equal("2.1.0.0", AssemblyAppVersionProvider.Format(new Version(2, 1)));
            Assert.Equal("2.1.3.0", AssemblyAppVersionProvider.Format(new Version(2, 1, 3)));
        }

        [Fact]
        public void Format_WithoutAVersion_IsEmpty()
        {
            Assert.Equal(string.Empty, AssemblyAppVersionProvider.Format(null));
        }

        [Fact]
        public void Format_WhenParsedForComparison_KeepsTheFirstThreeComponents()
        {
            // The build number is the fourth component and must never affect the comparison.
            Assert.True(FirmwareVersion.TryParse(AssemblyAppVersionProvider.Format(new Version(2, 1, 3, 17)), out var version));
            Assert.Equal(new FirmwareVersion(2, 1, 3), version);
        }

        [Fact]
        public void Version_ForAnAssembly_ReadsItsVersionResource()
        {
            // A purpose-built assembly with a known version: reading the running one back through
            // the same expression the provider uses would pass for any value at all.
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("KinesisEdit.Tests.VersionedFixture") { Version = new Version(3, 2, 1, 7) },
                AssemblyBuilderAccess.Run);

            Assert.Equal("3.2.1.7", new AssemblyAppVersionProvider(assembly).Version);
        }

        [Fact]
        public void Version_WithoutAnAssembly_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AssemblyAppVersionProvider((Assembly)null!));
        }
    }
}
