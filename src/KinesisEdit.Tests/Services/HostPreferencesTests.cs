using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The host-preferences model: its defaults, its value equality (which is what makes a no-op
    /// write detectable), and the one range rule <see cref="WindowGeometry"/> owns.
    /// </summary>
    public class HostPreferencesTests
    {
        [Fact]
        public void Default_FollowsTheSystem_ForBothThemeAndMotion()
        {
            // "Follow the OS" is what the app did before there was a choice, so a fresh install
            // must be indistinguishable from the app as it shipped.
            Assert.Equal(AppThemePreference.FollowSystem, HostPreferences.Default.Theme);
            Assert.Equal(MotionPreference.FollowSystem, HostPreferences.Default.Motion);
        }

        [Fact]
        public void Default_StoresNoWindowGeometry()
        {
            Assert.Null(HostPreferences.Default.Window);
        }

        [Fact]
        public void Default_StoresNoInspectorRailWidth()
        {
            // Null is "nothing stored", which the editor reads as the authored width — the rail as
            // it was before it could be dragged at all.
            Assert.Null(HostPreferences.Default.InspectorRailWidth);
        }

        [Fact]
        public void NewInstance_MatchesDefault()
        {
            Assert.Equal(HostPreferences.Default, new HostPreferences());
        }

        [Fact]
        public void FollowSystem_IsTheZeroValue_OfBothEnums()
        {
            // The zero value is what an unset field, a default(T) and a fresh record all land on;
            // it has to be the one that changes nothing.
            Assert.Equal(0, (int)AppThemePreference.FollowSystem);
            Assert.Equal(0, (int)MotionPreference.FollowSystem);
        }

        [Fact]
        public void Equality_IsByValue_IncludingTheWindow()
        {
            var geometry = new WindowGeometry(1000, 680, 12, 34, false);
            var first = new HostPreferences { Theme = AppThemePreference.Dark, Window = geometry };
            var second = new HostPreferences
            {
                Theme = AppThemePreference.Dark,
                Window = new WindowGeometry(1000, 680, 12, 34, false)
            };

            Assert.Equal(first, second);
        }

        [Fact]
        public void Equality_SeesAChange_InAnyField()
        {
            var baseline = new HostPreferences();

            Assert.NotEqual(baseline, baseline with { Theme = AppThemePreference.Light });
            Assert.NotEqual(baseline, baseline with { Motion = MotionPreference.AlwaysReduce });
            Assert.NotEqual(baseline, baseline with { Window = new WindowGeometry(800, 600, null, null, false) });
            Assert.NotEqual(baseline, baseline with { InspectorRailWidth = 320 });
        }

        [Theory]
        [InlineData(HostPreferences.MinimumInspectorRailWidth, HostPreferences.MinimumInspectorRailWidth)]
        [InlineData(HostPreferences.MaximumInspectorRailWidth, HostPreferences.MaximumInspectorRailWidth)]
        [InlineData(HostPreferences.DefaultInspectorRailWidth, HostPreferences.DefaultInspectorRailWidth)]
        [InlineData(320, 320)]
        [InlineData(0, HostPreferences.MinimumInspectorRailWidth)]
        [InlineData(-4000, HostPreferences.MinimumInspectorRailWidth)]
        [InlineData(9999, HostPreferences.MaximumInspectorRailWidth)]
        public void ClampInspectorRailWidth_BringsAWidthIntoTheBand(double width, double expected)
        {
            Assert.Equal(expected, HostPreferences.ClampInspectorRailWidth(width));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void ClampInspectorRailWidth_YieldsTheAuthoredWidth_ForANumberThatIsNotOne(double width)
        {
            // Math.Clamp propagates NaN, which would put a NaN on a column definition and take the
            // whole Layout tab's measure pass with it. A width that is not a number is not a
            // preference at all.
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, HostPreferences.ClampInspectorRailWidth(width));
        }

        [Fact]
        public void TheInspectorRailBand_ContainsTheAuthoredWidth()
        {
            // The default has to be a width the user could also have dragged to, or the first drag
            // would jump.
            Assert.True(HostPreferences.MinimumInspectorRailWidth < HostPreferences.DefaultInspectorRailWidth);
            Assert.True(HostPreferences.DefaultInspectorRailWidth < HostPreferences.MaximumInspectorRailWidth);
        }

        [Fact]
        public void TryCreate_ReturnsTheGeometry_ForARealWindow()
        {
            var geometry = WindowGeometry.TryCreate(1000, 680, -1440, -200, isMaximized: true);

            Assert.NotNull(geometry);
            Assert.Equal(1000, geometry.Width);
            Assert.Equal(680, geometry.Height);
            Assert.Equal(-1440, geometry.X);
            Assert.Equal(-200, geometry.Y);
            Assert.True(geometry.IsMaximized);
            Assert.True(geometry.HasPosition);
        }

        [Theory]
        [InlineData(0, 680)]
        [InlineData(-1, 680)]
        [InlineData(1000, 0)]
        [InlineData(1000, -50)]
        [InlineData(double.NaN, 680)]
        [InlineData(1000, double.PositiveInfinity)]
        [InlineData(WindowGeometry.MaximumExtent + 1, 680)]
        [InlineData(1000, WindowGeometry.MaximumExtent + 1)]
        public void TryCreate_ReturnsNull_ForASizeThatIsNotASize(double width, double height)
        {
            Assert.Null(WindowGeometry.TryCreate(width, height, null, null, false));
        }

        [Fact]
        public void TryCreate_AllowsNegativeCoordinates()
        {
            // A monitor left of or above the primary one has negative coordinates; rejecting them
            // would drag the window back onto the main screen on every launch.
            var geometry = WindowGeometry.TryCreate(900, 600, -3000, -1200, false);

            Assert.NotNull(geometry);
            Assert.Equal(-3000, geometry.X);
            Assert.Equal(-1200, geometry.Y);
        }

        [Fact]
        public void HasPosition_IsFalse_WhenEitherCoordinateIsMissing()
        {
            Assert.False(new WindowGeometry(900, 600, null, null, false).HasPosition);
            Assert.False(new WindowGeometry(900, 600, 10, null, false).HasPosition);
            Assert.False(new WindowGeometry(900, 600, null, 10, false).HasPosition);
        }
    }
}
