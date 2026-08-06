using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Controls
{
    /// <summary>
    /// The keyboard picture as a <b>surface</b>: what it decides on behalf of the caps it builds,
    /// as opposed to what it reads off the layer it is given.
    /// <para>
    /// There is exactly one such decision — <see cref="KeyboardView.ShowsLedStrips"/> — and it
    /// exists because the editor's Keys tab and the Lighting tab render the <i>same</i>
    /// <see cref="KeyboardLayerViewModel"/> instance (<c>KeyboardEditorViewModel.Apply</c> hands
    /// its <c>Layers</c> straight to <c>LightingTabViewModel.Attach</c>). No state on the layer or
    /// on a cap could ever separate the two pictures, so this is asserted at the composition and
    /// not in a view-model test.
    /// </para>
    /// </summary>
    public class KeyboardViewTests
    {
        private const double HostWidth = 900;

        private const double HostHeight = 400;

        [AvaloniaFact]
        public void APicture_DrawsNoLedStripsUntilItAsksForThem()
        {
            var board = CreateBoard();

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.False(board.ShowsLedStrips, "The LED row is opt-in; the Keys tab never asks.");
            Assert.All(CapsOf(board), cap => Assert.False(cap.ShowsLedStrip));
        }

        [AvaloniaFact]
        public void APictureThatAsksForLedStrips_HandsThatDownToEveryCapItBuilds()
        {
            var board = CreateBoard();

            board.ShowsLedStrips = true;

            using var host = ThemedHost.Show(board, ThemeVariant.Dark, HostWidth, HostHeight);

            var caps = CapsOf(board);

            Assert.NotEmpty(caps);
            Assert.All(caps, cap => Assert.True(cap.ShowsLedStrip));
        }

        [AvaloniaFact]
        public void TwoPicturesOfTheSameLayer_DisagreeAboutTheLedRow()
        {
            // The whole reason the flag is on the picture. These are the editor's two tabs: one
            // layer view model, one set of cap view models, two pictures — and the LED row is the
            // one thing they must not agree about.
            var layer = CreateLayer();
            var keys = new KeyboardView { DataContext = layer };
            var lighting = new KeyboardView { DataContext = layer, ShowsLedStrips = true };
            var panel = new StackPanel { Children = { keys, lighting } };

            using var host = ThemedHost.Show(panel, ThemeVariant.Dark, HostWidth, HostHeight);

            var keysCap = CapsOf(keys)[0];
            var lightingCap = CapsOf(lighting)[0];

            Assert.Same(keysCap.DataContext, lightingCap.DataContext);
            Assert.False(keysCap.ShowsLedStrip);
            Assert.True(lightingCap.ShowsLedStrip);
        }

        private static KeyboardView CreateBoard()
        {
            return new KeyboardView { DataContext = CreateLayer() };
        }

        private static KeyboardLayerViewModel CreateLayer()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            return KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];
        }

        private static IReadOnlyList<KeyCapView> CapsOf(KeyboardView board)
        {
            return board.GetVisualDescendants().OfType<KeyCapView>().ToArray();
        }
    }
}
