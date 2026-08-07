using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

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
        /// <summary>
        /// How the key inspector names the first panel of a split board (mockups <c>1e</c>/<c>2a</c>:
        /// "Left half · [d] position"). It is the <b>drawn</b> panel, not a hand of the user's:
        /// on the Freestyle Edge RGB panel 0 is the hotkey column plus the left typing half.
        /// </summary>
        public const string LeftHalfDescription = "Left half";

        /// <summary>How it names the second panel.</summary>
        public const string RightHalfDescription = "Right half";

        /// <summary>
        /// Where a cap sits, in the inspector's words. A board drawn in one piece has no halves to
        /// name and answers with an empty string rather than calling its only panel "left" — the
        /// header then reads "[d] position", which is still true.
        /// <para>
        /// It takes the section count rather than reading it off the cap because the cap knows only
        /// its own <see cref="Section"/>; the layer is what knows how many panels the board has
        /// (<see cref="KeyboardLayerViewModel.Sections"/>). Anything past the second panel is
        /// likewise left unnamed: no supported board has one, and inventing "third half" would be
        /// worse than saying nothing.
        /// </para>
        /// </summary>
        public static string DescribeSection(int section, int sectionCount)
        {
            if (sectionCount < 2)
            {
                return string.Empty;
            }

            return section switch
            {
                0 => LeftHalfDescription,
                1 => RightHalfDescription,
                _ => string.Empty
            };
        }

        /// <summary>
        /// One key-table entry spelled the way the device's own config file spells it —
        /// <c>[esc]</c>, <c>[d]</c>. The brackets are the file's (specs/04-layout-files.md), which
        /// is what makes this mono type rather than sans: "mono means this is literally a value in
        /// a config file".
        /// <para>
        /// An entry with no token in this dialect falls back to its caption, unbracketed, so the
        /// inspector says <em>something</em> about a position it cannot spell rather than showing
        /// an empty pair of brackets. Every position of every shipped geometry is built from a
        /// token, so the fallback is a guard rather than a path.
        /// </para>
        /// </summary>
        public static string FormatToken(KeyDefinition key, TokenDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(key);

            var token = key.GetToken(dialect);

            return token.Length > 0
                ? "[" + token + "]"
                : KeyCaption.For(key, dialect, KeyCaption.IsMacOs, EmbeddedFontGlyphCoverage.Instance);
        }

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

        /// <summary>Which board panel the cap is drawn in (see <see cref="KeyboardSection"/>).</summary>
        public int Section => _visual.Section;

        /// <summary>
        /// The cap's secondary silkscreen legend — the shifted character or the device hotkey
        /// printed under the main one (<c>"!"</c>, <c>"mute"</c>, <c>"scr lk"</c>), or null when
        /// the cap carries none. It is the <b>physical print</b>, not a second assignment: it comes
        /// off the immutable <see cref="KeyVisual"/> and so never changes and never notifies.
        /// </summary>
        public string? SecondaryLegend => _visual.SecondaryLegend;

        /// <summary>
        /// What the cap reads right now — the remapped action when there is one (see
        /// <see cref="KeyCaption"/>), otherwise the physical silkscreen
        /// (<see cref="KeyVisual.Legend"/>) when the board authored one for this position.
        /// <para>
        /// That is the whole of the legend rule: an unmodified digit reads <c>1</c> rather than the
        /// token's <c>1 !</c> (the <c>!</c> is the <see cref="SecondaryLegend"/>), the hotkey column
        /// reads its printed marks rather than <c>hk0</c>…, and a key that has been remapped always
        /// reads what it now does. A position whose print is what the caption already says carries
        /// no legend at all, so null means "use the caption" rather than "draw nothing".
        /// </para>
        /// </summary>
        public string Caption
        {
            get => _caption;
            private set
            {
                if (SetProperty(ref _caption, value))
                {
                    OnPropertyChanged(nameof(IsCaptionStacked));
                }
            }
        }

        /// <summary>
        /// Whether <see cref="Caption"/> carries a line break of its own — <c>Caps\nLock</c>,
        /// <c>Page\nDown</c> (specs/05-key-model.md §1.1 authors nine such positions on the Edge
        /// RGB). Two lines do not fit a 30x26 cap at the caption step, so the cap draws a stacked
        /// caption one step down; see the vertical budget at the head of Controls/KeyCapView.axaml.
        /// <para>
        /// It moves with the caption: a remap turns <c>Caps\nLock</c> into <c>Z</c> and the cap has
        /// to come back up to the full step, so the setter above raises this alongside it.
        /// </para>
        /// </summary>
        public bool IsCaptionStacked => Caption.Contains('\n', StringComparison.Ordinal);

        /// <summary>
        /// What the board shipped this position doing, as its config file spells it — the
        /// <c>factory [d]</c> half of the inspector's assignment line (mockups <c>1e</c>/<c>2a</c>)
        /// and the token its header names the position by.
        /// <para>
        /// It is <see cref="KeyboardKey.OriginalKey"/> and never the silkscreen: the print is what
        /// the cap says (<see cref="Caption"/>), the token is what the file says, and on the hotkey
        /// column the two differ outright — the cap reads <c>vdrv</c> and the file reads
        /// <c>[hk7]</c>. Fixed for the life of the position, so it never notifies.
        /// </para>
        /// </summary>
        public string FactoryAssignmentText { get; }

        /// <summary>
        /// What the position does <b>now</b>, as the file would spell it — the <c>now [esc]</c>
        /// half of the assignment line. <see cref="KeyboardKey.ModifiedOrOriginalKey"/>, so it
        /// equals <see cref="FactoryAssignmentText"/> until the position is remapped.
        /// <para>
        /// Re-read by <see cref="RefreshFromModel"/> like every other derived readout: Core mutates
        /// in place and announces nothing.
        /// </para>
        /// </summary>
        public string CurrentAssignmentText
        {
            get => _currentAssignmentText;
            private set => SetProperty(ref _currentAssignmentText, value);
        }

        /// <summary>Whether the position carries a remap (specs/05-key-model.md §1.3).</summary>
        public bool IsModified
        {
            get => _isModified;
            private set => SetProperty(ref _isModified, value);
        }

        /// <summary>
        /// Whether the position carries at least one macro (§1.3 <c>IsMacro</c>) — the cap's macro
        /// dot. Notifying and re-read by <see cref="RefreshFromModel"/>, because the macro panel
        /// writes into <see cref="Key"/> in place and Core announces nothing.
        /// </summary>
        public bool IsMacro
        {
            get => _isMacro;
            private set => SetProperty(ref _isMacro, value);
        }

        /// <summary>
        /// Whether the position carries a tap-and-hold assignment (§5.6) — the cap's corner badge.
        /// Notifying and re-read by <see cref="RefreshFromModel"/>, for the same reason as
        /// <see cref="IsMacro"/>.
        /// </summary>
        public bool IsTapAndHold
        {
            get => _isTapAndHold;
            private set => SetProperty(ref _isTapAndHold, value);
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

        /// <summary>
        /// Whether this position carries an advisory — a duplicate token today
        /// (<see cref="Core.Model.DuplicateKeyScan"/>), anything anchored to a key tomorrow. Pushed
        /// in by the editor from <see cref="Advisories.EditorAdvisories"/> after every rebuild, for
        /// the same reason as <see cref="ColorOverlayHex"/>: the fact lives outside
        /// <see cref="KeyboardKey"/>, so <see cref="RefreshFromModel"/> cannot reach it.
        /// <para>
        /// It is drawn as the 12×3 px <c>StatusAdvisoryStrong</c> rounded bar in the cap's
        /// top-right corner — one of the five badges of the vocabulary the cap carries (remap bar,
        /// macro dot, tap-and-hold corner, advisory bar, locked hatch), all of which are also
        /// counted on the layer (<see cref="KeyboardLayerViewModel.AdvisoryCount"/>).
        /// </para>
        /// </summary>
        public bool HasAdvisory
        {
            get => _hasAdvisory;
            set => SetProperty(ref _hasAdvisory, value);
        }

        /// <summary>
        /// The key's LED colour as <c>#RRGGBB</c>, or null when it has none. Settable and
        /// notifying because the Lighting tab re-paints keys while the editor is open
        /// (docs/app/keyboard-editor.md, "The Lighting tab"): the colour lives in the lighting
        /// model, not in <see cref="KeyboardKey"/>, so it is pushed in through
        /// <see cref="KeyboardLayerViewModel.ApplyColorOverlays"/> rather than re-read here.
        /// </summary>
        public string? ColorOverlayHex
        {
            get => _colorOverlayHex;
            set
            {
                if (SetProperty(ref _colorOverlayHex, value))
                {
                    OnPropertyChanged(nameof(HasColorOverlay));
                }
            }
        }

        /// <summary>
        /// Whether this key's LED is <b>lit</b>. It is not "should the cap draw an LED strip" — the
        /// Keys tab and the Lighting tab render the same cap view models, so that question is the
        /// picture's (<c>KeyboardView.ShowsLedStrips</c>) and an unlit key on a lighting board is
        /// hatched rather than absent.
        /// </summary>
        public bool HasColorOverlay => ColorOverlayHex is not null;

        private readonly KeyVisual _visual;
        private readonly TokenDialect _dialect;
        private string _caption;
        private string _currentAssignmentText;
        private string? _colorOverlayHex;
        private bool _isModified;
        private bool _isMacro;
        private bool _isTapAndHold;
        private bool _isSelected;
        private bool _isListening;
        private bool _hasAdvisory;

        /// <summary>Joins one model key to its placement.</summary>
        public KeyboardKeyViewModel(KeyboardKey key, KeyVisual visual, TokenDialect dialect, string? colorOverlayHex = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            _visual = visual ?? throw new ArgumentNullException(nameof(visual));
            _dialect = dialect;
            _colorOverlayHex = colorOverlayHex;

            _caption = ResolveCaption();
            FactoryAssignmentText = FormatToken(key.OriginalKey, dialect);
            _currentAssignmentText = FormatToken(key.ModifiedOrOriginalKey, dialect);
            _isModified = key.IsModified;
            _isMacro = key.IsMacro;
            _isTapAndHold = key.IsTapAndHold;
        }

        /// <summary>
        /// Re-reads the model after an edit and raises whatever actually moved. Core mutates in
        /// place and announces nothing, so every path that writes to <see cref="Key"/> must end
        /// here or the cap keeps showing the old assignment.
        /// </summary>
        public void RefreshFromModel()
        {
            Caption = ResolveCaption();
            CurrentAssignmentText = FormatToken(Key.ModifiedOrOriginalKey, _dialect);
            IsModified = Key.IsModified;
            IsMacro = Key.IsMacro;
            IsTapAndHold = Key.IsTapAndHold;
        }

        /// <summary>
        /// The legend rule of <see cref="Caption"/>, in one place so the constructor and
        /// <see cref="RefreshFromModel"/> cannot drift apart — invariant 3 of
        /// docs/app/keyboard-editor.md ends every model write in the latter, so the two have to
        /// agree about what an untouched cap reads.
        /// </summary>
        private string ResolveCaption()
        {
            if (!Key.IsModified && _visual.Legend is { Length: > 0 } legend)
            {
                return legend;
            }

            return KeyCaption.ForKey(Key, _dialect, EmbeddedFontGlyphCoverage.Instance);
        }
    }
}
