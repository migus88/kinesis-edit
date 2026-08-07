using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One arrow of the lighting tab's direction row (specs/07-lighting.md §3, design mockup 2f).
    /// <para>
    /// <b>All four are always present.</b> Which ones a mode can actually use is
    /// <see cref="LightingModeParameters.Directions"/>'s answer — device-aware, so Fireball offers
    /// none on the Freestyle Edge RGB even though its file line carries a direction token — and an
    /// arrow the mode cannot use stays in place with <see cref="IsAvailable"/> false, struck
    /// through by its control theme. That is 2f overriding the app's own "absent, never disabled"
    /// law, verbatim and for a stated reason: "Directions a mode can't use stay in place, struck
    /// through — the row never changes shape as you move down the list." The rail is a column of
    /// fourteen rows the user scrubs, and a control that changed shape under the pointer as the
    /// selection moved would be worse than a struck arrow.
    /// </para>
    /// </summary>
    public sealed class LightingDirectionViewModel : ViewModelBase
    {
        /// <summary>Rebound's replacement caption for <see cref="LightingDirection.Left"/> (§3).</summary>
        public const string HorizontalCaption = "Horizontal";

        /// <summary>Rebound's replacement caption for <see cref="LightingDirection.Up"/> (§3).</summary>
        public const string VerticalCaption = "Vertical";

        /// <summary>
        /// The row's four arrows, in <see cref="LightingDirection"/> order — down, left, up, right,
        /// which is also the order <see cref="LightingModeCatalog"/> lists a mode's own set in, so
        /// an available subset never appears shuffled against the row it sits in.
        /// </summary>
        public static IReadOnlyList<LightingDirection> Order { get; } =
        [
            LightingDirection.Down,
            LightingDirection.Left,
            LightingDirection.Up,
            LightingDirection.Right
        ];

        /// <summary>
        /// The four arrows for <paramref name="parameters"/>' mode, each marked available or not.
        /// </summary>
        public static IReadOnlyList<LightingDirectionViewModel> CreateAll(LightingModeParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            var entries = new List<LightingDirectionViewModel>(Order.Count);

            foreach (var direction in Order)
            {
                entries.Add(new LightingDirectionViewModel(
                    direction,
                    GetCaption(direction, parameters.Mode),
                    parameters.Directions.Contains(direction)));
            }

            return entries;
        }

        /// <summary>
        /// The caption of <paramref name="direction"/> in <paramref name="mode"/>. <b>Rebound is
        /// relabelled</b>: §3 replaces its arrows with "Horizontal"/"Vertical" buttons mapping to
        /// left/up. Only the two it offers are renamed — the two it does not are struck through
        /// under their own names, because calling an unusable arrow "Horizontal" would say the mode
        /// has two horizontals.
        /// </summary>
        public static string GetCaption(LightingDirection direction, LightingMode mode)
        {
            if (mode == LightingMode.Rebound)
            {
                if (direction == LightingDirection.Left)
                {
                    return HorizontalCaption;
                }

                if (direction == LightingDirection.Up)
                {
                    return VerticalCaption;
                }
            }

            return direction switch
            {
                LightingDirection.Down => "Down",
                LightingDirection.Up => "Up",
                LightingDirection.Right => "Right",
                _ => "Left"
            };
        }

        /// <summary>The direction this arrow writes.</summary>
        public LightingDirection Direction { get; }

        /// <summary>The arrow's caption.</summary>
        public string Caption { get; }

        /// <summary>
        /// Whether the selected mode runs in this direction. False draws the arrow struck through
        /// in place, and selecting it is a no-op rather than a refused command — the control is not
        /// hit-testable, so the guard is the last line rather than the only one.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>Whether this is the layer's current direction.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Creates one direction arrow.</summary>
        public LightingDirectionViewModel(LightingDirection direction, string caption, bool isAvailable)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Direction = direction;
            Caption = caption;
            IsAvailable = isAvailable;
        }
    }
}
