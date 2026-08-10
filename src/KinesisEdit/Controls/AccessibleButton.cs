using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// A <see cref="Button"/> whose automation peer refuses to be pressed while it is disabled.
    /// <b>Every button the app authors is one of these</b>; a bare <c>&lt;Button&gt;</c> in a view is a
    /// crash waiting for a VoiceOver user, and a test in the accessibility suite says so.
    /// <para>
    /// The reason the guard has to live on a control type — rather than somewhere central — is that a
    /// custom peer can only be installed through <see cref="Control.OnCreateAutomationPeer"/>.
    /// <c>Control._automationPeer</c> is private and <c>GetOrCreateAutomationPeer()</c> is internal, so
    /// there is no global hook. See <see cref="DisabledPressGuard"/> for what the peer actually does
    /// and why the crash exists.
    /// </para>
    /// <para>
    /// <b><see cref="StyleKeyOverride"/> is what makes this cost nothing in the style layer.</b>
    /// Avalonia's <c>OfType</c> selector is an exact-type match, so a <c>Button</c> subclass would
    /// normally stop matching every <c>Button.xxx</c> bridge in <c>Styles/</c> and lose its implicit
    /// <c>ControlTheme</c>. But <c>TypeNameAndClassSelector</c> matches on the <b>style key</b>
    /// (<c>StyledElement.GetStyleKey(control) ?? control.GetType()</c>) and
    /// <c>StyledElement.GetEffectiveTheme</c> looks the implicit theme up by the same key — so
    /// reporting <c>typeof(Button)</c> keeps the class bridges, <c>BaseButton</c> and FluentTheme's own
    /// Button styles all matching, unchanged. If a swapped button ever looks wrong, the bug is a
    /// missing style key, never a selector: <b>do not "fix" it in <c>Styles/</c> or
    /// <c>Themes/ControlThemes/</c></b>.
    /// </para>
    /// <para>
    /// <b>Scope: buttons only.</b> <see cref="ComboBox"/>, <c>Slider</c>, <c>TextBox</c> and
    /// <c>MenuItem</c> can all be disabled and are deliberately not covered — the app never disables an
    /// invokable one today. The next control type the app disables is a known follow-up, not a
    /// surprise.
    /// </para>
    /// </summary>
    public class AccessibleButton : Button
    {
        protected override Type StyleKeyOverride => typeof(Button);

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DisabledSafeButtonAutomationPeer(this);
        }

        /// <summary>
        /// The stock button peer with one seam changed — see <see cref="DisabledPressGuard"/>.
        /// Everything else about the peer, including the name, control type and the working
        /// <see cref="Avalonia.Automation.Provider.IInvokeProvider"/> an <i>enabled</i> button
        /// advertises, is the base peer's.
        /// </summary>
        private sealed class DisabledSafeButtonAutomationPeer : ButtonAutomationPeer
        {
            public DisabledSafeButtonAutomationPeer(Button owner)
                : base(owner)
            {
            }

            protected override object? GetProviderCore(Type providerType)
            {
                if (DisabledPressGuard.ShouldSuppress(this, providerType))
                {
                    return null;
                }

                return base.GetProviderCore(providerType);
            }
        }
    }
}
