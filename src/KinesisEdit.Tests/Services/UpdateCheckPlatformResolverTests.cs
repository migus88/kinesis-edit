using System.Runtime.InteropServices;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The app-layer half of the app-key selection of specs/09-firmware.md §3 step 4: which
    /// operating system the process runs on. Core never probes the runtime, so this is the only
    /// place <see cref="RuntimeInformation"/> is consulted.
    /// </summary>
    public class UpdateCheckPlatformResolverTests
    {
        [Fact]
        public void Resolve_OnTheHostPlatform_MatchesTheRuntime()
        {
            var expected = UpdateCheckPlatform.None;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                expected = UpdateCheckPlatform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                expected = UpdateCheckPlatform.MacOs;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                expected = UpdateCheckPlatform.Linux;
            }

            Assert.Equal(expected, UpdateCheckPlatformResolver.Resolve());
        }

        [Fact]
        public void Resolve_OnASupportedPlatform_ResolvesAnAppKey()
        {
            // The three platforms the app ships on all map to a manifest key; None would silently
            // turn the app row into "Error fetching app version".
            Assert.NotEqual(UpdateCheckPlatform.None, UpdateCheckPlatformResolver.Resolve());
            Assert.NotNull(UpdateCheckKeys.FindAppKey(ServingApp.SmartSetMasterGaming, UpdateCheckPlatformResolver.Resolve()));
        }
    }
}
