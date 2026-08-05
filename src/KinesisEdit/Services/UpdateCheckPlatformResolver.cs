using System.Runtime.InteropServices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Resolves the operating system whose SmartSet-app build the update check compares
    /// (specs/09-firmware.md §3 step 4). This lives in the app layer on purpose:
    /// <c>KinesisEdit.Core</c> never probes the runtime, it takes
    /// <see cref="UpdateCheckPlatform"/> as data so every OS × master-app combination stays
    /// unit-testable. The composition root resolves it once and passes the value down.
    /// </summary>
    public static class UpdateCheckPlatformResolver
    {
        /// <summary>The platform this process runs on; <c>None</c> on an OS the spec knows no app key for.</summary>
        public static UpdateCheckPlatform Resolve()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return UpdateCheckPlatform.Windows;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return UpdateCheckPlatform.MacOs;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return UpdateCheckPlatform.Linux;
            }

            return UpdateCheckPlatform.None;
        }
    }
}
