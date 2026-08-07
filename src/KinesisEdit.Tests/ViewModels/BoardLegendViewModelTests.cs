using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The legend row's own view model (mockups 1e/2a): five layer-scoped counts, the copy prompt,
    /// and the two commands its buttons run. Everything it knows is pushed in — the counts by
    /// <see cref="BoardLegendViewModel.Refresh"/>, the commands by the editor — so these tests are
    /// about the projection and nothing else.
    /// </summary>
    public class BoardLegendViewModelTests
    {
        [AvaloniaFact]
        public void Refresh_TakesTheFiveCountsFromTheLayerItIsGiven()
        {
            var layer = CreateLayer();

            layer.Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));
            layer.RefreshFromModel();
            layer.AdvisoryCount = 3;

            var legend = CreateLegend();

            legend.Refresh(layer, string.Empty);

            Assert.Equal(layer.RemappedCount, legend.RemappedCount);
            Assert.Equal(layer.MacroCount, legend.MacroCount);
            Assert.Equal(layer.TapAndHoldCount, legend.TapAndHoldCount);
            Assert.Equal(layer.LockedCount, legend.LockedCount);
            Assert.Equal(3, legend.AdvisoryCount);
            Assert.Equal(1, legend.RemappedCount);
        }

        [AvaloniaFact]
        public void Refresh_WithNoLayer_ZeroesEveryCount()
        {
            var legend = CreateLegend();

            legend.Refresh(CreateLayer(), string.Empty);
            legend.Refresh(null, string.Empty);

            Assert.Equal(0, legend.RemappedCount);
            Assert.Equal(0, legend.MacroCount);
            Assert.Equal(0, legend.TapAndHoldCount);
            Assert.Equal(0, legend.AdvisoryCount);
            Assert.Equal(0, legend.LockedCount);
        }

        [AvaloniaFact]
        public void Refresh_IsLayerScoped_AndFollowsTheLayerItIsHanded()
        {
            // The row is about the layer on screen: handed the other one, it counts the other one.
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layers = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null);

            layers[1].Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));
            layers[1].RefreshFromModel();

            var legend = CreateLegend();

            legend.Refresh(layers[0], string.Empty);

            Assert.Equal(0, legend.RemappedCount);

            legend.Refresh(layers[1], string.Empty);

            Assert.Equal(1, legend.RemappedCount);
        }

        [AvaloniaFact]
        public void Refresh_RaisesNotificationForEveryCountThatMoved()
        {
            // The row is bound, and Core announces nothing: a count that changed without notifying
            // would leave the mockup's "live counts" frozen at whatever the load produced.
            var layer = CreateLayer();
            var legend = CreateLegend();

            legend.Refresh(layer, string.Empty);

            var changed = new List<string>();

            legend.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

            layer.Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));
            layer.RefreshFromModel();
            layer.AdvisoryCount = 2;

            legend.Refresh(layer, string.Empty);

            Assert.Contains(nameof(BoardLegendViewModel.RemappedCount), changed);
            Assert.Contains(nameof(BoardLegendViewModel.AdvisoryCount), changed);
        }

        [AvaloniaFact]
        public void LockedCount_CountsThePositionsThatCanNeverBeRemapped()
        {
            var layer = CreateLayer();
            var legend = CreateLegend();

            legend.Refresh(layer, string.Empty);

            Assert.Equal(layer.Keys.Count(key => !key.CanEdit), legend.LockedCount);
        }

        [AvaloniaFact]
        public void CopyPrompt_IsEmptyUntilACopyIsArmed()
        {
            var legend = CreateLegend();

            Assert.Equal(string.Empty, legend.CopyPrompt);
            Assert.False(legend.IsPickingCopyTarget);

            legend.Refresh(CreateLayer(), BoardLegendViewModel.CopyTargetPrompt);

            Assert.Equal(BoardLegendViewModel.CopyTargetPrompt, legend.CopyPrompt);
            Assert.True(legend.IsPickingCopyTarget);

            legend.Refresh(CreateLayer(), string.Empty);

            Assert.False(legend.IsPickingCopyTarget);
        }

        [AvaloniaFact]
        public void CopyPrompt_RaisesTheDerivedFlagWithIt()
        {
            var legend = CreateLegend();
            var changed = new List<string>();

            legend.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

            legend.Refresh(null, BoardLegendViewModel.CopyTargetPrompt);

            Assert.Contains(nameof(BoardLegendViewModel.CopyPrompt), changed);
            Assert.Contains(nameof(BoardLegendViewModel.IsPickingCopyTarget), changed);
        }

        [AvaloniaFact]
        public void TheRow_RunsTheCommandsItWasGiven_AndReportsTheirAvailability()
        {
            var copies = 0;
            var resets = 0;
            var canRun = false;

            var copy = new RelayCommand(() => copies++, () => canRun);
            var reset = new RelayCommand(() => resets++, () => canRun);
            var legend = new BoardLegendViewModel(copy, reset);

            Assert.Same(copy, legend.CopyKeyCommand);
            Assert.Same(reset, legend.ResetLayerCommand);
            Assert.False(legend.CopyKeyCommand.CanExecute(null));
            Assert.False(legend.ResetLayerCommand.CanExecute(null));

            canRun = true;

            copy.NotifyCanExecuteChanged();
            reset.NotifyCanExecuteChanged();

            Assert.True(legend.CopyKeyCommand.CanExecute(null));
            Assert.True(legend.ResetLayerCommand.CanExecute(null));

            legend.CopyKeyCommand.Execute(null);
            legend.ResetLayerCommand.Execute(null);

            Assert.Equal(1, copies);
            Assert.Equal(1, resets);
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingCommand()
        {
            var command = new RelayCommand(() => { });

            Assert.Throws<ArgumentNullException>(() => new BoardLegendViewModel(null!, command));
            Assert.Throws<ArgumentNullException>(() => new BoardLegendViewModel(command, null!));
        }

        [AvaloniaFact]
        public void TheCaptions_AreTheMockupsOwn()
        {
            // Mockup 2a: "Remapped 3", "Macro 2", "Tap-and-hold 11", "Advisory 3", "Locked 1",
            // plus the row's two actions. `Reset layer` is sentence case, a deliberate deviation
            // from spec 10's verbatim `Reset Layer`.
            Assert.Equal("Remapped", BoardLegendViewModel.RemappedCaption);
            Assert.Equal("Macro", BoardLegendViewModel.MacroCaption);
            Assert.Equal("Tap-and-hold", BoardLegendViewModel.TapAndHoldCaption);
            Assert.Equal("Advisory", BoardLegendViewModel.AdvisoryCaption);
            Assert.Equal("Locked", BoardLegendViewModel.LockedCaption);
            Assert.Equal("Copy key…", BoardLegendViewModel.CopyKeyCaption);
            Assert.Equal("Reset layer", BoardLegendViewModel.ResetLayerCaption);
            Assert.Equal("Pick a target key · Esc to cancel", BoardLegendViewModel.CopyTargetPrompt);
        }

        private static BoardLegendViewModel CreateLegend()
        {
            return new BoardLegendViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }));
        }

        private static KeyboardLayerViewModel CreateLayer()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            return KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];
        }
    }
}
