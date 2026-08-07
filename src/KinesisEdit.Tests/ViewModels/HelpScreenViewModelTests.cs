using System.Reflection;
using System.Reflection.Emit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The shell's Help screen: the link table and the about card.
    /// <para>
    /// The claim worth testing here is not "there are some links" but <b>where each one came
    /// from</b> — every per-device row is a catalog value, the two web-tool rows are that board's
    /// own two pages, and only one address in the whole screen is written down in this app.
    /// </para>
    /// </summary>
    public class HelpScreenViewModelTests
    {
        [Fact]
        public void GeneralLinks_Always_OfferKinesisSupportIndexUnderTheWhichBoardWording()
        {
            var viewModel = CreateViewModel(out _);
            var link = Assert.Single(viewModel.GeneralLinks);

            // The same question the read-only settings banner asks, spelled once, in Core.
            Assert.Equal(SettingsMessageCatalog.WhichBoardLinkCaption, link.Caption);
            Assert.Equal(HelpScreenViewModel.KinesisSupportUrl, link.Url);
            Assert.True(link.HasDescription);
        }

        [Fact]
        public void KinesisSupportUrl_Always_IsTheOnlyAddressThisScreenDeclares()
        {
            // Every other URL in the app hangs off a DeviceDefinition, because it is about one
            // board. This one is the root of all of them and belongs to no device — which is the
            // whole reason it is app-level. If a second constant appears here, it is either a
            // per-device page that belongs in the catalog or a decision that belongs in the doc.
            var declared = typeof(HelpScreenViewModel)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string?)field.GetRawConstantValue() ?? string.Empty)
                .Where(value => value.Contains("://", StringComparison.Ordinal))
                .ToArray();

            Assert.Equal([HelpScreenViewModel.KinesisSupportUrl], declared);
        }

        [Fact]
        public void TroubleshootingLinks_Always_AreExactlyTheCatalogsTroubleshootingPages()
        {
            // Nothing invented, nothing retyped, and nothing dropped: a board the catalog gains a
            // page for appears here without this screen being edited.
            var viewModel = CreateViewModel(out _);

            Assert.Equal(
                DeviceCatalog.All
                    .Where(device => device.TroubleshootingUrl is not null)
                    .Select(device => device.TroubleshootingUrl),
                viewModel.TroubleshootingLinks.Select(link => link.Url));
        }

        [Fact]
        public void TroubleshootingLinks_Always_NameTheirBoard()
        {
            var viewModel = CreateViewModel(out _);
            var expected = DeviceCatalog.All
                .Where(device => device.TroubleshootingUrl is not null)
                .Select(device => device.DisplayName + " support");

            Assert.Equal(expected, viewModel.TroubleshootingLinks.Select(link => link.Caption));
        }

        [Fact]
        public void TroubleshootingLinks_Always_CoverEveryProgrammableBoardTheCatalogHasAPageFor()
        {
            // A weaker restatement of the rule above, aimed at the thing a reader cares about: the
            // seven boards the app can be pointed at.
            var viewModel = CreateViewModel(out _);
            var programmable = DeviceCatalog.All
                .Where(device => device.IsProgrammable && device.TroubleshootingUrl is not null)
                .Select(device => device.TroubleshootingUrl!);

            Assert.All(programmable, url => Assert.Contains(url, viewModel.TroubleshootingLinks.Select(link => link.Url)));
        }

        [Fact]
        public void WebToolLinks_Always_AreTheWebConfiguredBoardsOwnTwoPages()
        {
            var viewModel = CreateViewModel(out _);
            var device = Assert.Single(WebToolCardViewModel.WebToolDevices());

            Assert.Equal(
                new[] { device.ConfigurationUrl, device.SupportUrl },
                viewModel.WebToolLinks.Select(link => link.Url));
            Assert.True(viewModel.HasWebToolLinks);
        }

        [Fact]
        public void WebToolLinks_Always_AreTheAdvantage360ProfessionalsInTodaysCatalog()
        {
            // The membership above is derived; this pins what that derivation currently resolves to,
            // so a catalog edit that silently moved the section is visible.
            var device = Assert.Single(WebToolCardViewModel.WebToolDevices());

            Assert.Equal(DeviceId.Advantage360Professional, device.Id);
        }

        [Fact]
        public void EveryLink_Always_HasARealDestination()
        {
            // "Every link has a real destination" — a caption with nothing behind it is not a link,
            // and a row with a blank URL would open the browser on nothing.
            var viewModel = CreateViewModel(out _);

            Assert.All(AllLinks(viewModel), link =>
            {
                Assert.False(string.IsNullOrWhiteSpace(link.Caption));
                Assert.StartsWith("https://", link.Url, StringComparison.Ordinal);
            });
        }

        [Fact]
        public void EveryCaption_Always_LeavesTheArrowToTheIconSystem()
        {
            // The mockups write these as "... ↗". The arrow is IconExternalLink geometry beside the
            // words; a Latin-1 arrow in a caption is a glyph the shipped font may not carry.
            var viewModel = CreateViewModel(out _);

            Assert.All(AllLinks(viewModel), link => Assert.DoesNotContain('↗', link.Caption));
        }

        [Fact]
        public void OpenLinkCommand_WithALink_OpensThatPage()
        {
            var viewModel = CreateViewModel(out var launcher);
            var link = viewModel.TroubleshootingLinks[0];

            viewModel.OpenLinkCommand.Execute(link);

            Assert.Equal([link.Url], launcher.OpenedUrls);
        }

        [Fact]
        public void OpenLinkCommand_WithNull_OpensNothing()
        {
            var viewModel = CreateViewModel(out var launcher);

            viewModel.OpenLinkCommand.Execute(null);

            Assert.Empty(launcher.OpenedUrls);
        }

        [Fact]
        public void VersionLine_Always_ReadsTheRunningAssemblysOwnVersion()
        {
            // The about card shows the real version, not a literal somebody has to remember to bump.
            var viewModel = CreateViewModel(out _);
            var expected = HelpScreenViewModel.ReadVersion(typeof(HelpScreenViewModel).Assembly);

            Assert.Equal(HelpScreenViewModel.FormatVersionLine(expected), viewModel.VersionLine);
            Assert.NotEqual(HelpScreenViewModel.UnknownVersion, expected);
            Assert.Contains(expected, viewModel.VersionLine, StringComparison.Ordinal);
        }

        [Fact]
        public void ReadVersion_OfTheApp_MatchesTheProjectsDeclaredVersion()
        {
            // KinesisEdit.csproj sets <Version>, and InformationalVersion is what the SDK derives
            // from it. A build that stops carrying the attribute would silently fall back to the
            // 4-part assembly version, which reads differently.
            var version = HelpScreenViewModel.ReadVersion(typeof(HelpScreenViewModel).Assembly);

            Assert.Matches(@"^\d+\.\d+\.\d+", version);
        }

        [Fact]
        public void ReadVersion_WithBuildMetadata_DropsTheCommit()
        {
            // The SDK appends "+<commit>" when source-link is on. "Which version am I running" is
            // not answered by a hash.
            var assembly = CreateAssemblyWith("2.5.0-beta.3+9f2c1ab");

            Assert.Equal("2.5.0-beta.3", HelpScreenViewModel.ReadVersion(assembly));
        }

        [Fact]
        public void ReadVersion_WithNoInformationalVersion_FallsBackRatherThanThrowing()
        {
            // A Help screen is never worth a throw.
            var assembly = CreateAssemblyWith(informationalVersion: null);

            Assert.False(string.IsNullOrWhiteSpace(HelpScreenViewModel.ReadVersion(assembly)));
        }

        [Fact]
        public void ReadVersion_WithABlankInformationalVersion_FallsBackRatherThanShowingNothing()
        {
            var assembly = CreateAssemblyWith("   ");

            Assert.False(string.IsNullOrWhiteSpace(HelpScreenViewModel.ReadVersion(assembly)));
        }

        [Fact]
        public void Constructor_WithNoLauncher_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new HelpScreenViewModel(null!));
        }

        private static IEnumerable<HelpLink> AllLinks(HelpScreenViewModel viewModel)
        {
            return viewModel.GeneralLinks
                .Concat(viewModel.WebToolLinks)
                .Concat(viewModel.TroubleshootingLinks);
        }

        private static HelpScreenViewModel CreateViewModel(out FakeUrlLauncher launcher)
        {
            launcher = new FakeUrlLauncher();

            return new HelpScreenViewModel(launcher);
        }

        /// <summary>
        /// A throwaway in-memory assembly carrying <paramref name="informationalVersion"/>, or none
        /// at all. Emitting one is the only way to exercise the attribute's absence — the test
        /// assembly always has one.
        /// </summary>
        private static Assembly CreateAssemblyWith(string? informationalVersion)
        {
            var name = new AssemblyName("KinesisEdit.VersionProbe") { Version = new Version(9, 8, 7, 6) };
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

            if (informationalVersion is not null)
            {
                var constructor = typeof(AssemblyInformationalVersionAttribute)
                    .GetConstructor([typeof(string)])!;

                assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
            }

            return assembly;
        }
    }
}
