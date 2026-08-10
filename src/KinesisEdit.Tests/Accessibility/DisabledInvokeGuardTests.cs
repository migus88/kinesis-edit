using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Controls;
using KinesisEdit.Tests.Design;
using KinesisEdit.Tests.Headless;

namespace KinesisEdit.Tests.Accessibility
{
    /// <summary>
    /// The disabled-press guard — <c>Controls/AccessibleButton.cs</c>,
    /// <c>Controls/AccessibleToggleButton.cs</c> and the rule they share,
    /// <c>Controls/DisabledPressGuard.cs</c> (issue #54).
    /// <para>
    /// The defect is a process kill, not a wrong pixel: <c>ButtonAutomationPeer.Invoke()</c> and
    /// <c>ToggleButtonAutomationPeer.Toggle()</c> call <c>EnsureEnabled()</c>, which throws on a
    /// disabled owner, and on macOS that call arrives from Objective-C — a managed exception
    /// escaping a native-to-managed callback fail-fasts the app. So a VoiceOver user pressing any
    /// disabled button killed it.
    /// </para>
    /// <para>
    /// <b>Everything here presses the way the platform presses.</b>
    /// <c>native/Avalonia.Native/src/OSX/automation.mm</c>'s <c>accessibilityPerformPress</c> is an
    /// <c>if / else if / else if</c> chain that <i>asks for</i> a provider before it uses one, and
    /// the fix works by making that question answer <c>null</c>. <c>Invoke()</c> itself is
    /// non-virtual and unchanged, so a direct
    /// <c>((IInvokeProvider)peer).Invoke()</c> on a disabled button <b>still throws, correctly</b>
    /// — that is what <see cref="AStockButton_StillThrowsWhenItsDisabledPeerIsInvoked"/> pins.
    /// <see cref="PerformPress"/> is the mirror of the native chain and is the path every guard
    /// assertion drives.
    /// </para>
    /// <para>
    /// Every control is rooted through <see cref="ThemedHost"/>, never merely constructed:
    /// <c>AutomationPeer.IsEnabled()</c> resolves to <c>Owner.IsEffectivelyEnabled</c>, which is a
    /// property of a control in a tree.
    /// </para>
    /// </summary>
    public partial class DisabledInvokeGuardTests
    {
        private const double HostWidth = 240;

        private const double HostHeight = 120;

        /// <summary>
        /// A floor for the buttons the scan below must actually see, not a census: the app authors
        /// 95 of them today across 26 files. It is what stops the guard passing because the corpus
        /// came back empty.
        /// </summary>
        private const int AuthoredButtonFloor = 90;

        /// <summary>Each authoring class and the theme <c>Styles/Buttons.axaml</c> bridges it to.</summary>
        public static TheoryData<string, string, string> BridgedClasses()
        {
            var cases = new TheoryData<string, string, string>();

            foreach (var (className, key) in new[]
            {
                ("primaryAction", "PrimaryActionButton"),
                ("secondary", "SecondaryButton"),
                ("eject", "EjectButton"),
                ("discard", "DiscardButton"),
                ("ghost", "GhostButton"),
                ("link", "LinkButton"),
                ("rowButton", "RowButton")
            })
            {
                cases.Add(className, key, "Dark");
                cases.Add(className, key, "Light");
            }

            return cases;
        }

        [AvaloniaFact]
        public void ADisabledButton_HandsThePlatformNoInvokeProvider()
        {
            // Criterion 1. All three of the types accessibilityPerformPress consults, because the
            // chain has to fall through rather than move one branch to the right. Two of them are
            // null on an enabled button as well (see the next test) — asserting them here is about
            // the chain's shape, not about today's base peer.
            var button = new AccessibleButton { Content = "Save", IsEnabled = false };

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(button);

            Assert.False(button.IsEffectivelyEnabled);
            Assert.False(peer.IsEnabled());

            Assert.Null(peer.GetProvider<IInvokeProvider>());
            Assert.Null(peer.GetProvider<IExpandCollapseProvider>());
            Assert.Null(peer.GetProvider<IToggleProvider>());
        }

