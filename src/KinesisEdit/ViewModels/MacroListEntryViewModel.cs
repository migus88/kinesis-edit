using System.Globalization;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the profile's macro list — the Advantage360's "macro repository" of
    /// specs/10-apps-and-ui.md, shown on every device because it is the only place a user can see
    /// every macro of the profile at once. Each row renders its macro with
    /// <see cref="MacroKeystrokeRenderer"/> <b>under its layer prefix</b>
    /// (<see cref="MacroKeystrokeRenderer.LayerPrefixFor"/>), so what the list shows is the line
    /// the file will carry — a bottom-layer macro on the Gen1 family reads
    /// <c>fn {q}&gt;{s5}{x1}{a}</c>, exactly as <c>LayoutFileSerializer</c> writes it
    /// (specs/06-macros.md §3 step 1, 04 §3.1).
    /// </summary>
    public sealed class MacroListEntryViewModel : ViewModelBase
    {
        /// <summary>Separates the layer and trigger parts of <see cref="TriggerCaption"/>.</summary>
        public const string TriggerCaptionSeparator = " · ";

        /// <summary>What a row shows for a flat-list macro whose trigger is not assigned yet (06 §1).</summary>
        public const string UnassignedTriggerCaption = "Unassigned";

        /// <summary>
        /// Every macro of <paramref name="layout"/> in a stable order: the Gen2 flat list first,
        /// then every populated key slot in layer / key / slot order — the traversal order of
        /// <see cref="KeyboardLayout.EnumerateMacros"/>, walked here rather than reused because a
        /// row needs the layer, key and slot the macro was found in, which the enumeration drops.
        /// </summary>
        public static IReadOnlyList<MacroListEntryViewModel> BuildAll(KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var entries = new List<MacroListEntryViewModel>();

            foreach (var macro in layout.Macros)
            {
                var layer = layout.FindLayer(macro.LayerIndex);

                entries.Add(new MacroListEntryViewModel(
                    macro,
                    layer,
                    layer?.FindByTriggerKeyCode(macro.TriggerKey),
                    slot: 0,
                    layout.Dialect));
            }

            foreach (var layer in layout.Layers)
            {
                foreach (var key in layer.Keys)
                {
                    for (var slot = 1; slot <= key.Macros.Count; slot++)
                    {
                        var macro = key.GetMacro(slot);

                        if (macro is not null)
                        {
                            entries.Add(new MacroListEntryViewModel(macro, layer, key, slot, layout.Dialect));
                        }
                    }
                }
            }

            return entries;
        }

        /// <summary>The macro this row shows.</summary>
        public Macro Macro { get; }

        /// <summary>The layer the macro belongs to, or null when it names none (06 §1).</summary>
        public KeyboardLayer? Layer { get; }

        /// <summary>The key the macro is triggered by, or null when it names no trigger yet (06 §1).</summary>
        public KeyboardKey? Key { get; }

        /// <summary>The key slot the macro was found in, or 0 for a flat-list macro (06 §1).</summary>
        public int Slot { get; }

        /// <summary>Where the macro sits: "Top · A (1)" — layer, trigger key, and slot where there is one.</summary>
        public string TriggerCaption { get; }

        /// <summary>The macro as the file will carry it, layer prefix included (06 §3).</summary>
        public string Text { get; }

        /// <summary>Whether this is the row the panel is editing.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Whether this macro is over one of the device's budgets — the row's amber dot. Pushed in
        /// by <see cref="MacroPanelViewModel.Advisories"/> whenever the rows are rebuilt; a budget
        /// is <b>reported, never refused</b>, so the dot is the whole consequence.
        /// </summary>
        public bool HasAdvisory
        {
            get => _hasAdvisory;
            set => SetProperty(ref _hasAdvisory, value);
        }

        private bool _isSelected;
        private bool _hasAdvisory;

        /// <summary>Creates one row over a macro found on <paramref name="key"/> of <paramref name="layer"/>.</summary>
        public MacroListEntryViewModel(
            Macro macro,
            KeyboardLayer? layer,
            KeyboardKey? key,
            int slot,
            TokenDialect dialect)
        {
            Macro = macro ?? throw new ArgumentNullException(nameof(macro));

            Layer = layer;
            Key = key;
            Slot = slot;
            Text = MacroKeystrokeRenderer.Render(
                macro,
                dialect,
                MacroKeystrokeRenderer.LayerPrefixFor(dialect, layer?.Index ?? 0));
            TriggerCaption = BuildTriggerCaption(layer, key, slot, dialect);
        }

        private static string BuildTriggerCaption(KeyboardLayer? layer, KeyboardKey? key, int slot, TokenDialect dialect)
        {
            var trigger = key is null
                ? UnassignedTriggerCaption
                : KeyCaption.For(key.TriggerKey, dialect, KeyCaption.IsMacOs);

            if (slot > 0)
            {
                trigger += " (" + slot.ToString(CultureInfo.InvariantCulture) + ")";
            }

            return layer is null ? trigger : LayerCaptions.ForLayer(layer, dialect) + TriggerCaptionSeparator + trigger;
        }
    }
}
