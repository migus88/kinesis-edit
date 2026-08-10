using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// The one rule behind <see cref="AccessibleButton"/> and <see cref="AccessibleToggleButton"/>:
    /// while a control is disabled, its automation peer must hand the platform <c>null</c> for every
    /// provider interface a native "press" can arrive through.
    /// <para>
    /// <b>Why this exists at all.</b> <c>ButtonAutomationPeer.Invoke()</c> and
    /// <c>ToggleButtonAutomationPeer.Toggle()</c> both call <c>EnsureEnabled()</c>, which throws
    /// <c>ElementNotEnabledException</c> on a disabled owner. On macOS that call arrives from
    /// Objective-C (<c>AvnAutomationPeer.InvokeProvider_Invoke()</c>, reached from
    /// <c>automation.mm</c>'s <c>accessibilityPerformPress</c>), and a managed exception escaping a
    /// native-to-managed callback <b>fail-fasts the process</b> — a VoiceOver user pressing any
    /// disabled button kills the app. Upstream closed the same report (AvaloniaUI/Avalonia#20680) with
    /// PR #20687, but that fix guards <c>AvaloniaAccessHelper</c> in <c>Avalonia.Android</c> only;
    /// verified against tag 11.3.20, <c>Avalonia.Native/AvnAutomationPeer.cs</c> still reads
    /// <c>public void InvokeProvider_Invoke() =&gt; InvokeProvider.Invoke();</c> with no guard. A
    /// package bump does not fix this.
    /// </para>
    /// <para>
    /// <b>Why nulling providers rather than overriding the action.</b>
    /// <c>ButtonAutomationPeer.Invoke()</c> is non-virtual, so there is nothing to override on the
    /// action itself. <c>GetProviderCore</c> is the supported seam: the native side asks
    /// <c>IsInvokeProvider()</c> before it presses, that question resolves to
    /// <c>AutomationPeer.GetProvider&lt;T&gt;()</c>, and a <c>null</c> answer makes the press a clean
    /// no-op. The exception is never constructed and nothing crosses the interop boundary.
    /// </para>
    /// <para>
    /// <b>All three provider types, deliberately.</b> <c>accessibilityPerformPress</c> is an
    /// <c>if / else if / else if</c> chain over <see cref="IInvokeProvider"/>,
    /// <see cref="IExpandCollapseProvider"/> and <see cref="IToggleProvider"/>. Nulling only the first
    /// would move the crash one branch to the right rather than remove it, so the whole chain has to
    /// fall through.
    /// </para>
    /// <para>
    /// <b>Removal condition:</b> when <c>Avalonia.Native</c> guards its own callbacks, the two control
    /// types and this guard can go, and every <c>AccessibleButton</c> in the views becomes a
    /// <c>Button</c> again. The accessibility suite pins a stock <c>Button</c> still throwing, so that
    /// day announces itself as a failing test rather than as dead code nobody noticed.
    /// </para>
    /// </summary>
    internal static class DisabledPressGuard
    {
        public static bool ShouldSuppress(AutomationPeer peer, Type providerType)
        {
            if (peer.IsEnabled())
            {
                return false;
            }

            return providerType == typeof(IInvokeProvider)
                || providerType == typeof(IExpandCollapseProvider)
                || providerType == typeof(IToggleProvider);
        }
    }
}
