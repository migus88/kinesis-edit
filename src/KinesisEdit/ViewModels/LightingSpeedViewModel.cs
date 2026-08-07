using System.Globalization;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The lighting tab's speed control: the nine bars of design mockup 2f and the mono
    /// <c>Speed 6 / 9</c> readout beside them. It holds no rule of its own — the bounds are
    /// <see cref="LayerLightingState.MinimumSpeed"/>/<see cref="LayerLightingState.MaximumSpeed"/>
    /// and the value is <see cref="LightingTabViewModel.Speed"/>'s, which is what writes the model.
    /// <para>
    /// Nine bars rather than a slider is the design's call and a legibility one: the file's speed
    /// is nine discrete values (§2.1), a slider would suggest a continuum, and the readout is a
    /// value that exists verbatim in a config file — hence mono.
    /// </para>
    /// </summary>
    public sealed class LightingSpeedViewModel : ViewModelBase
    {
        /// <summary>The readout's separator: "Speed 6 / 9".</summary>
        public const string ReadoutPrefix = "Speed ";

        /// <summary>The nine bars, slowest first.</summary>
        public IReadOnlyList<LightingSpeedSegmentViewModel> Segments { get; }

        /// <summary>The chosen speed, always inside the file's own 1..9 bounds.</summary>
        public int Speed
        {
            get => _speed;
            private set
            {
                if (SetProperty(ref _speed, value))
                {
                    OnPropertyChanged(nameof(Readout));

                    RefreshSegments();
                }
            }
        }

        /// <summary>The mono readout — <c>Speed 6 / 9</c> (mockup 2f, verbatim).</summary>
        public string Readout => string.Create(
            CultureInfo.InvariantCulture,
            $"{ReadoutPrefix}{_speed} / {LayerLightingState.MaximumSpeed}");

        private int _speed = LayerLightingState.DefaultSpeed;

        /// <summary>Builds the nine bars at the file's default speed.</summary>
        public LightingSpeedViewModel()
        {
            var segments = new List<LightingSpeedSegmentViewModel>(
                LayerLightingState.MaximumSpeed - LayerLightingState.MinimumSpeed + 1);

            for (var speed = LayerLightingState.MinimumSpeed; speed <= LayerLightingState.MaximumSpeed; speed++)
            {
                segments.Add(new LightingSpeedSegmentViewModel(speed));
            }

            Segments = segments;

            RefreshSegments();
        }

        /// <summary>
        /// Shows <paramref name="speed"/>, clamped to the file's bounds. It is a readout only: the
        /// write into the model is <see cref="LightingTabViewModel.Speed"/>'s, so this cannot
        /// become a second path to the same field.
        /// </summary>
        public void Show(int speed)
        {
            Speed = Math.Clamp(speed, LayerLightingState.MinimumSpeed, LayerLightingState.MaximumSpeed);
        }

        private void RefreshSegments()
        {
            foreach (var segment in Segments)
            {
                segment.IsFilled = segment.Speed <= _speed;
            }
        }
    }
}
