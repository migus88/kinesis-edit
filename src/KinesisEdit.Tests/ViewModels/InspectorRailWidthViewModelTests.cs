using Avalonia.Headless.XUnit;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The editor's one rail width, on its own (issue #124). Everything asserted here used to be
    /// asserted through <see cref="KeyboardEditorViewModel"/> and still is — that suite is the proof
    /// the delegation is faithful, and this one is the proof the object is right about itself now
    /// that a second tab shares it.
    /// </summary>
    public sealed class InspectorRailWidthViewModelTests
    {
        [AvaloniaFact]
        public void Width_WithNoStore_IsTheAuthoredWidth()
        {
            var rail = new InspectorRailWidthViewModel();

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, rail.Width);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, rail.EffectiveWidth);
            Assert.False(rail.IsWide);
        }

        [AvaloniaFact]
        public void Width_WithAStoredWidth_OpensAtIt()
        {
            var rail = new InspectorRailWidthViewModel(new FakeHostPreferencesStore(
                HostPreferences.Default with { InspectorRailWidth = 396 }));

            Assert.Equal(396, rail.Width);
        }

        [AvaloniaTheory]
        [InlineData(9999, HostPreferences.MaximumInspectorRailWidth)]
        [InlineData(10, HostPreferences.MinimumInspectorRailWidth)]
        [InlineData(double.NaN, HostPreferences.DefaultInspectorRailWidth)]
        public void Width_WithAStoredWidthTheBandRefuses_OpensClamped(double stored, double expected)
        {
            // Belt to the parser's brace: a store handed a hand-built record has never been through
            // the tolerant read at all.
            var rail = new InspectorRailWidthViewModel(new FakeHostPreferencesStore(
                HostPreferences.Default with { InspectorRailWidth = stored }));

            Assert.Equal(expected, rail.Width);
        }

        [AvaloniaTheory]
        [InlineData(396, 396)]
        [InlineData(9999, HostPreferences.MaximumInspectorRailWidth)]
        [InlineData(10, HostPreferences.MinimumInspectorRailWidth)]
        public void Width_WhenSet_IsClampedAndPersisted(double dragged, double expected)
        {
            var preferences = new FakeHostPreferencesStore();
            var rail = new InspectorRailWidthViewModel(preferences);

            rail.Width = dragged;

            Assert.Equal(expected, rail.Width);
            Assert.Equal(expected, preferences.Current.InspectorRailWidth);
        }

        [AvaloniaFact]
        public void Width_SetRepeatedly_WritesOncePerRealChange()
        {
            // The "do not debounce" rule from the other end: the store writes synchronously on every
            // real change, so a drag that reports continuously must cost one write at its commit.
            var preferences = new FakeHostPreferencesStore();
            var rail = new InspectorRailWidthViewModel(preferences);
            var announced = 0;

            rail.Width = 360;

            rail.PropertyChanged += (_, _) => announced++;

            rail.Width = 360;
            rail.Width = 360;

            Assert.Equal(1, preferences.UpdateCount);
            Assert.Equal(0, announced);
        }

        [AvaloniaFact]
        public void Width_SetToTheOnlyKeyItPersistsUnder_LeavesEveryOtherPreferenceAlone()
        {
            // The width goes on the existing inspectorRailWidth key and nowhere else — issue #124
            // shares one number between two tabs rather than adding a second preference — and the
            // write is a mutation function, so a theme written a moment earlier survives it.
            var preferences = new FakeHostPreferencesStore(HostPreferences.Default with
            {
                Theme = AppThemePreference.Dark
            });

            var rail = new InspectorRailWidthViewModel(preferences);

            rail.Width = 404;

            Assert.Equal(404, preferences.Current.InspectorRailWidth);
            Assert.Equal(AppThemePreference.Dark, preferences.Current.Theme);
        }

        [AvaloniaFact]
        public void Width_WithNoStore_StillMovesAndClamps()
        {
            // A rail built without the store — a design scene, a Lighting tab in a unit test — is
            // one that drags and forgets, never one that refuses to drag.
            var rail = new InspectorRailWidthViewModel();

            rail.Width = 9999;

            Assert.Equal(HostPreferences.MaximumInspectorRailWidth, rail.Width);
        }

        [AvaloniaFact]
        public void Width_WhenItMoves_AnnouncesTheEffectiveWidthToo()
        {
            var rail = new InspectorRailWidthViewModel();
            var announced = new List<string?>();

            rail.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            rail.Width = 400;

            Assert.Contains(nameof(InspectorRailWidthViewModel.Width), announced);
            Assert.Contains(nameof(InspectorRailWidthViewModel.EffectiveWidth), announced);
        }

        [AvaloniaFact]
        public void Width_SetOutsideTheBand_IsPushedBackAtTheBinding()
        {
            // The column is bound two-way. A width the clamp moved has to be announced even when the
            // field did not change, or the splitter would go on drawing a width the model refused.
            var rail = new InspectorRailWidthViewModel();

            rail.Width = 9999;

            var announced = new List<string?>();

            rail.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            rail.Width = 10000;

            Assert.Contains(nameof(InspectorRailWidthViewModel.Width), announced);
        }

        [AvaloniaFact]
        public void MacroRailWidth_StaysInsideTheBandTheSeamCanReach()
        {
            // The floor moved to 440 with issue #146 and to 480 with #148, and it is only a floor if
            // the seam can be dragged to both sides of it: a macro width outside the band would be a
            // width the clamp refuses, so the rail would open at one number and store another.
            // Asserted against the clamp itself rather than against the two constants, because the
            // clamp is what the stored width actually goes through.
            Assert.InRange(
                InspectorRailWidthViewModel.MacroRailWidth,
                HostPreferences.MinimumInspectorRailWidth,
                HostPreferences.MaximumInspectorRailWidth);

            Assert.Equal(
                InspectorRailWidthViewModel.MacroRailWidth,
                HostPreferences.ClampInspectorRailWidth(InspectorRailWidthViewModel.MacroRailWidth));

            // ...and "both sides" has to mean something. A ceiling a few pixels above the floor
            // would leave the seam nowhere to go and make the floor an override in all but name.
            Assert.True(
                HostPreferences.MaximumInspectorRailWidth - InspectorRailWidthViewModel.MacroRailWidth >= 80,
                "The macro floor leaves the drag seam less than 80 px of travel above it.");
        }

        [AvaloniaFact]
        public void EffectiveWidth_WhileWide_NeverGoesBelowTheMacroWidth()
        {
            // The macro entitlement honoured as a FLOOR: a rail dragged narrow still gives macro
            // editing the room its step list and its composer need.
            var rail = new InspectorRailWidthViewModel
            {
                Width = HostPreferences.MinimumInspectorRailWidth,
                IsWide = true
            };

            Assert.Equal(InspectorRailWidthViewModel.MacroRailWidth, rail.EffectiveWidth);
            Assert.Equal(HostPreferences.MinimumInspectorRailWidth, rail.Width);
        }

        [AvaloniaFact]
        public void EffectiveWidth_WhenTheUserDraggedWider_KeepsTheDraggedWidth()
        {
            // ...and it is a floor rather than an override, which is the deviation issue #119
            // recorded: a user who dragged to 500 is not yanked back to the macro width by opening
            // a macro.
            var rail = new InspectorRailWidthViewModel
            {
                Width = 500,
                IsWide = true
            };

            Assert.Equal(500, rail.EffectiveWidth);
        }

        [AvaloniaFact]
        public void EffectiveWidth_IsAnnounced_WhenTheFloorMoves()
        {
            var rail = new InspectorRailWidthViewModel();
            var announced = new List<string?>();

            rail.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            rail.IsWide = true;

            Assert.Contains(nameof(InspectorRailWidthViewModel.EffectiveWidth), announced);

            announced.Clear();

            // ...and not when it is re-asserted. IsWide is re-raised on every inspector mode switch,
            // so the guard is what stops a mode swap announcing a width that did not move.
            rail.IsWide = true;

            Assert.Empty(announced);
        }

        [AvaloniaFact]
        public void IsWide_NeverTouchesTheStoredWidth()
        {
            // The floor is a fact about the showing panel, not about the user's choice — so it is
            // never persisted, and the Lighting tab (which reads the raw Width) never sees it.
            var preferences = new FakeHostPreferencesStore();
            var rail = new InspectorRailWidthViewModel(preferences)
            {
                Width = 250,
                IsWide = true
            };

            Assert.Equal(250, rail.Width);
            Assert.Equal(250, preferences.Current.InspectorRailWidth);
            Assert.Equal(InspectorRailWidthViewModel.MacroRailWidth, rail.EffectiveWidth);
        }
    }
}
