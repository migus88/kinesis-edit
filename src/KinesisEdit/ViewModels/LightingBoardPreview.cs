using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The lighting board's animation: the elapsed clock, the sampler, and the push onto the
    /// layer's caps. Design mockup 2f — "the board animates the selected mode at its real speed and
    /// direction" — is this class plus the frame timer the view runs.
    /// <para>
    /// It owns no effect and no timing curve: <see cref="LightingEffectSampler"/> answers what the
    /// board looks like at time <c>t</c> and <see cref="LightingSpeedScale"/> turns the file's
    /// speed into seconds, both in Core ([lighting.md] § "Preview"). What lives here is the clock,
    /// the normalised key list, and the rule that a frozen board still shows a frame.
    /// </para>
    /// </summary>
    public sealed class LightingBoardPreview
    {
        private readonly LightingEffectSampler _sampler = new();
        private IReadOnlyList<LightingPreviewKey> _previewKeys = [];
        private KeyboardLayerViewModel? _board;
        private LayerLightingState? _state;
        private double _elapsedSeconds;

        /// <summary>
        /// Points the preview at another layer: the board it paints, and the lighting state it
        /// samples. The clock restarts, because an effect that carried its phase across a layer
        /// switch would appear mid-stride on a board the user has just arrived at.
        /// </summary>
        public void SetLayer(KeyboardLayerViewModel? board, LayerLightingState? state)
        {
            _board = board;
            _state = state;
            _elapsedSeconds = 0.0;
            _previewKeys = board is null ? [] : BuildPreviewKeys(board);
        }

        /// <summary>
        /// Moves the clock on by <paramref name="deltaSeconds"/> and re-draws the board.
        /// <paramref name="isFrozen"/> holds the clock at zero instead — the reduce-motion case —
        /// so the board shows the <c>t = 0</c> frame of whatever mode is selected rather than
        /// nothing at all.
        /// <para>
        /// A negative or non-finite delta is ignored rather than rewinding the clock: a frame timer
        /// that has just been resumed, or a machine whose clock stepped, must not make the board
        /// jump backwards.
        /// </para>
        /// </summary>
        public void Advance(double deltaSeconds, bool isFrozen)
        {
            if (isFrozen)
            {
                _elapsedSeconds = 0.0;
            }
            else if (double.IsFinite(deltaSeconds) && deltaSeconds > 0.0)
            {
                _elapsedSeconds += deltaSeconds;
            }

            Draw();
        }

        /// <summary>
        /// Re-draws the board at the clock's current position, without moving it. Every write into
        /// the lighting model ends here, so a mode, a colour or a speed picked while the preview is
        /// frozen still shows immediately.
        /// </summary>
        public void Draw()
        {
            if (_board is null)
            {
                return;
            }

            if (_state is null)
            {
                _board.ApplyLighting(LightingEffectFrame.Empty, null);

                return;
            }

            _board.ApplyLighting(
                _sampler.Sample(_state, _previewKeys, _elapsedSeconds),
                _state.KeyColors);
        }

        /// <summary>
        /// The board's keys as Core sees them: the memory key code, and the key's <b>centre</b> in
        /// normalised board space (0..1, X right, Y down — <see cref="LightingPreviewKey"/>).
        /// <para>
        /// Built once per layer and not once per frame: it is fixed geometry, and rebuilding ~76 of
        /// these thirty times a second would be the only allocation on the preview's hot path.
        /// The centre rather than the corner, because a travelling effect that reached a 2U cap at
        /// its left edge would light it a whole cap early; and the code rather than the index,
        /// because that is what <see cref="LayerLightingState.KeyColors"/> and
        /// <see cref="LightingEffectFrame.Cells"/> are keyed by (specs/07-lighting.md §4).
        /// </para>
        /// </summary>
        private static IReadOnlyList<LightingPreviewKey> BuildPreviewKeys(KeyboardLayerViewModel board)
        {
            var width = board.BoardWidth;
            var height = board.BoardHeight;

            if (width <= 0.0 || height <= 0.0)
            {
                return [];
            }

            var keys = new List<LightingPreviewKey>(board.Keys.Count);

            foreach (var key in board.Keys)
            {
                keys.Add(new LightingPreviewKey(
                    key.Key.OriginalKey.Code,
                    (key.X + (key.Width / 2.0)) / width,
                    (key.Y + (key.Height / 2.0)) / height));
            }

            return keys;
        }
    }
}