        [AvaloniaFact]
        public void ACommandGatedButton_HandsThePlatformNoInvokeProvider()
        {
            // The shape both reported crashes actually had: neither the pedal editor's Save nor the
            // shell's Settings pill binds IsEnabled — each is a command whose CanExecute says no,
            // and Button folds that into IsEffectivelyEnabled itself. A guard keyed on an explicit
            // IsEnabled binding would have missed both, so the rule is asserted through the command
            // as well as through the property.
            var command = new RelayCommand(() => { }, () => false);
            var button = new AccessibleButton { Content = "Save", Command = command };

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.True(button.IsEnabled, "The test disabled the button the wrong way.");
            Assert.False(button.IsEffectivelyEnabled);

            var peer = ControlAutomationPeer.CreatePeerForElement(button);

            Assert.Null(peer.GetProvider<IInvokeProvider>());

            PerformPress(peer);
        }

        [AvaloniaFact]
        public void AnEnabledButton_StillAdvertisesItsInvokeProvider()
        {
            // Criterion 2's first half — and a record of what the base peer offers: ButtonAutomationPeer
            // implements IInvokeProvider and nothing else, so the other two branches of the native
            // chain are unreachable for a Button whether it is enabled or not. If a future Avalonia
            // gives the peer an IExpandCollapseProvider, this fails and says so, and the guard
            // already covers it.
            var button = new AccessibleButton { Content = "Save" };

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(button);

            Assert.True(peer.IsEnabled());
            Assert.NotNull(peer.GetProvider<IInvokeProvider>());
            Assert.Null(peer.GetProvider<IExpandCollapseProvider>());
            Assert.Null(peer.GetProvider<IToggleProvider>());
        }

        [AvaloniaFact]
        public void AnEnabledButtonsInvokeProvider_StillRaisesClickAndStillRunsItsCommand()
        {
            // Criterion 2's second half. Click and Command are different paths out of one press —
            // a peer that raised the event but never reached the command would satisfy half the
            // app and none of the Save buttons.
            var clicks = 0;
            var command = new CountingCommand();
            var button = new AccessibleButton { Content = "Save", Command = command };

            button.Click += (_, _) => clicks++;

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(button);

            PerformPress(peer);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, clicks);
            Assert.Equal(1, command.Executions);
        }

