namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One bar of the lighting tab's speed control (design mockup 2f: "Speed 6 / 9", drawn as
    /// segmented bars). It carries the speed it sets and whether it is filled — a bar is filled
    /// when its own value is at or below the chosen speed, which is what makes nine bars read as a
    /// level rather than as nine choices.
    /// </summary>
    public sealed class LightingSpeedSegmentViewModel : ViewModelBase
    {
        /// <summary>The speed this bar sets (1..9, specs/07-lighting.md §2.1).</summary>
        public int Speed { get; }

        /// <summary>Whether the bar is drawn filled, i.e. whether the speed reaches it.</summary>
        public bool IsFilled
        {
            get => _isFilled;
            set => SetProperty(ref _isFilled, value);
        }

        private bool _isFilled;

        /// <summary>Creates one bar.</summary>
        public LightingSpeedSegmentViewModel(int speed)
        {
            Speed = speed;
        }
    }
}
