using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One key cap of the keyboard picture: a <see cref="KeyboardKey"/> of the runtime model
    /// joined to the <see cref="KeyVisual"/> that says where it sits. Core's model raises no
    /// change notification (docs/app/keyboard-model.md — plain mutable POCOs), so this wrapper is
    /// where the UI learns that an edit happened: every mutation is followed by
    /// <see cref="RefreshFromModel"/>.
    /// <para>
    /// Geometry is in <b>key units</b> (1.0 = one 1U cap) and never changes — the same rectangles
    /// are drawn for every layer of a device — so it is exposed straight from the visual. The
    /// colour overlay is a <c>#RRGGBB</c> string, not a brush: view models expose values only
    /// (docs/app/app-shell.md, invariant 6).
    /// </para>
    /// </summary>
    public sealed class KeyboardKeyViewModel : ViewModelBase
    {
        /// <summary>The model object this cap edits.</summary>
        public KeyboardKey Key { get; }

        /// <summary>Ordinal of the position inside its layer (specs/05-key-model.md §7.4).</summary>
        public int Index => _visual.Index;

        /// <summary>Left edge in key units, board-absolute.</summary>
        public double X => _visual.X;

        /// <summary>Top edge in key units, board-absolute.</summary>
        public double Y => _visual.Y;

        /// <summary>Cap width in key units.</summary>
        public double Width => _visual.Width;

        /// <summary>Cap height in key units.</summary>
        public double Height => _visual.Height;

        /// <summary>Presentational grouping of the key (main block, thumb cluster, hotkey column…).</summary>
        public KeyCluster Cluster => _visual.Cluster;

        /// <summary>What the cap reads right now — the remapped action when there is one (see <see cref="KeyCaption"/>).</summary>
        public string Caption
        {
            get => _caption;
            private set => SetProperty(ref _caption, value);
        }

        /// <summary>Whether the position carries a remap (specs/05-key-model.md §1.3).</summary>
        public bool IsModified
        {
            get => _isModified;
            private set => SetProperty(ref _isModified, value);
        }

        /// <summary>Whether the position may be remapped at all (§5.3).</summary>
        public bool CanEdit => Key.CanEdit;

        /// <summary>Whether the position may carry a macro (§5.3); consumed by issue #15.</summary>
        public bool CanAssignMacro => Key.CanAssignMacro;

        /// <summary>Whether this is the key the editor's actions apply to.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Whether this key is waiting for the next physical keypress to become its new assignment
        /// (specs/10-apps-and-ui.md, "Remap workflow"). At most one key of the editor is listening.
        /// </summary>
        public bool IsListening
        {
            get => _isListening;
            set => SetProperty(ref _isListening, value);
        }

        /// <summary>The key's LED colour as <c>#RRGGBB</c>, or null when it has none.</summary>
        public string? ColorOverlayHex { get; }

        /// <summary>Whether the key carries a colour overlay.</summary>
        public bool HasColorOverlay => ColorOverlayHex is not null;

        private readonly KeyVisual _visual;
        private readonly TokenDialect _dialect;
        private string _caption;
        private bool _isModified;
        private bool _isSelected;
        private bool _isListening;

        /// <summary>Joins one model key to its placement.</summary>
        public KeyboardKeyViewModel(KeyboardKey key, KeyVisual visual, TokenDialect dialect, string? colorOverlayHex = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            _visual = visual ?? throw new ArgumentNullException(nameof(visual));
            _dialect = dialect;
            ColorOverlayHex = colorOverlayHex;

            _caption = KeyCaption.ForKey(key, dialect);
            _isModified = key.IsModified;
        }

        /// <summary>
        /// Re-reads the model after an edit and raises whatever actually moved. Core mutates in
        /// place and announces nothing, so every path that writes to <see cref="Key"/> must end
        /// here or the cap keeps showing the old assignment.
        /// </summary>
        public void RefreshFromModel()
        {
            Caption = KeyCaption.ForKey(Key, _dialect);
            IsModified = Key.IsModified;
        }
    }
}