        [AvaloniaFact]
        public void PressingADisabledButton_TheWayThePlatformDoes_IsANoOpAndThrowsNothing()
        {
            // Criterion 3. "Throws nothing" is the crash gone; "no-op" is the other half — a guard
            // that swallowed the exception by pressing anyway would pass a throw-free test and
            // would run the command the app deliberately gated.
            var clicks = 0;
            var command = new CountingCommand();
            var button = new AccessibleButton { Content = "Save", Command = command, IsEnabled = false };

            button.Click += (_, _) => clicks++;

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(button);

            PerformPress(peer);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, clicks);
            Assert.Equal(0, command.Executions);
        }

        [AvaloniaFact]
        public void TheGuard_IsAskedOnEveryPress_NotOnceWhenThePeerIsBuilt()
        {
            // A control keeps one peer for its lifetime (Control._automationPeer is cached), and
            // every button in the app is disabled and re-enabled repeatedly as its command's
            // CanExecute moves. A guard evaluated at construction would leave the app's Save
            // permanently unpressable by VoiceOver after its first clean moment.
            var button = new AccessibleButton { Content = "Save", IsEnabled = false };

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(button);

            Assert.Null(peer.GetProvider<IInvokeProvider>());

            button.IsEnabled = true;

            Dispatcher.UIThread.RunJobs();

            Assert.Same(peer, ControlAutomationPeer.CreatePeerForElement(button));
            Assert.NotNull(peer.GetProvider<IInvokeProvider>());
        }

        [AvaloniaFact]
        public void ADisabledToggleButton_HandsThePlatformNoToggleProvider()
        {
            // Criterion 4. ToggleButtonAutomationPeer does not derive from ButtonAutomationPeer —
            // it implements IToggleProvider explicitly and nothing else — so a toggle is pressed
            // through the native chain's *third* branch. That is exactly why the guard nulls all
            // three types rather than IInvokeProvider alone.
            var toggle = new AccessibleToggleButton { Content = "Off", IsEnabled = false };

            using var host = ThemedHost.Show(toggle, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(toggle);

            Assert.False(peer.IsEnabled());
            Assert.Null(peer.GetProvider<IToggleProvider>());
            Assert.Null(peer.GetProvider<IInvokeProvider>());
            Assert.Null(peer.GetProvider<IExpandCollapseProvider>());

            PerformPress(peer);

            Dispatcher.UIThread.RunJobs();

            Assert.False(toggle.IsChecked);
        }

        [AvaloniaFact]
        public void AnEnabledToggleButton_StillFlipsThroughItsToggleProvider()
        {
            // The other half of criterion 4: the guard must cost an enabled switch nothing.
            var toggle = new AccessibleToggleButton { Content = "Off" };

            using var host = ThemedHost.Show(toggle, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(toggle);
            var provider = peer.GetProvider<IToggleProvider>();

            Assert.NotNull(provider);
            Assert.Equal(ToggleState.Off, provider.ToggleState);

            PerformPress(peer);

            Dispatcher.UIThread.RunJobs();

            Assert.True(toggle.IsChecked);
            Assert.Equal(ToggleState.On, provider.ToggleState);
        }

        [AvaloniaFact]
        public void AStockButton_StillThrowsWhenItsDisabledPeerIsInvoked()
        {
            // Why the subclass exists, pinned rather than remembered. This is the *direct* call —
            // the one the native side makes only after asking for the provider — and it is
            // supposed to throw: Invoke() is non-virtual, and the fix removes the question, not
            // the answer.
            //
            // REMOVAL CONDITION. When this test fails, the managed hazard is gone upstream and
            // AccessibleButton, AccessibleToggleButton, DisabledPressGuard and the views' 95
            // `controls:` prefixes can all go. It cannot see the *other* possible upstream fix —
            // Avalonia.Native guarding its own callbacks, which is invisible from managed code —
            // so a package bump is also the moment to re-read
            // `src/Avalonia.Native/AvnAutomationPeer.cs`; it was still unguarded at tag 11.3.20.
            // See docs/app/design-system.md, "The disabled-press guard".
            var button = new Button { Content = "Save", IsEnabled = false };

            using var host = ThemedHost.Show(button, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(button);
            var provider = Assert.IsAssignableFrom<IInvokeProvider>(peer);

            Assert.Throws<ElementNotEnabledException>(() => provider.Invoke());
        }

        [AvaloniaFact]
        public void AStockToggleButton_StillThrowsWhenItsDisabledPeerIsToggled()
        {
            // The same, one branch to the right — the branch nulling IInvokeProvider alone would
            // have moved the crash onto.
            var toggle = new ToggleButton { Content = "Off", IsEnabled = false };

            using var host = ThemedHost.Show(toggle, ThemeVariant.Dark, HostWidth, HostHeight);

            var peer = ControlAutomationPeer.CreatePeerForElement(toggle);
            var provider = Assert.IsAssignableFrom<IToggleProvider>(peer);

            Assert.Throws<ElementNotEnabledException>(() => provider.Toggle());
        }

        [AvaloniaTheory]
        [MemberData(nameof(BridgedClasses))]
        public void ABridgedAccessibleButton_ResolvesTheThemeAStockButtonWould(
            string className,
            string key,
            string variantName)
        {
            // Criterion 6, and the assertion that protects all 95 swapped call sites. Avalonia's
            // OfType selector is an exact-type match, so `Button.primaryAction` would stop matching
            // a subclass outright — except that TypeNameAndClassSelector evaluates the *style key*,
            // which AccessibleButton overrides to typeof(Button). Delete StyleKeyOverride and every
            // one of these fails.
            //
            // Two controls, one window, so this cannot pass by the class simply resolving: the
            // subclass must reach the very same ControlTheme instance the stock button does.
            var variant = ToVariant(variantName);
            var stock = new Button { Classes = { className }, Content = "Home" };
            var accessible = new AccessibleButton { Classes = { className }, Content = "Home" };
            var panel = new StackPanel { Children = { stock, accessible } };

            using var host = ThemedHost.Show(panel, variant, HostWidth, HostHeight * 2);

            Assert.Same(DesignTokens.Resolve(key, variant), accessible.Theme);
            Assert.Same(stock.Theme, accessible.Theme);

            // ...and the theme's own setters landed on it, not merely its reference: the template
            // and the resting face both come from the ControlTheme rather than from the bridge.
            Assert.NotNull(accessible.Template);
            Assert.Same(stock.Template, accessible.Template);
            Assert.Equal(stock.Background, accessible.Background);
            Assert.Null(accessible.FocusAdorner);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AClasslessAccessibleButton_IsTemplatedExactlyLikeAStockButton(string variantName)
        {
            // The other half of criterion 6, and the one worth reading carefully: `BaseButton` is a
            // *keyed* ControlTheme reached only through another theme's BasedOn — nothing in the app
            // declares an implicit `{x:Type Button}` theme — so a classless button is templated by
            // FluentTheme's own Button theme, looked up by style key. That lookup
            // (StyledElement.GetEffectiveTheme) is the second thing StyleKeyOverride buys, and
            // without it a classless AccessibleButton would find no theme at all, get no Template,
            // and render as nothing while every resource test stayed green.
            var variant = ToVariant(variantName);
            var stock = new Button { Content = "Home" };
            var accessible = new AccessibleButton { Content = "Home" };
            var panel = new StackPanel { Children = { stock, accessible } };

            using var host = ThemedHost.Show(panel, variant, HostWidth, HostHeight * 2);

            Assert.Null(stock.Theme);
            Assert.Null(accessible.Theme);

            Assert.NotNull(accessible.Template);
            Assert.Same(stock.Template, accessible.Template);
            Assert.True(accessible.Bounds.Width > 0, "The classless subclass never got laid out.");
            Assert.Equal(stock.Bounds.Size, accessible.Bounds.Size);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheAccessibleToggleButton_ResolvesTheSettingSwitchThemeAStockToggleWould(string variantName)
        {
            // The app's one toggle wears `settingSwitch`, and its bridge is written
            // `ToggleButton.settingSwitch` — the same exact-type selector, so the same style key
            // has to be reported for typeof(ToggleButton).
            var variant = ToVariant(variantName);
            var stock = new ToggleButton { Classes = { "settingSwitch" } };
            var accessible = new AccessibleToggleButton { Classes = { "settingSwitch" } };
            var panel = new StackPanel { Children = { stock, accessible } };

            using var host = ThemedHost.Show(panel, variant, HostWidth, HostHeight * 2);

            Assert.Same(DesignTokens.Resolve("SettingSwitch", variant), accessible.Theme);
            Assert.Same(stock.Theme, accessible.Theme);
            Assert.NotNull(accessible.Template);
            Assert.Same(stock.Template, accessible.Template);
        }

        [AvaloniaFact]
        public void NoAuthoredView_StillWritesABareButtonElement()
        {
            // Criterion 5, in the idiom of the other markup gates (no hex literal, no `/template/`
            // selector): the swap is only a fix while it stays complete, and one `<Button>` added
            // later is one screen that can still kill the app under VoiceOver. Read from the source
            // because the app compiles its XAML away, and with comments stripped because these
            // files quote the markup they explain.
            var failures = new List<string>();
            var authored = 0;

            foreach (var (path, xaml) in AuthoredXaml.Files())
            {
                var body = AuthoredXaml.WithoutComments(xaml);

                foreach (Match match in BareButtonElementPattern().Matches(body))
                {
                    failures.Add($"{path}: {match.Value} — author controls:AccessibleButton instead");
                }

                authored += AccessibleButtonElementPattern().Matches(body).Count;
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

            // A scan that matched nothing anywhere would pass for the wrong reason.
            Assert.True(
                authored >= AuthoredButtonFloor,
                $"Only {authored} accessible buttons were found; the scan is not reading the app's markup.");
        }

        [AvaloniaFact]
        public void TheBareButtonScan_FindsAPlantedElementAndSparesEveryLegitimateOne()
        {
            // The scan's own claims, both directions. Three shapes look like a bare button and are
            // not one: the `controls:` element itself, its `.Flyout` property element, and the word
            // Button inside an attribute value.
            const string planted = """
                <StackPanel>
                  <Button Classes="secondary" Content="Save" />
                  <ToggleButton />
                  <controls:AccessibleButton x:Name="ClearButton" Classes="primaryAction">
                    <controls:AccessibleButton.Flyout>
                      <Flyout />
                    </controls:AccessibleButton.Flyout>
                  </controls:AccessibleButton>
                  <controls:AccessibleToggleButton Classes="settingSwitch" />
                </StackPanel>
                """;

            Assert.Equal(
                new[] { "<Button", "<ToggleButton" },
                BareButtonElementPattern().Matches(planted).Select(match => match.Value));

            Assert.Equal(
                new[] { "<controls:AccessibleButton", "<controls:AccessibleToggleButton" },
                AccessibleButtonElementPattern().Matches(planted).Select(match => match.Value));
        }

        /// <summary>
        /// An element whose local name is exactly <c>Button</c> or <c>ToggleButton</c>. The
        /// lookahead is what keeps <c>&lt;Button.Flyout&gt;</c> — a property element, not an
        /// instantiation — and any longer name out; a prefixed <c>&lt;controls:Accessible…&gt;</c>
        /// never starts with either word.
        /// </summary>
        [GeneratedRegex(@"<(?:Button|ToggleButton)(?=[\s/>])")]
        private static partial Regex BareButtonElementPattern();

        /// <summary>The guarded form: a prefixed <c>AccessibleButton</c>/<c>AccessibleToggleButton</c> element.</summary>
        [GeneratedRegex(@"<\w+:Accessible(?:Toggle)?Button(?=[\s/>])")]
        private static partial Regex AccessibleButtonElementPattern();

        /// <summary>
        /// <c>accessibilityPerformPress</c>, in managed code. It asks for each provider in turn and
        /// uses the first one it is handed, which is why a <c>null</c> answer is a clean no-op and
        /// why nulling only the first type would move the crash rather than remove it.
        /// </summary>
        private static void PerformPress(AutomationPeer peer)
        {
            if (peer.GetProvider<IInvokeProvider>() is { } invoke)
            {
                invoke.Invoke();
            }
            else if (peer.GetProvider<IExpandCollapseProvider>() is { } expandCollapse)
            {
                expandCollapse.Expand();
            }
            else if (peer.GetProvider<IToggleProvider>() is { } toggle)
            {
                toggle.Toggle();
            }
        }

        private static ThemeVariant ToVariant(string variantName)
        {
            return variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>A command that only records that it ran, so a press can be counted.</summary>
        private sealed class CountingCommand : ICommand
        {
            public int Executions { get; private set; }

            /// <summary>Declared with empty accessors: this command's answer never changes.</summary>
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                Executions++;
            }
        }
    }
}
