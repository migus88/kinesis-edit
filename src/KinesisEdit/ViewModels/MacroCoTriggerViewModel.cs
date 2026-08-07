using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One co-trigger toggle of the macro panel — the six Left/Right Shift, Ctrl and Alt buttons
    /// of specs/10-apps-and-ui.md, holding the modifier a macro's trigger must be pressed with
    /// (specs/06-macros.md §2.1, §5).
    /// <para>
    /// The toggle owns no rules: how many may be on at once is
    /// <see cref="Core.Devices.MacroCapability.MaxCoTriggersPerMacro"/> and is enforced by
    /// <see cref="MacroInspectorPanelViewModel"/>, because <see cref="Macro.AddCoTrigger"/> deliberately
    /// neither de-duplicates nor refuses (06 §5 counts populated slots, and a repeated token in a
    /// field file must survive a round trip).
    /// </para>
    /// </summary>
    public sealed class MacroCoTriggerViewModel : ViewModelBase
    {
        /// <summary>
        /// The six co-trigger buttons of the legacy macro panel, in spec order. Left/right are
        /// distinct keys in the §5.1 modifier table and capture reports them apart, so both
        /// spellings of each modifier get their own toggle.
        /// </summary>
        public static IReadOnlyList<MacroCoTriggerViewModel> CreateAll(TokenDialect dialect)
        {
            var modifiers = new[]
            {
                MacroModifiers.LeftShift,
                MacroModifiers.RightShift,
                MacroModifiers.LeftControl,
                MacroModifiers.RightControl,
                MacroModifiers.LeftAlt,
                MacroModifiers.RightAlt
            };

            var toggles = new List<MacroCoTriggerViewModel>(modifiers.Length);

            foreach (var modifier in modifiers)
            {
                var key = KeyRegistry.FindByCode(MacroModifierCodes.GetKeyCode(modifier));

                if (key is null)
                {
                    continue;
                }

                toggles.Add(new MacroCoTriggerViewModel(key, dialect));
            }

            return toggles;
        }

        /// <summary>The modifier key this toggle adds as a co-trigger (§5.1).</summary>
        public KeyDefinition Key { get; }

        /// <summary>What the button reads, by the shared <see cref="KeyCaption"/> rule.</summary>
        public string Caption { get; }

        /// <summary>Whether the macro under edit currently carries this co-trigger.</summary>
        public bool IsOn
        {
            get => _isOn;
            internal set => SetProperty(ref _isOn, value);
        }

        private bool _isOn;

        /// <summary>Creates the toggle for one modifier key.</summary>
        public MacroCoTriggerViewModel(KeyDefinition key, TokenDialect dialect)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));

            Caption = KeyCaption.For(key, dialect, KeyCaption.IsMacOs);
        }
    }
}
