using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The shell's <b>Help</b> screen — the app bar's third nav pill. A table of links out of the
    /// app, and a card saying which app this is.
    /// <para>
    /// <b>Nothing here is invented.</b> Every per-device row is a
    /// <see cref="DeviceDefinition.TroubleshootingUrl"/> out of <see cref="DeviceCatalog"/>. A
    /// board the catalog carries no page for gets no row, rather than a dead one.
    /// </para>
    /// <para>
    /// Every row is drawn with <c>Button.link</c> plus the <c>IconExternalLink</c> mark and opened
    /// through <see cref="IUrlLauncher"/>. The mockups write such a caption as "… ↗"; the arrow is
    /// geometry beside the words and never a character in the string
    /// (docs/app/design-system.md, "Icons and device art").
    /// </para>
    /// </summary>
    public sealed class HelpScreenViewModel : ViewModelBase
    {
        /// <summary>
        /// Kinesis' support index — the page that answers "which board do I have?".
        /// <para>
        /// <b>This is the app's first app-level URL, and it is app-level by design.</b> Every other
        /// address in the codebase hangs off a <see cref="DeviceDefinition"/>, because it is about
        /// one board; this one is the root of all of them and belongs to no device, so it cannot
        /// live in the catalog. It stays here, on the one screen that offers it, rather than in a
        /// URL registry with a single entry.
        /// </para>
        /// </summary>
        public const string KinesisSupportUrl = "https://kinesis-ergo.com/support/";

        /// <summary>The screen's title.</summary>
        public const string Title = "Help";

        /// <summary>What the screen is, in one line.</summary>
        public const string Subtitle =
            "Everything below opens in your browser. KinesisEdit itself makes no network requests.";

        /// <summary>Caption over the general links.</summary>
        public const string GeneralSectionLabel = "Start here";

        /// <summary>What is on the other end of the support index.</summary>
        public const string KinesisSupportDescription =
            "Kinesis' support index — every board it sells, with its manual, its firmware and its FAQs.";

        /// <summary>Caption over the per-board troubleshooting rows.</summary>
        public const string TroubleshootingSectionLabel = "Troubleshooting, by device";

        /// <summary>What the per-board rows are.</summary>
        public const string TroubleshootingSectionNote =
            "Connection steps, firmware downloads and the support page for each board KinesisEdit knows about.";

        /// <summary>Caption over the about card.</summary>
        public const string AboutSectionLabel = "About";

        /// <summary>The product name, as the app bar's wordmark spells it.</summary>
        public const string AppName = "KinesisEdit";

        /// <summary>What the app is, in the one sentence the about card has room for.</summary>
        public const string AboutText =
            "A cross-platform editor for Kinesis' SmartSet keyboards and pedals. It edits the plain-text "
            + "configuration files on a board's v-Drive; the board's own firmware reads them back.";

        /// <summary>What the version line reads when the assembly carries no usable version at all.</summary>
        public const string UnknownVersion = "unknown";

        // "Version 0.1.0" - the app speaking, so sans type and a spelled-out label rather than a
        // bare number that could be mistaken for a value out of a file.
        private const string VersionTemplate = "Version {0}";

        // The support-page row's caption, per device: "Advantage2 support".
        private const string DeviceSupportTemplate = "{0} support";

        /// <summary>
        /// The version the built assembly reports, with any build metadata dropped.
        /// <para>
        /// <c>InformationalVersion</c> is the one the SDK derives from the project's
        /// <c>&lt;Version&gt;</c> and the only one that can carry a pre-release tag, so it is what a
        /// user is shown. The SDK appends <c>+&lt;commit&gt;</c> to it when source-link is on; the
        /// commit is not what "which version am I running" means, so it is cut. A blank or absent
        /// attribute falls back to the assembly version and then to
        /// <see cref="UnknownVersion"/> — a Help screen is never worth a throw.
        /// </para>
        /// </summary>
        public static string ReadVersion(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var metadata = informational.IndexOf('+', StringComparison.Ordinal);
                var trimmed = (metadata < 0 ? informational : informational[..metadata]).Trim();

                if (trimmed.Length > 0)
                {
                    return trimmed;
                }
            }

            return assembly.GetName().Version?.ToString() ?? UnknownVersion;
        }

        /// <summary>Formats the about card's version line.</summary>
        public static string FormatVersionLine(string version)
        {
            ArgumentNullException.ThrowIfNull(version);

            return string.Format(CultureInfo.InvariantCulture, VersionTemplate, version);
        }

        /// <summary>The links that are about the app rather than about one board.</summary>
        public IReadOnlyList<HelpLink> GeneralLinks { get; }

        /// <summary>One row per catalogued board that has a troubleshooting page, in catalog order.</summary>
        public IReadOnlyList<HelpLink> TroubleshootingLinks { get; }

        /// <summary>The product name shown on the about card.</summary>
        public string ProductName => AppName;

        /// <summary>"Version 0.1.0".</summary>
        public string VersionLine { get; }

        /// <summary>Opens a row's page in the user's browser.</summary>
        public IRelayCommand<HelpLink> OpenLinkCommand { get; }

        private readonly IUrlLauncher _urlLauncher;

        /// <summary>
        /// Creates the screen. <paramref name="assembly"/> is the one the version is read from and
        /// defaults to this app's, which is what the shell wants; a test passes its own.
        /// </summary>
        public HelpScreenViewModel(IUrlLauncher urlLauncher, Assembly? assembly = null)
        {
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));

            GeneralLinks = CreateGeneralLinks();
            TroubleshootingLinks = CreateTroubleshootingLinks();
            VersionLine = FormatVersionLine(ReadVersion(assembly ?? typeof(HelpScreenViewModel).Assembly));

            OpenLinkCommand = new RelayCommand<HelpLink>(OpenLink);
        }

        private static IReadOnlyList<HelpLink> CreateGeneralLinks()
        {
            return
            [
                new HelpLink
                {
                    // The same question the read-only settings banner asks, so the app spells it
                    // once. Core owns the wording (specs 08 / mockup 1j); this screen borrows it
                    // rather than declaring a second copy that could drift from it.
                    Caption = SettingsMessageCatalog.WhichBoardLinkCaption,
                    Url = KinesisSupportUrl,
                    Description = KinesisSupportDescription
                }
            ];
        }

        private static IReadOnlyList<HelpLink> CreateTroubleshootingLinks()
        {
            return
            [
                .. DeviceCatalog.All
                    .Where(device => device.TroubleshootingUrl is not null)
                    .Select(device => new HelpLink
                    {
                        Caption = FormatDeviceSupportCaption(device.DisplayName),
                        Url = device.TroubleshootingUrl!
                    })
            ];
        }

        private static string FormatDeviceSupportCaption(string deviceName)
        {
            return string.Format(CultureInfo.InvariantCulture, DeviceSupportTemplate, deviceName);
        }

        private void OpenLink(HelpLink? link)
        {
            if (link is null)
            {
                return;
            }

            _urlLauncher.Open(link.Url);
        }
    }
}
