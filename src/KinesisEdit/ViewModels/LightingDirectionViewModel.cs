using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One entry of the lighting tab's direction control (specs/07-lighting.md §3). Which
    /// directions a mode offers on a device is <see cref="LightingAvailability"/>'s answer — it
    /// is device-aware, so Fireball correctly offers nothing on the Freestyle Edge RGB even
    /// though its file line carries a direction token.
    /// </summary>
    public sealed class LightingDirectionViewModel : ViewModelBase
    {
        /// <summary>Rebound's replacement caption for <see cref="LightingDirection.Left"/> (§3).</summary>
        public const string HorizontalCaption = "Horizontal";

        /// <summary>Rebound's replacement caption for <see cref="LightingDirection.Up"/> (§3).</summary>
        public const string VerticalCaption = "Vertical";

        /// <summary>
        /// The direction entries <paramref name="mode"/> offers on <paramref name="deviceId"/>,
        /// empty when it has no direction panel there. <b>Rebound is relabelled</b>: §3 replaces
        /// its four arrows with "Horizontal"/"Vertical" buttons mapping to left/up.
        /// </summary>
        public static IReadOnlyList<LightingDirectionViewModel> CreateAll(DeviceId deviceId, LightingMode mode)
        {
            var directions = LightingAvailability.GetKeyBacklightDirections(deviceId, mode);

            if (directions.Count == 0)
            {
                return [];
            }

            var entries = new List<LightingDirectionViewModel>(directions.Count);

            foreach (var direction in directions)
            {
                entries.Add(new LightingDirectionViewModel(direction, GetCaption(direction, mode)));
            }

            return entries;
        }

        /// <summary>The caption of <paramref name="direction"/> in <paramref name="mode"/>.</summary>
        public static string GetCaption(LightingDirection direction, LightingMode mode)
        {
            if (mode == LightingMode.Rebound)
            {
                return direction == LightingDirection.Up ? VerticalCaption : HorizontalCaption;
            }

            return direction switch
            {
                LightingDirection.Down => "Down",
                LightingDirection.Up => "Up",
                LightingDirection.Right => "Right",
                _ => "Left"
            };
        }

        /// <summary>The direction this entry writes.</summary>
        public LightingDirection Direction { get; }

        /// <summary>The entry's caption.</summary>
        public string Caption { get; }

        /// <summary>Whether this is the layer's current direction.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Creates one direction entry.</summary>
        public LightingDirectionViewModel(LightingDirection direction, string caption)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Direction = direction;
            Caption = caption;
        }
    }
}
