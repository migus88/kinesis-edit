using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// <see cref="AccessibleButton"/>'s counterpart for <see cref="ToggleButton"/>:
    /// <c>ToggleButtonAutomationPeer.Toggle()</c> calls the same <c>EnsureEnabled()</c> on the same
    /// native press path, so a disabled toggle is the same process-killing crash. See
    /// <see cref="DisabledPressGuard"/> for the mechanism.
    /// <para>
    /// The app has exactly one <see cref="ToggleButton"/> — the settings rows' <c>settingSwitch</c> —
    /// and covering it closes the last reachable crash vector for a handful of lines.
    /// </para>
    /// <para>
    /// <b><see cref="StyleKeyOverride"/> carries the same load as on <see cref="AccessibleButton"/>:</b>
    /// reporting <c>typeof(ToggleButton)</c> is what keeps the <c>ToggleButton.settingSwitch</c> bridge
    /// — and with it the <c>SettingSwitch</c> <c>ControlTheme</c> — matching a subclass. Nothing in
    /// <c>Styles/</c> or <c>Themes/ControlThemes/</c> changes for this type.
    /// </para>
    /// </summary>
    public class AccessibleToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DisabledSafeToggleButtonAutomationPeer(this);
        }

        /// <summary>
        /// The stock toggle peer with one seam changed — see <see cref="DisabledPressGuard"/>. An
        /// enabled toggle still advertises the base peer's
        /// <see cref="Avalonia.Automation.Provider.IToggleProvider"/> and toggles exactly as before.
        /// </summary>
        private sealed class DisabledSafeToggleButtonAutomationPeer : ToggleButtonAutomationPeer
        {
            public DisabledSafeToggleButtonAutomationPeer(ToggleButton owner)
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
