using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;
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
        /// "Left half · d position"). It is the <b>drawn</b> panel, not a hand of the user's:
        /// on the Freestyle Edge RGB panel 0 is the hotkey column plus the left typing half.
        /// </summary>
        public const string LeftHalfDescription = "Left half";

        /// <summary>How it names the second panel.</summary>
        public const string RightHalfDescription = "Right half";

        /// <summary>
        /// Where a cap sits, in the inspector's words. A board drawn in one piece has no halves to
        /// name and answers with an empty string rather than calling its only panel "left" — the
        /// header then reads "d position", which is still true.
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
        /// One key-table entry spelled the way the device's own config file spells it — <c>esc</c>,
        /// <c>d</c>.
        ///
        /// <para><b>The file's square brackets are deliberately not drawn</b> (issue #150). They were
        /// here until then, and they were what made the mono law — "mono means this is literally a
        /// value in a config file" — literally true of every token in the app. The designer's mock
        /// draws a bare token in a chip, the chip is what says "this is a value", and the user chose
        /// the mock knowing the cost. The type stays mono, so the law still <em>reads</em>; it is a
        /// recorded deviation rather than a silent one — see docs/app/design-system.md's deviation
        /// ledger.</para>
        ///
        /// <para>An entry with no token in this dialect falls back to its caption, so the inspector
        /// says <em>something</em> about a position it cannot spell rather than nothing at all.
        /// Nothing distinguishes that fallback from a token any more — the brackets were the only
        /// thing that ever did — and nothing in the app depends on telling them apart: every
        /// consumer draws the string and none of them parses it. Every position of every shipped
        /// geometry is built from a token, so the fallback is a guard rather than a path.</para>
        /// </summary>
        public static string FormatToken(KeyDefinition key, TokenDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(key);

            var token = key.GetToken(dialect);

            return token.Length > 0
                ? token
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
        /// <c>factory d</c> half of the inspector's assignment line (mockups <c>1e</c>/<c>2a</c>)
        /// and the token its header names the position by.
        /// <para>
        /// It is <see cref="KeyboardKey.OriginalKey"/> and never the silkscreen: the print is what
        /// the cap says (<see cref="Caption"/>), the token is what the file says, and on the hotkey
        /// column the two differ outright — the cap reads <c>vdrv</c> and the file reads
        /// <c>hk7</c>. Fixed for the life of the position, so it never notifies.
        /// </para>
        /// </summary>
        public string FactoryAssignmentText { get; }

        /// <summary>
        /// What the position does <b>now</b>, as the file would spell it — the <c>now esc</c>
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

        /// <summary>
        /// Whether this is the key the editor's actions apply to — the <b>Layout board's</b> single
        /// selection, the one the key inspector rail follows.
        /// <para>
        /// It is the counterpart of <see cref="IsLightingSelected"/> and stays separate from it for
        /// the reason recorded there. Both boards render the very same cap view models, so this is
        /// true on the Lighting board too; since issue #133 the cap simply does not <i>draw</i> it
        /// there (<c>.selected:not(.lighting)</c> in the <c>KeyCapButton</c> theme). Nothing clears
        /// it on a tab switch — the selection is still here when the user comes back.
        /// </para>
        /// </summary>
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
        /// the same reason as <see cref="PaintColorHex"/>: the fact lives outside
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
        /// The colour this key's <b>face</b> is painted with, as <c>#RRGGBB</c>, or null when the
        /// key is unpainted. It is the *paint* layer of the lighting board, drawn under the
        /// previewed effect at <see cref="PaintOpacity"/>, and it stands for model state: a mode
        /// that ignores it does not erase it.
        /// <para>
        /// It is the key's colour on file — one entry of <see cref="LayerLightingState.KeyColors"/>
        /// — <b>softened for the preview</b> by <see cref="LedPreviewTint"/>, so it is a value to
        /// draw and never a value to store or to show as a number
        /// ([#124](https://github.com/migus88/kinesis-edit/issues/124)). What is on file is what the
        /// lighting model holds and what the rail's colour slots show verbatim; nothing reads it
        /// back off the cap.
        /// </para>
        /// <para>
        /// Pushed in through <see cref="ApplyPaint"/> rather than re-read by
        /// <see cref="RefreshFromModel"/>, because the colour lives in the lighting model and not
        /// in <see cref="KeyboardKey"/> (docs/app/keyboard-editor.md, "The Lighting tab"). The
        /// setter is public so a test or a design scene can stand a lit cap up in one initializer,
        /// and it takes the face colour it is given as-is — softening happens on the
        /// <see cref="ApplyPaint"/> path alone, because it must happen exactly once. The app writes
        /// through <see cref="ApplyPaint"/>, which is also the path that skips the formatting when
        /// nothing moved.
        /// </para>
        /// </summary>
        public string? PaintColorHex
        {
            get => _paintColorHex;
            set
            {
                _paintColor = KeyColorOverlay.TryParseHex(value, out var parsed) ? parsed : null;

                SetPaintColorHex(value);
            }
        }

        /// <summary>Whether this key carries a painted colour on file.</summary>
        public bool HasPaintColor => PaintColorHex is not null;

        /// <summary>
        /// The opacity the paint layer is drawn at under the previewed effect:
        /// <see cref="LightingEffectFrame.PaintOpacityDirect"/> under a mode that renders the paint,
        /// <see cref="LightingEffectFrame.PaintOpacityDimmed"/> under one that ignores it ("the
        /// colors are still on file"), <see cref="LightingEffectFrame.PaintOpacityHidden"/> under
        /// Disable and Pitch Black. It is the frame's answer, never this cap's
        /// (<see cref="LightingPaintModes.PaintOpacityFor"/>).
        /// </summary>
        public double PaintOpacity
        {
            get => _paintOpacity;
            set
            {
                if (SetProperty(ref _paintOpacity, value))
                {
                    RaisePaintSideChanged();
                }
            }
        }

        /// <summary>
        /// The colour the previewed effect lights this key with in <b>this frame</b>, as
        /// <c>#RRGGBB</c>, or null when the effect does not reach it. It is not file state and
        /// never survives a save; it is re-pushed ~30 times a second by
        /// <see cref="LightingTabViewModel.AdvancePreview"/>, and — like
        /// <see cref="PaintColorHex"/> — it is the sampler's colour after
        /// <see cref="LedPreviewTint"/>, i.e. what the face is painted with rather than what the
        /// sampler answered.
        /// <para>
        /// Null rather than black when unlit: black is a colour a key can legitimately be lit
        /// (Pitch Black lights every key black), so "unlit" has to be absence — which is exactly
        /// <see cref="LightingEffectFrame.Cells"/>'s own contract, and the reason softening leaves
        /// black alone rather than lifting it.
        /// </para>
        /// </summary>
        public string? EffectColorHex
        {
            get => _effectColorHex;
            set
            {
                _effectColor = KeyColorOverlay.TryParseHex(value, out var parsed) ? parsed : null;

                SetEffectColorHex(value);
            }
        }

        /// <summary>Whether the previewed effect lights this key in the current frame.</summary>
        public bool HasEffectColor => EffectColorHex is not null;

        /// <summary>
        /// The alpha the effect layer is drawn at this frame, 0..1 — the sampler's own answer
        /// (<c>LightingPreviewCell.Intensity</c>), untouched. Which <i>side</i> of it the paint is
        /// drawn on is <see cref="ShowsPaintUnderEffect"/>/<see cref="ShowsPaintOverEffect"/>.
        /// </summary>
        public double EffectIntensity
        {
            get => _effectIntensity;
            set => SetProperty(ref _effectIntensity, value);
        }

        /// <summary>
        /// <b>Which side of the effect the paint layer is drawn on — the one place the 40 % / 60 %
        /// blend lives</b>, together with <see cref="ShowsPaintOverEffect"/>. Exactly one of the
        /// two is ever true, and neither is when the key is unpainted.
        /// <para>
        /// This is <b>under</b>: a mode that renders the paint directly
        /// (<see cref="LightingEffectFrame.PaintOpacityDirect"/> — Freestyle, Breathe, Frozen Wave)
        /// paints the cap in full and lets the effect layer above modulate it, which is what makes
        /// Breathe pulse.
        /// </para>
        /// <para>
        /// <b>Why the two sides.</b> Mockup 2f, verbatim: "Wave ignores painted colors, so the
        /// paint layer is shown at 40% under the effect — the colors are still on file." Drawn
        /// literally under, that sentence renders as nothing at all: Wave, Solid and Spectrum light
        /// every key at intensity 1.0, so the effect above covers the paint outright and 0 % of it
        /// survives. Putting the same layer, at the same
        /// <see cref="LightingEffectFrame.PaintOpacityDimmed"/>, <i>over</i> the effect composites
        /// to the blend the design describes — <c>effect·(1 − 0.4) + paint·0.4</c> — so the effect
        /// still reads and travels and the painted colour is present at the 40 % it was promised.
        /// The two shares are therefore one number, the paint layer's own opacity, and its
        /// complement is whatever is left of the cap: they cannot drift apart because there is only
        /// one of them.
        /// </para>
        /// </summary>
        public bool ShowsPaintUnderEffect => HasPaintColor && _paintOpacity >= LightingEffectFrame.PaintOpacityDirect;

        /// <summary>
        /// The other side: a mode that <b>ignores</b> the paint draws it over the effect, at the
        /// dimmed opacity the frame handed down. See <see cref="ShowsPaintUnderEffect"/> for the
        /// arithmetic and why the order is what carries it.
        /// <para>
        /// A paint opacity of <see cref="LightingEffectFrame.PaintOpacityHidden"/> — Disable and
        /// Pitch Black — draws no paint layer on either side, so those two boards stay hatched.
        /// </para>
        /// </summary>
        public bool ShowsPaintOverEffect => HasPaintColor
            && _paintOpacity > LightingEffectFrame.PaintOpacityHidden
            && _paintOpacity < LightingEffectFrame.PaintOpacityDirect;

        /// <summary>
        /// Whether this cap is in the <b>lighting board's</b> paint selection — the set a colour
        /// or a Clear applies to (<see cref="LightingPaintSelection"/>).
        /// <para>
        /// Deliberately not <see cref="IsSelected"/>: that is the Keys tab's <i>single</i>
        /// selection, the one the key inspector rail follows, and the two boards render the very
        /// same cap view models. One flag for both would make a lighting multi-selection open the
        /// inspector on whichever key happened to be last.
        /// </para>
        /// <para>
        /// Two flags is not by itself enough, because both are true on both boards: each is drawn
        /// only on the board that owns it (<c>.lighting.paintSelected</c> since #131,
        /// <c>.selected:not(.lighting)</c> since #133). Neither is cleared by a tab switch.
        /// </para>
        /// </summary>
        public bool IsLightingSelected
        {
            get => _isLightingSelected;
            set => SetProperty(ref _isLightingSelected, value);
        }

        private readonly KeyVisual _visual;
        private readonly TokenDialect _dialect;
        private string _caption;
        private string _currentAssignmentText;
        private LedColor? _paintColor;
        private LedColor? _effectColor;
        private string? _paintColorHex;
        private string? _effectColorHex;
        private double _paintOpacity = LightingEffectFrame.PaintOpacityDirect;
        private double _effectIntensity;
        private bool _isModified;
        private bool _isMacro;
        private bool _isTapAndHold;
        private bool _isSelected;
        private bool _isListening;
        private bool _hasAdvisory;
        private bool _isLightingSelected;

        /// <summary>Joins one model key to its placement.</summary>
        public KeyboardKeyViewModel(KeyboardKey key, KeyVisual visual, TokenDialect dialect, LedColor? paintColor = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            _visual = visual ?? throw new ArgumentNullException(nameof(visual));
            _dialect = dialect;

            ApplyPaint(paintColor, LightingEffectFrame.PaintOpacityDirect);

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
        /// Sets the key's painted colour and the opacity the paint layer is drawn at.
        /// <paramref name="color"/> is the colour <b>on file</b>, and is null when the key is
        /// unpainted.
        /// <para>
        /// It takes a <see cref="LedColor"/> rather than a hex string so that the string is
        /// formatted only when the colour actually moved: this runs for every cap of a layer, and
        /// <see cref="ApplyEffect"/> beside it runs ~30 times a second on ~76 caps, so an
        /// unconditional format-and-notify would be the most expensive thing on the screen.
        /// </para>
        /// <para>
        /// <b>This is the one seam the preview softening is applied at</b>
        /// (<see cref="LedPreviewTint"/>): a stored colour becomes a face colour here, and here
        /// only. Deliberately not in <c>KeyColorOverlay.ToHex</c> and not in the view's
        /// hex-to-brush converter — both are shared with the rail's colour slots and the picker,
        /// which must keep showing the file's value verbatim — and deliberately upstream of
        /// <c>LitLabelBrushConverter</c>, which reads <see cref="PaintColorHex"/> to decide the lit
        /// caption's light/dark flip and would otherwise be reasoning about a colour nothing draws.
        /// The change check stays on the <i>stored</i> colour, so an unchanged key still costs
        /// nothing and the softening never runs twice on the same value.
        /// </para>
        /// </summary>
        public void ApplyPaint(LedColor? color, double opacity)
        {
            if (_paintColor != color)
            {
                _paintColor = color;

                SetPaintColorHex(color is { } value ? KeyColorOverlay.ToHex(LedPreviewTint.Soften(value)) : null);
            }

            PaintOpacity = opacity;
        }

        /// <summary>
        /// Sets what the previewed effect lights this key with this frame — see
        /// <see cref="EffectColorHex"/>. <paramref name="color"/> is the sampler's answer and is
        /// null when the effect does not reach the key, in which case the intensity is irrelevant
        /// rather than meaningful. It is softened for the face on the same terms as
        /// <see cref="ApplyPaint"/>, which is where the reasoning is written down.
        /// </summary>
        public void ApplyEffect(LedColor? color, double intensity)
        {
            if (_effectColor != color)
            {
                _effectColor = color;

                SetEffectColorHex(color is { } value ? KeyColorOverlay.ToHex(LedPreviewTint.Soften(value)) : null);
            }

            EffectIntensity = intensity;
        }

        /// <summary>
        /// Writes <see cref="PaintColorHex"/>'s backing field and raises the pair of notifications
        /// that go with it — the half of the property that is shared with <see cref="ApplyPaint"/>,
        /// which has already resolved the <see cref="LedColor"/> and must not re-parse the string
        /// it just formatted.
        /// </summary>
        private void SetPaintColorHex(string? hex)
        {
            if (SetProperty(ref _paintColorHex, hex, nameof(PaintColorHex)))
            {
                OnPropertyChanged(nameof(HasPaintColor));

                // A key that has just gained or lost its paint has gained or lost a paint LAYER,
                // and the mode need not have moved for that to happen.
                RaisePaintSideChanged();
            }
        }

        /// <summary>The same, for <see cref="EffectColorHex"/> and <see cref="ApplyEffect"/>.</summary>
        private void SetEffectColorHex(string? hex)
        {
            if (SetProperty(ref _effectColorHex, hex, nameof(EffectColorHex)))
            {
                OnPropertyChanged(nameof(HasEffectColor));
            }
        }

        /// <summary>
        /// Announces the pair of derived flags that say which side of the effect the paint layer is
        /// drawn on. Both inputs — whether the key is painted at all, and at what opacity — move
        /// independently of each other, and neither notifies for the other.
        /// </summary>
        private void RaisePaintSideChanged()
        {
            OnPropertyChanged(nameof(ShowsPaintUnderEffect));
            OnPropertyChanged(nameof(ShowsPaintOverEffect));
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

            // The cap's own abbreviation, applied here and nowhere else: this is the one caption
            // that has to survive a 28 px box. Everything that writes the key's name into prose —
            // FormatToken above, the macro rows, the co-trigger caption, the library header —
            // calls KeyCaption directly and keeps the full word.
            return KeyCapCaption.For(KeyCaption.ForKey(Key, _dialect, EmbeddedFontGlyphCoverage.Instance));
        }
    }
}
