using System.Globalization;
using System.Reflection;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Reads the app's own version off the running executable's version resource — the .NET
    /// equivalent of the embedded resource specs/09-firmware.md §3 step 2 reads — and formats it
    /// with all four components. <see cref="Core.Devices.FirmwareVersion"/> truncates it to three
    /// when comparing, so the fourth component never affects the outcome; it is kept because the
    /// spec pins the format.
    /// </summary>
    public sealed class AssemblyAppVersionProvider : IAppVersionProvider
    {
        /// <summary>
        /// Formats <paramref name="version"/> as the spec's four components; empty when the
        /// assembly carries no version at all. Components the assembly left unspecified (-1)
        /// count as 0, so the text always has four of them.
        /// </summary>
        public static string Format(Version? version)
        {
            if (version is null)
            {
                return string.Empty;
            }

            return string.Create(
                CultureInfo.InvariantCulture,
                $"{version.Major}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}.{Math.Max(version.Revision, 0)}");
        }

        /// <summary>The four-component version text of the assembly this provider was built for.</summary>
        public string Version { get; }

        /// <summary>Creates the provider over the entry assembly, falling back to this one when there is none.</summary>
        public AssemblyAppVersionProvider() : this(Assembly.GetEntryAssembly() ?? typeof(AssemblyAppVersionProvider).Assembly)
        {
        }

        /// <summary>Creates the provider over <paramref name="assembly"/>.</summary>
        public AssemblyAppVersionProvider(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            Version = Format(assembly.GetName().Version);
        }
    }
}
