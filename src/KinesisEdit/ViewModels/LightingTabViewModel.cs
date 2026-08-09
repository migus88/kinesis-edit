using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's Lighting tab: the per-layer LED editor of specs/07-lighting.md §3 and §4,
    /// redesigned around design mockup 2f — <b>the mode is rendered on the board</b>, which is
    /// still the point of the screen: the board animates the selected mode at its real speed and
    /// direction, and under it sits the paint selection the colour picker applies to.
    /// <para>
    /// <b>The rail beside it is a properties panel</b> (issue #128). 2f drew a fourteen-row mode
    /// list taking most of the column and a fixed parameter footer under it; the list is a
    /// <c>ComboBox</c> now, and <b>only the properties the selected mode actually has are
    /// rendered</b> — each with one line saying what it means in that mode
    /// (<see cref="LightingHintCatalog"/>). What a mode has is still exactly one answer, Core's
    /// <see cref="LightingModeParameters"/>; nothing here decides it a second time.
    /// </para>
    /// <para>
    /// It owns no lighting rules. Mode membership and firmware gating are
    /// <see cref="LightingAvailability"/>'s, what a mode accepts is
    /// <see cref="LightingModeParameters"/>'s, what the board looks like at an instant is
    /// <see cref="LightingEffectSampler"/>'s, the zones are <see cref="LightingZoneCatalog"/>'s,
    /// and the file is written by <c>ProfileSession.Save</c> — this panel mutates the model the
    /// session handed out, so the editor's Save persists lighting with no save path of its own.
    /// </para>
    /// </summary>
    public sealed class LightingTabViewModel : ViewModelBase
    {
        /// <summary>Caption of the button that erases every per-key color (specs/07-lighting.md §4).</summary>
        public const string ResetAllCaption = "Reset All";

        /// <summary>Title of the "Reset All" confirmation. Not a spec string.</summary>
        public const string ResetAllTitle = "Reset All";

        /// <summary>The confirmation prompt, quoted verbatim from specs/07-lighting.md §4.</summary>
        public const string ResetAllConfirmation = "Do you want to erase color assignments for each key";

        /// <summary>
        /// The affirmative of that confirmation, named after what it does rather than "Yes"
        /// (docs/design/mockups.md, mockup 1k). It still answers <c>Yes</c>.
        /// <para>
        /// <c>color</c>, not <c>colour</c>: every user-facing lighting string in this app is
        /// American ("Effect Color", "Add to Custom Colors", and the spec prompt this button
        /// answers — "erase color assignments for each key"). One British spelling in the middle of
        /// them would read as a typo.
        /// </para>
        /// </summary>
        public const string ResetAllConfirmCaption = "Erase colors";

        /// <summary>The way out of that confirmation. It still answers <c>No</c>.</summary>
        public const string ResetAllDeclineCaption = "Cancel";

        /// <summary>The note shown in demo mode (03 §3.5): everything is explorable, nothing is written.</summary>
        public const string DemoModeHint =
            "This device is open in demo mode, so lighting can be explored but not saved.";

        /// <summary>Why the Fn layer and the base-color swatch are unavailable (specs/07-lighting.md §3).</summary>
        public const string LayerCustomizationLockedHint =
            "Fn-layer lighting and per-effect base colors need LED firmware 1.0.44 or newer.";

        /// <summary>
        /// What the board header says while the preview animates — mockup 2f's "Wave · live
        /// preview", verbatim.
        /// </summary>
        public const string LivePreviewSuffix = " · live preview";

        /// <summary>
        /// What it says while the preview is frozen. The board still shows the mode's first frame,
        /// so it is a preview; it is just not a live one.
        /// </summary>
        public const string FrozenPreviewSuffix = " · preview";

        /// <summary>
        /// The rail's own header. Mockup 2f reads "Mode — click to preview on the board", which
        /// described a scrolled list of fourteen rows; the rail is a properties panel over a
        /// dropdown since issue #128, so the instruction is kept and the gesture it names is not.
        /// </summary>
        public const string ModeRailCaption = "Mode — previewed on the board";

        /// <summary>
        /// Whether this panel can edit <paramref name="device"/>'s lighting. True exactly for a
        /// board whose led file is the plain two-layer key-backlight model this panel understands:
        /// per-key RGB hardware <b>without</b> an edge strip.
        /// <para>
        /// That is capability data, not a device id — but it is also the deferral line: the TKO
        /// adds a second, parallel edge section to the same file (<c>TkoLightingModel</c>) and the
        /// Advantage 360 has six indicators instead of keys (<c>Advantage360LightingModel</c>), and
        /// neither editor is built (issues #40/#41). Both models therefore leave the tab dark.
        /// </para>
        /// </summary>
        public static bool IsSupported(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            return device.Lighting.Kind == LightingKind.PerKeyRgb && !device.Lighting.HasEdgeLighting;
        }

        /// <summary>Whether the device's lighting is editable here at all (<see cref="IsSupported"/>).</summary>
        public bool IsAvailable { get; }

        /// <summary>An inline note about this panel's state — the demo-mode hint, or empty.</summary>
        public string StatusMessage { get; }

        /// <summary>Whether there is a <see cref="StatusMessage"/> to show.</summary>
        public bool HasStatusMessage => StatusMessage.Length > 0;

        /// <summary>
        /// Whether the <c>LightingLayerCustomization</c> gate passes (LED firmware ≥ 1.0.44 on the
        /// RGB, specs/07-lighting.md §3). It governs both the Fn layer and the base-color swatch.
        /// </summary>
        public bool IsLayerCustomizationAvailable { get; }

        /// <summary>Core's explanation of that gate; empty while it passes.</summary>
        public string LayerLockHint => IsLayerCustomizationAvailable ? string.Empty : LayerCustomizationLockedHint;

        /// <summary>The two layers a led file describes, or empty until the profile is attached.</summary>
        public IReadOnlyList<LightingLayerViewModel> Layers
        {
            get => _layers;
            private set => SetProperty(ref _layers, value);
        }

        /// <summary>The layer every control on this tab reads and writes (§4).</summary>
        public LightingLayerViewModel? SelectedLayer
        {
            get => _selectedLayer;
            private set => SetProperty(ref _selectedLayer, value);
        }

        /// <summary>The keyboard picture of <see cref="SelectedLayer"/> — the previewed board.</summary>
        public KeyboardLayerViewModel? Board
        {
            get => _board;
            private set => SetProperty(ref _board, value);
        }

        /// <summary>The device's mode menu for its firmware (§3, mockup 2f).</summary>
        public IReadOnlyList<LightingModeViewModel> Modes { get; }

        /// <summary>The selected layer's mode.</summary>
        public LightingMode SelectedMode
        {
            get => _selectedMode;
            private set
            {
                if (SetProperty(ref _selectedMode, value))
                {
                    OnPropertyChanged(nameof(ModeCaption));
                    OnPropertyChanged(nameof(BoardHeader));
                }
            }
        }

        /// <summary>
        /// The row of <see cref="Modes"/> for <see cref="SelectedMode"/> — what the rail's mode
        /// dropdown binds its <c>SelectedItem</c> to (issue #128), one-way, with the control's
        /// <c>SelectionChanged</c> running <see cref="SelectModeCommand"/>: the repo's established
        /// shape for a selector over a property the view model owns.
        /// <para>
        /// <b>Null is a real state</b>, not a bug: a led file may carry a mode the device's own
        /// menu does not offer — Ripple or Fireball below the KBD 1.0.121 / LED 1.0.58 gate (§3) —
        /// and <see cref="Modes"/> is the menu. The dropdown then shows its placeholder, which is
        /// <see cref="ModeCaption"/>, so the layer still says what it is set to.
        /// </para>
        /// </summary>
        public LightingModeViewModel? SelectedModeOption
        {
            get => _selectedModeOption;
            private set => SetProperty(ref _selectedModeOption, value);
        }

        /// <summary>What the rail calls the selected mode (<see cref="LightingModeCaptions"/>).</summary>
        public string ModeCaption => LightingModeCaptions.For(SelectedMode);

        /// <summary>
        /// The line over the board: <c>Wave · live preview</c> while it animates,
        /// <c>Wave · preview</c> while reduce-motion holds it on its first frame (mockup 2f).
        /// </summary>
        public string BoardHeader => ModeCaption + (IsPreviewAnimating ? LivePreviewSuffix : FrozenPreviewSuffix);

        /// <summary>
        /// Whether the board is animating. It is the negation of the live
        /// <see cref="IMotionSettings.ReduceMotion"/> preference, re-read on every
        /// <see cref="AdvancePreview"/> — see there for why it is polled rather than subscribed to.
        /// </summary>
        public bool IsPreviewAnimating
        {
            get => _isPreviewAnimating;
            private set
            {
                if (SetProperty(ref _isPreviewAnimating, value))
                {
                    OnPropertyChanged(nameof(BoardHeader));
                }
            }
        }

        /// <summary>
        /// What the selected mode accepts — the effect and base colours, the speed, the directions,
        /// whether it paints per key and whether the preview renders that paint directly. It is
        /// Core's answer (<see cref="LightingModeParameters.For"/>) and the one thing every control
        /// on this tab asks: the app layer holds no second copy of the §3 table.
        /// </summary>
        public LightingModeParameters Parameters
        {
            get => _parameters;
            private set
            {
                if (SetProperty(ref _parameters, value))
                {
                    OnPropertyChanged(nameof(ModeHint));
                    OnPropertyChanged(nameof(SpeedHint));
                    OnPropertyChanged(nameof(DirectionHint));
                    OnPropertyChanged(nameof(PickerHint));

                    NotifyCommands();
                }
            }
        }

        // ===== The rail's inline explanations (issue #128) =====================================
        // One line under each control saying what it means IN THE SELECTED MODE — the ask being
        // "I don't understand what is the difference between Effect and Base color in reactive
        // mode", which no per-property sentence could have answered. The copy is
        // LightingHintCatalog's; these four are the properties the view binds, and the two colour
        // swatches carry their own (LightingColorSlotViewModel.Hint) because a swatch is one row
        // with one meaning. They all follow Parameters rather than SelectedMode, because Parameters
        // is what decides which of them is on screen and it is written after the mode is.

        /// <summary>What the selected mode does, under the mode dropdown.</summary>
        public string ModeHint => LightingHintCatalog.ForMode(SelectedMode);

        /// <summary>What the nine speed bars move, in this mode.</summary>
        public string SpeedHint => LightingHintCatalog.ForSpeed(SelectedMode);

        /// <summary>What the direction arrows steer, in this mode.</summary>
        public string DirectionHint => LightingHintCatalog.ForDirection(SelectedMode);

        /// <summary>What the colour picker writes — the swatch above it, the paint selection, or both.</summary>
        public string PickerHint => LightingHintCatalog.ForPicker(Parameters.AcceptsAnyColor);

        /// <summary>The effect-color swatch.</summary>
        public LightingColorSlotViewModel EffectColor { get; }

        /// <summary>The base-color swatch of the two-line effects.</summary>
        public LightingColorSlotViewModel BaseColor { get; }

        /// <summary>The nine speed bars and their mono readout (mockup 2f).</summary>
        public LightingSpeedViewModel SpeedControl { get; }

        /// <summary>The effect speed, always inside <see cref="MinimumSpeed"/>..<see cref="MaximumSpeed"/>.</summary>
        public int Speed
        {
            get => _speed;
            set
            {
                var clamped = Math.Clamp(value, MinimumSpeed, MaximumSpeed);

                SpeedControl.Show(clamped);

                if (SetProperty(ref _speed, clamped) && SelectedLayer is not null)
                {
                    SelectedLayer.State.Speed = clamped;

                    RefreshBoard();
                    RaiseModelChanged();
                }
            }
        }

        /// <summary>Lowest speed the knob offers (specs/07-lighting.md §2.1).</summary>
        public int MinimumSpeed => LayerLightingState.MinimumSpeed;

        /// <summary>Highest speed the knob offers (§2.1).</summary>
        public int MaximumSpeed => LayerLightingState.MaximumSpeed;

        /// <summary>
        /// The four direction arrows, each carrying
        /// <see cref="LightingDirectionViewModel.IsAvailable"/>.
        /// <para>
        /// <b>The list is still four long; the rail draws only the available ones</b> (issue #128).
        /// Mockup 2f kept a mode's unusable arrows in place and struck them through so that "the
        /// row never changes shape as you move down the list" — and the list it was talking about
        /// is gone. Which arrow a mode accepts is still one answer, Core's, and it is still the
        /// only thing a write is validated against: an unavailable arrow that somehow reaches
        /// <see cref="SelectDirectionCommand"/> is a no-op whether or not it was ever drawn.
        /// </para>
        /// </summary>
        public IReadOnlyList<LightingDirectionViewModel> Directions
        {
            get => _directions;
            private set => SetProperty(ref _directions, value);
        }

        /// <summary>The device's zone buttons (§4), built once.</summary>
        public IReadOnlyList<LightingZoneViewModel> Zones { get; }

        /// <summary>
        /// The width of the rail beside the board — the editor's <b>one</b> rail width object, so a
        /// seam dragged here is the width the Keys tab's key inspector opens at and the other way
        /// round (issue #124), persisted under the existing <c>inspectorRailWidth</c> preference.
        /// <para>
        /// <b>This tab binds <see cref="InspectorRailWidthViewModel.Width"/>, never
        /// <see cref="InspectorRailWidthViewModel.EffectiveWidth"/>.</b> The 300 px floor is the
        /// macro panel's entitlement and there is no macro panel on this tab; inheriting it would
        /// widen this rail for a reason that does not exist here.
        /// </para>
        /// </summary>
        public InspectorRailWidthViewModel Rail { get; }

        /// <summary>
        /// The keys a colour applies to — the lighting board's own multi-selection, which is
        /// <b>not</b> the editor's single selection (see <see cref="LightingPaintSelection"/>).
        /// It is also what "Clear" empties (<see cref="ClearSelectionCommand"/>).
        /// </summary>
        public LightingPaintSelection Selection { get; }

        /// <summary>The shared color picker; its color is what the selection or a zone paints with (§4).</summary>
        public ColorPickerViewModel Picker { get; }

        /// <summary>Switches the tab to another layer, re-reading every control from it (§4).</summary>
        public IRelayCommand<LightingLayerViewModel> SelectLayerCommand { get; }

        /// <summary>Sets the selected layer's mode — what a click on a rail row runs.</summary>
        public IRelayCommand<LightingModeViewModel> SelectModeCommand { get; }

        /// <summary>Points the picker at one of the two color swatches.</summary>
        public IRelayCommand<LightingColorSlotViewModel> SelectColorSlotCommand { get; }

        /// <summary>Sets the selected layer's direction; an unavailable arrow is a no-op.</summary>
        public IRelayCommand<LightingDirectionViewModel> SelectDirectionCommand { get; }

        /// <summary>Sets the speed from one of the nine bars.</summary>
        public IRelayCommand<int> SetSpeedCommand { get; }

        /// <summary>
        /// Adds a zone's keys to the paint selection, or takes them back out (§4), and paints
        /// whatever it added — a <b>direct paint gesture</b>, see
        /// <see cref="PaintWhatTheGestureSelects"/>.
        /// <para>
        /// It acts on <b>its own keys and nothing else</b>, and the button it runs from shows no
        /// state at all (issue #131) — see <see cref="LightingZoneViewModel"/> for why a latch
        /// cannot be honest on a family of overlapping zones.
        /// </para>
        /// </summary>
        public IRelayCommand<LightingZoneViewModel> SelectZoneCommand { get; }

        /// <summary>
        /// Adds a key to the paint selection or takes it out — what a click on a cap runs, and a
        /// direct paint gesture (<see cref="PaintWhatTheGestureSelects"/>).
        /// </summary>
        public IRelayCommand<KeyboardKeyViewModel> SelectKeyCommand { get; }

        /// <summary>
        /// Extends the paint selection to a key — what a shift-click on a cap runs, and a direct
        /// paint gesture (<see cref="PaintWhatTheGestureSelects"/>).
        /// </summary>
        public IRelayCommand<KeyboardKeyViewModel> ExtendSelectionCommand { get; }

        /// <summary>
        /// Selects every key of the layer (mockup 2f's "Select all"). It is deliberately
        /// <b>not</b> a paint gesture — see <see cref="PaintWhatTheGestureSelects"/>.
        /// </summary>
        public IRelayCommand SelectAllKeysCommand { get; }

        /// <summary>
        /// Empties the paint selection — mockup 2f's "Clear", the button beside "Select all"
        /// (issue #131).
        /// <para>
        /// <b>It used to paint the selected keys black instead</b>, which is a real operation but
        /// not the one its caption promises next to a bulk <i>selector</i> — and it was a visible
        /// no-op in most of the states a user presses it in: with nothing selected, over keys that
        /// carry no colour, and under Off/Pitch Black, where the paint layer is drawn at 0 %. The
        /// user's report was simply "the Clear button does nothing". Painting a selection off is
        /// still reachable — pick black in the picker, which is <c>SetKeyColor</c>'s own erase
        /// (§2.1) — and erasing the whole layer is still <see cref="ResetAllCommand"/>.
        /// </para>
        /// <para>
        /// It is gated on <see cref="LightingPaintSelection.HasSelection"/>: a button that empties
        /// an already empty selection is the very thing this command was rebound to stop being.
        /// </para>
        /// </summary>
        public IRelayCommand ClearSelectionCommand { get; }

        /// <summary>
        /// Paints the picker's current color onto the <b>whole</b> selection, and announces it once.
        /// <para>
        /// <b>It has no button any more</b> (issue #128). It was the rail footer's <c>Apply</c>,
        /// the commit of the select-then-apply flow #124 introduced; every control on this rail
        /// writes on the spot now — including a selection gesture, which paints what it adds (see
        /// <see cref="PaintWhatTheGestureSelects"/>) — so the one thing Apply could do that nothing
        /// else could is done by the gesture that used to need it.
        /// </para>
        /// <para>
        /// The command survives the button because it is still the honest name for "put this colour
        /// on everything selected". It stays enabled exactly while something is selected.
        /// </para>
        /// </summary>
        public IRelayCommand PaintSelectionCommand { get; }

        /// <summary>Erases every per-key color of the layer, after the §4 confirmation.</summary>
        public IAsyncRelayCommand ResetAllCommand { get; }

        /// <summary>
        /// Raised after every write into the profile's <see cref="LightingModel"/>. This panel has
        /// no save path of its own — <c>ProfileSession.Save</c> serializes whatever the session's
        /// Lighting holds — so a lighting edit still makes the <b>session</b> dirty, and the
        /// editor's Save has to turn amber for it. Core's model announces nothing, so the write
        /// sites say so here.
        /// <para>
        /// It may fire for a write that changed nothing (re-reading a layer assigns the speed it
        /// just read). That is harmless: the consumer re-asks
        /// <c>IProfileSession.IsDirty</c>, which compares serialized lines rather than trusting the
        /// notification.
        /// </para>
        /// </summary>
        public event EventHandler? ModelChanged;

        private readonly DeviceSnapshot _device;
        private readonly INotificationService _notifications;
        private readonly IMotionSettings? _motionSettings;
        private readonly LightingBoardPreview _preview = new();
        private IReadOnlyList<LightingLayerViewModel> _layers = [];
        private IReadOnlyList<LightingDirectionViewModel> _directions = [];
        private LightingModeParameters _parameters = LightingModeParameters.None;
        private LightingLayerViewModel? _selectedLayer;
        private LightingModeViewModel? _selectedModeOption;
        private KeyboardLayerViewModel? _board;
        private LightingModel? _model;
        private KeyboardLayer? _topLayoutLayer;
        private LightingMode _selectedMode = LightingMode.Disabled;
        private int _speed = LayerLightingState.DefaultSpeed;
        private bool _isPreviewAnimating;
        private bool _isSynchronizing;

        /// <summary>
        /// Creates the panel for <paramref name="device"/>. Construction touches no file and needs
        /// no profile — the menus come from the catalogs — so the editor builds it eagerly and
        /// hands the model over in <see cref="Attach"/> once the profile has been read.
        /// </summary>
        /// <param name="device">The board this panel edits.</param>
        /// <param name="notifications">The app-wide notification surface.</param>
        /// <param name="preferences">
        /// The session's <c>app_settings.txt</c>, which is where the picker's twelve custom slots
        /// live. <b>Required, and deliberately not defaulted</b>: a defaulted
        /// <see cref="NullAppPreferencesStore"/> would let a call site forget the store and lose
        /// every swatch silently, with no test able to see it. A host with no drive passes
        /// <see cref="NullAppPreferencesStore.Instance"/> in as many words.
        /// </param>
        /// <param name="motionSettings">
        /// The app's live motion switch, polled by <see cref="AdvancePreview"/>. Optional for the
        /// same reason the editor's session is — a panel built for a unit test or a design scene
        /// has no app around it — and null then means "animate", which is the state the design was
        /// drawn in. The shell passes the app's own instance, and only then is the Settings
        /// screen's reduce-motion preference live on this board.
        /// </param>
        /// <param name="rail">
        /// The editor's one rail width (issue #124). Optional, and null builds a store-less one of
        /// its own: a panel built for a unit test or a design scene has no editor around it to share
        /// with, and a rail that drags and forgets is the right degradation — the same shape the
        /// editor's own <c>IHostPreferencesStore</c> already has.
        /// </param>
        public LightingTabViewModel(
            DeviceSnapshot device,
            INotificationService notifications,
            IAppPreferencesStore preferences,
            IMotionSettings? motionSettings = null,
            InspectorRailWidthViewModel? rail = null)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _motionSettings = motionSettings;

            ArgumentNullException.ThrowIfNull(preferences);

            Rail = rail ?? new InspectorRailWidthViewModel();

            IsAvailable = IsSupported(device.Device);
            IsLayerCustomizationAvailable = LightingAvailability.IsFnLayerLightingAvailable(
                device.DeviceId,
                device.Firmware);

            StatusMessage = device.IsDemoMode || device.Location is null ? DemoModeHint : string.Empty;

            Modes = LightingModeViewModel.CreateAll(device.DeviceId, device.Firmware, IsLayerCustomizationAvailable);
            Zones = LightingZoneViewModel.CreateAll(device.DeviceId);

            EffectColor = LightingColorSlotViewModel.CreateEffectColor();
            BaseColor = LightingColorSlotViewModel.CreateBaseColor();
            SpeedControl = new LightingSpeedViewModel();
            Selection = new LightingPaintSelection();

            Picker = new ColorPickerViewModel(device, preferences);
            Picker.ColorChanged += OnPickerColorChanged;

            _isPreviewAnimating = !(motionSettings?.ReduceMotion ?? false);

            Directions = LightingDirectionViewModel.CreateAll(_parameters);

            SelectLayerCommand = new RelayCommand<LightingLayerViewModel>(SelectLayer);
            SelectModeCommand = new RelayCommand<LightingModeViewModel>(SelectMode);
            SelectColorSlotCommand = new RelayCommand<LightingColorSlotViewModel>(SelectColorSlot);
            SelectDirectionCommand = new RelayCommand<LightingDirectionViewModel>(SelectDirection);
            SetSpeedCommand = new RelayCommand<int>(speed => Speed = speed, _ => Parameters.AcceptsSpeed);

            // NONE OF THE PAINT COMMANDS IS GATED ON THE MODE. The painted colours belong to the
            // layer, not to the effect running over them (mockup 2f: "the colors are still on
            // file"), so the controls that manage them are reachable whenever this tab is —
            // which is already "this board has per-key RGB", because that is what puts the tab on
            // screen at all (IsSupported). What the mode decides is how the paint is *drawn*:
            // directly, or at 40% under the effect (LightingModeParameters.RendersPaintDirectly).
            //
            // PaintSelectionCommand's CanExecute below is not an exception to that: it asks about
            // the SELECTION, a fact about what the user pointed at, not about the effect running.
            //
            // THE FIRST THREE ARE PAINT GESTURES AND `Select all` IS NOT. See
            // PaintWhatTheGestureSelects: pointing at keys on the board is how a colour lands now
            // that the Apply button is gone, and a bulk selector must never be one.
            SelectZoneCommand = new RelayCommand<LightingZoneViewModel>(
                zone => PaintWhatTheGestureSelects(() => SelectZone(zone)));
            SelectKeyCommand = new RelayCommand<KeyboardKeyViewModel>(
                key => PaintWhatTheGestureSelects(() => Selection.Toggle(key)));
            ExtendSelectionCommand = new RelayCommand<KeyboardKeyViewModel>(
                key => PaintWhatTheGestureSelects(() => Selection.Extend(key)));
            SelectAllKeysCommand = new RelayCommand(Selection.SelectAll);
            // `Clear` is the other half of `Select all`, so it moves the SELECTION and not the
            // paint (issue #131). Same gate as PaintSelectionCommand's, and for the same reason.
            ClearSelectionCommand = new RelayCommand(Selection.Clear, () => Selection.HasSelection);
            // The one gate is the selection, not the mode: painting nothing is not a paint, and a
            // control that claims otherwise is a lie the user only finds out about by pressing it.
            PaintSelectionCommand = new RelayCommand(() => PaintSelection(Picker.Color), () => Selection.HasSelection);
            ResetAllCommand = new AsyncRelayCommand(ResetAllAsync);

            // ONE SUBSCRIPTION, NOT A LIST OF CALL SITES, and subscribed after the commands exist
            // because it re-asks two of them. Both gates are functions of the whole selection, and
            // the selection moves from six places (a click, a shift-click, "Select all", a zone, an
            // emptying, a layer switch). Hanging them on the selection's own notification is what
            // makes "they always agree with the board" true by construction rather than by a list
            // somebody has to keep complete — the same reasoning as RefreshLegend on the Keys tab.
            // The selection is this panel's own object and dies with it, so nothing detaches this.
            //
            // WHAT IS NO LONGER HUNG HERE is the zone buttons' selected state (issue #131). It was
            // the third consumer, and a derived one — "every key of this zone is selected" — over a
            // family of OVERLAPPING zones, so one button's click silently repainted another's face.
            // See LightingZoneViewModel.
            Selection.Changed += OnSelectionChanged;
        }

        /// <summary>
        /// Points the panel at the profile's lighting model and at the keyboard pictures the
        /// editor built. <paramref name="lighting"/> is <c>IProfileSession.Lighting</c>: the RGB is
        /// exactly the case where it is a plain <see cref="LightingModel"/>. Anything else — demo
        /// mode, a load that failed, another device's model — falls back to an <b>in-memory</b>
        /// model so the tab stays explorable; nothing is written, because Save is already
        /// unavailable in those cases (03 §3.5).
        /// </summary>
        public void Attach(object? lighting, IReadOnlyList<KeyboardLayerViewModel> layers)
        {
            ArgumentNullException.ThrowIfNull(layers);

            if (!IsAvailable)
            {
                return;
            }

            _model = lighting as LightingModel ?? new LightingModel();
            _topLayoutLayer = layers.Count > 0 ? layers[0].Layer : null;

            Layers = BuildLayers(layers);

            SelectLayer(Layers.Count > 0 ? Layers[0] : null);
        }

        /// <summary>Reads the picker's stored custom colors; total and idempotent.</summary>
        public Task LoadAsync()
        {
            return IsAvailable ? Picker.LoadAsync() : Task.CompletedTask;
        }

        /// <summary>
        /// Moves the preview on by <paramref name="deltaSeconds"/> and re-draws the board. It is
        /// the <b>only</b> entry point the view's frame timer calls, and the only place the elapsed
        /// clock moves.
        /// <para>
        /// <b>Reduce-motion is read here, on every call.</b> Since issue #96 it is a live user
        /// preference — the Settings screen can flip it while this editor is open — and
        /// <see cref="IMotionSettings"/> raises no notification, so polling once a frame is the
        /// mechanism that makes it live. While it is set the clock is held at zero and the board
        /// shows the mode's <c>t = 0</c> frame: a frozen picture of the effect rather than a blank
        /// board, because the point of this screen is to show what the mode looks like.
        /// </para>
        /// </summary>
        public void AdvancePreview(double deltaSeconds)
        {
            if (!IsAvailable)
            {
                return;
            }

            var isFrozen = _motionSettings?.ReduceMotion ?? false;

            IsPreviewAnimating = !isFrozen;

            _preview.Advance(deltaSeconds, isFrozen);
        }

        private IReadOnlyList<LightingLayerViewModel> BuildLayers(IReadOnlyList<KeyboardLayerViewModel> boards)
        {
            // A led file describes exactly two layers (§1.5); the picture may have more (an
            // Advantage 360 has five), so the tab shows the two it can address and no more.
            var states = new[] { _model!.TopLayer, _model.FnLayer };
            var layers = new List<LightingLayerViewModel>(states.Length);

            for (var index = 0; index < states.Length; index++)
            {
                var board = index < boards.Count ? boards[index] : null;

                layers.Add(new LightingLayerViewModel(
                    index,
                    board?.Caption ?? (index == 0 ? LayerCaptions.TopLayerCaption : LayerCaptions.FnLayerCaption),
                    states[index],
                    board,
                    isEnabled: index == 0 || IsLayerCustomizationAvailable));
            }

            return layers;
        }

        /// <summary>
        /// Switches layers. Every control is re-read from the newly active layer, because the two
        /// layers are fully independent (§4 "Switching layers in lighting mode re-reads all
        /// controls from the newly active layer's values") — and the paint selection is emptied,
        /// because a selection is a set of positions on one layer.
        /// </summary>
        private void SelectLayer(LightingLayerViewModel? layer)
        {
            if (layer is not null && !layer.IsEnabled)
            {
                return;
            }

            // RE-SELECTING THE LAYER ALREADY OPEN IS NOT A LAYER CHANGE, and the difference is the
            // paint selection: SetLayer below clears it, which is right when the user moves from
            // Top to Fn (the keys are a different layer's) and wrong every other time this runs.
            //
            // It runs a lot. The switcher is a ListBox, and a ListBox raises SelectionChanged while
            // it is binding — so simply SHOWING the tab re-asserted the open layer and wiped the
            // selection before the first frame. Picking keys, leaving for the Keys tab and coming
            // back did it too, because the tab is hidden rather than unloaded and is re-shown.
            // Both were silent: the caption read "Paint · no keys selected" over a board whose caps
            // had just been cleared, and every view-model test passed because none of them went
            // through the view. A captured frame is what showed it.
            if (ReferenceEquals(layer, SelectedLayer))
            {
                return;
            }

            foreach (var entry in Layers)
            {
                entry.IsSelected = ReferenceEquals(entry, layer);
            }

            SelectedLayer = layer;
            Board = layer?.Board;

            Selection.SetLayer(Board?.Keys);
            _preview.SetLayer(Board, layer?.State);

            ReadFromState();
        }

        private void ReadFromState()
        {
            var state = SelectedLayer?.State;

            _isSynchronizing = true;

            try
            {
                SelectedMode = state?.Mode ?? LightingMode.Disabled;
                SelectedModeOption = FindMode(SelectedMode);

                EffectColor.ReadFrom(state);
                BaseColor.ReadFrom(state);

                var speed = Math.Clamp(state?.Speed ?? LayerLightingState.DefaultSpeed, MinimumSpeed, MaximumSpeed);

                SpeedControl.Show(speed);
                SetProperty(ref _speed, speed, nameof(Speed));

                RefreshParameters();
            }
            finally
            {
                _isSynchronizing = false;
            }

            RefreshBoard();
        }

        private void SelectMode(LightingModeViewModel? mode)
        {
            var state = SelectedLayer?.State;

            if (mode is null || state is null)
            {
                return;
            }

            // RE-SELECTING THE MODE ALREADY OPEN IS NOT A MODE CHANGE, and the guard is load
            // bearing for exactly the reason SelectLayer's is: the picker is a ComboBox since issue
            // #128, and a selector raises SelectionChanged while it BINDS. Without this, merely
            // showing the tab — or coming back to it, since it is hidden rather than unloaded —
            // ran a write and raised ModelChanged, which turns the editor's Save amber over a
            // profile nobody edited (invariant 16). Nothing else on this panel could have noticed:
            // the mode written is the mode that was already there.
            if (ReferenceEquals(mode, SelectedModeOption))
            {
                return;
            }

            state.Mode = mode.Mode;
            SelectedMode = mode.Mode;
            SelectedModeOption = mode;

            _isSynchronizing = true;

            try
            {
                RefreshParameters();
            }
            finally
            {
                _isSynchronizing = false;
            }

            // The selection deliberately survives: picking a mode is how the user finds out what a
            // set of keys looks like under it, and having to re-select them each time would make
            // the rail unusable for exactly the comparison it exists for.
            RefreshBoard();
            RaiseModelChanged();
        }

        /// <summary>
        /// Recomputes what the selected mode accepts, re-labels and re-explains the two colour
        /// swatches for it, rebuilds the direction arrows, and makes sure the picker is pointed at
        /// a swatch the mode actually has.
        /// </summary>
        private void RefreshParameters()
        {
            Parameters = LightingModeParameters.For(_device.DeviceId, SelectedMode, IsLayerCustomizationAvailable);

            EffectColor.IsVisible = Parameters.AcceptsEffectColor;
            BaseColor.IsVisible = Parameters.AcceptsBaseColor;

            // The swatch is renamed as well as explained: in a per-key mode it is not an effect
            // colour at all (07 §2.2 writes no such line for Freestyle/Breathe/Frozen Wave), and
            // "Effect Color" there names a line the file will never carry.
            EffectColor.Caption = LightingColorSlotViewModel.EffectCaptionFor(SelectedMode);
            EffectColor.Hint = LightingHintCatalog.ForEffectColor(SelectedMode);
            BaseColor.Hint = LightingHintCatalog.ForBaseColor(SelectedMode);

            RefreshDirections();
            RefreshSelectedColorSlot();
        }

        /// <summary>The row of <see cref="Modes"/> for <paramref name="mode"/>, or null (see
        /// <see cref="SelectedModeOption"/> for when that happens).</summary>
        private LightingModeViewModel? FindMode(LightingMode mode)
        {
            foreach (var entry in Modes)
            {
                if (entry.Mode == mode)
                {
                    return entry;
                }
            }

            return null;
        }

        private void RefreshDirections()
        {
            Directions = LightingDirectionViewModel.CreateAll(Parameters);

            var state = SelectedLayer?.State;

            if (!Parameters.AcceptsDirection || state is null)
            {
                return;
            }

            var current = FindDirection(state.Direction) ?? FirstAvailableDirection();

            if (current is null)
            {
                return;
            }

            // A direction the mode does not accept would be written as the default anyway
            // (specs/07-lighting.md §2.4 item 5), so the control and the file are kept in step.
            //
            // THIS IS A WRITE INTO THE PROFILE'S MODEL, and it is announced like the other seven.
            // Rebound offers only Left and Up, so a layer whose file carries Down normalizes the
            // moment it is shown — a mode pick, a layer switch, or the load itself — and without
            // the notification the session would be dirty with a grey Save (invariant 16).
            var hasNormalized = state.Direction != current.Direction;

            state.Direction = current.Direction;

            foreach (var entry in Directions)
            {
                entry.IsSelected = ReferenceEquals(entry, current);
            }

            if (hasNormalized)
            {
                RaiseModelChanged();
            }
        }

        private LightingDirectionViewModel? FindDirection(LightingDirection direction)
        {
            foreach (var entry in Directions)
            {
                if (entry.IsAvailable && entry.Direction == direction)
                {
                    return entry;
                }
            }

            return null;
        }

        private LightingDirectionViewModel? FirstAvailableDirection()
        {
            foreach (var entry in Directions)
            {
                if (entry.IsAvailable)
                {
                    return entry;
                }
            }

            return null;
        }

        private void RefreshSelectedColorSlot()
        {
            var selected = EffectColor.IsSelected && EffectColor.IsVisible ? EffectColor
                : BaseColor.IsSelected && BaseColor.IsVisible ? BaseColor
                : EffectColor.IsVisible ? EffectColor
                : BaseColor.IsVisible ? BaseColor
                : null;

            SelectColorSlot(selected);
        }

        private void SelectColorSlot(LightingColorSlotViewModel? slot)
        {
            if (slot is not null && !slot.IsVisible)
            {
                return;
            }

            EffectColor.IsSelected = ReferenceEquals(slot, EffectColor);
            BaseColor.IsSelected = ReferenceEquals(slot, BaseColor);

            if (slot is null)
            {
                return;
            }

            var wasSynchronizing = _isSynchronizing;

            _isSynchronizing = true;

            try
            {
                Picker.Color = slot.Color;
            }
            finally
            {
                _isSynchronizing = wasSynchronizing;
            }
        }

        /// <summary>
        /// The picker moved: the selected swatch follows it into the model, and — in a per-key mode
        /// — so does every selected key. Both writes are announced <b>once</b>, because they are
        /// one gesture. While the tab is re-reading a layer the flow is the other way round, so the
        /// write is suppressed.
        /// </summary>
        private void OnPickerColorChanged(LedColor color)
        {
            if (_isSynchronizing)
            {
                return;
            }

            var state = SelectedLayer?.State;

            if (EffectColor.IsSelected)
            {
                EffectColor.Assign(state, color);
            }
            else if (BaseColor.IsSelected)
            {
                BaseColor.Assign(state, color);
            }

            PaintSelectedKeys(color);

            RefreshBoard();
            RaiseModelChanged();
        }

        private void SelectDirection(LightingDirectionViewModel? direction)
        {
            var state = SelectedLayer?.State;

            // An unavailable arrow is not drawn at all since issue #128, so this is the second line
            // rather than the only one — but a mode that cannot run that way must not have it
            // written, whatever reaches the command.
            if (direction is null || state is null || !direction.IsAvailable)
            {
                return;
            }

            state.Direction = direction.Direction;

            foreach (var entry in Directions)
            {
                entry.IsSelected = ReferenceEquals(entry, direction);
            }

            RefreshBoard();
            RaiseModelChanged();
        }

        /// <summary>
        /// Paints the whole paint selection with <paramref name="color"/> and announces it once.
        /// Keys are addressed by <b>memory key code</b> (§4), and black clears the key rather than
        /// storing it — that is <see cref="LayerLightingState.SetKeyColor"/>'s contract (§2.1),
        /// honoured rather than worked around, which is also what makes the black swatch an eraser.
        /// </summary>
        private void PaintSelection(LedColor color)
        {
            if (!PaintSelectedKeys(color))
            {
                return;
            }

            RefreshBoard();
            RaiseModelChanged();
        }

        private bool PaintSelectedKeys(LedColor color)
        {
            return PaintKeys(Selection.Keys, color);
        }

        /// <summary>
        /// Writes <paramref name="color"/> onto <paramref name="keys"/> and answers whether
        /// anything was written. Keys are addressed by <b>memory key code</b> (§4).
        /// </summary>
        private bool PaintKeys(IReadOnlyList<KeyboardKeyViewModel> keys, LedColor color)
        {
            var state = SelectedLayer?.State;

            if (state is null || keys.Count == 0)
            {
                return false;
            }

            foreach (var key in keys)
            {
                state.SetKeyColor(key.Key.OriginalKey.Code, color);
            }

            return true;
        }

        /// <summary>
        /// Runs a <b>direct paint gesture on the board</b> — a click on a cap, a shift-click run, a
        /// zone button — and paints the picker's current colour onto whatever that gesture <i>added
        /// to</i> the selection.
        /// <para>
        /// It is what replaced the rail footer's <c>Apply</c> in issue #128. Apply existed for one
        /// flow: pushing an already-held colour onto a selection made <em>after</em> the colour was
        /// chosen, which the picker cannot do because it only writes on <c>ColorChanged</c>. With
        /// every other control on the rail applying on the spot, a button whose whole job was
        /// "commit, now" was the one thing left asking to be pressed.
        /// </para>
        /// <para>
        /// <b>It is emphatically not "paint on every selection change".</b> `Select all` plus a
        /// held colour would then repaint the entire layer in a single click with nothing but
        /// <c>Reset All</c> to undo it — which is precisely the regression issue #124 removed when
        /// <c>ApplyZoneCommand</c> became <c>SelectZoneCommand</c>. So this is wired to the three
        /// commands that are a user pointing at keys, and <see cref="SelectAllKeysCommand"/> is
        /// deliberately not one of them.
        /// </para>
        /// <para>
        /// <b>Only what the gesture added is painted</b>, never the whole selection: a click that
        /// grows a selection must not re-colour the keys already in it, or `Clear` followed by one
        /// more click would silently undo the Clear.
        /// </para>
        /// </summary>
        private void PaintWhatTheGestureSelects(Action gesture)
        {
            var before = new HashSet<KeyboardKeyViewModel>(Selection.Keys);

            gesture();

            var added = new List<KeyboardKeyViewModel>(Selection.Count);

            foreach (var key in Selection.Keys)
            {
                if (!before.Contains(key))
                {
                    added.Add(key);
                }
            }

            if (!PaintKeys(added, Picker.Color))
            {
                return;
            }

            RefreshBoard();
            RaiseModelChanged();
        }

        /// <summary>
        /// Adds a whole zone to the paint selection, or subtracts it. Since issue #124 it selects
        /// rather than paints, and since #128 the <i>gesture</i> around it paints what it selected —
        /// see <see cref="PaintWhatTheGestureSelects"/>, which is the only caller. Nothing about the
        /// zones' membership has changed through any of it.
        /// <para>
        /// <b>Plain addition and subtraction over this zone's own keys</b> (issue #131): any of them
        /// still unselected means the whole zone goes in, all of them already selected means the
        /// whole zone comes out. The question is asked of <i>these</i> key codes and answered from
        /// the selection alone, so nothing another button did can change what this one does next —
        /// which is exactly what the removed <c>IsSelected</c> latch could not promise, the zones
        /// being nested (<c>All ⊃ Left Module ⊃ Game ⊃ WASD</c>).
        /// </para>
        /// <para>
        /// Zone membership is still authored against the <b>top layer</b>, so on the Fn layer every
        /// code is re-resolved to the same physical position first (§2.4 item 6: "Fn-layer per-key
        /// lines address keys by top-layer position; the color is applied to the same physical key
        /// on the Fn layer").
        /// </para>
        /// </summary>
        private void SelectZone(LightingZoneViewModel? zone)
        {
            if (zone is null || SelectedLayer is null)
            {
                return;
            }

            var keyCodes = ResolveZoneKeyCodes(zone);

            if (Selection.ContainsAll(keyCodes))
            {
                Selection.DeselectByKeyCode(keyCodes);
            }
            else
            {
                Selection.SelectByKeyCode(keyCodes);
            }
        }

        /// <summary>
        /// The zone's key codes as the <b>shown layer</b> addresses them — the one place the §2.4
        /// item 6 resolution is applied to a zone, so the set a click adds and the set the next
        /// click subtracts can never disagree about which keys the zone means.
        /// </summary>
        private List<int> ResolveZoneKeyCodes(LightingZoneViewModel zone)
        {
            var keyCodes = new List<int>(zone.KeyCodes.Count);

            foreach (var topLayerKeyCode in zone.KeyCodes)
            {
                if (ResolveKeyCode(topLayerKeyCode) is { } keyCode)
                {
                    keyCodes.Add(keyCode);
                }
            }

            return keyCodes;
        }

        /// <summary>
        /// The selection moved, from wherever. The two commands whose gate <i>is</i> the selection
        /// re-ask it: <see cref="PaintSelectionCommand"/> and <see cref="ClearSelectionCommand"/>
        /// are live exactly while something is selected.
        /// <para>
        /// <b>Nothing here recomputes a zone button</b> any more (issue #131). The buttons are
        /// stateless, which is what stops one of them from changing another's appearance.
        /// </para>
        /// </summary>
        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            PaintSelectionCommand.NotifyCanExecuteChanged();
            ClearSelectionCommand.NotifyCanExecuteChanged();
        }

        private int? ResolveKeyCode(int topLayerKeyCode)
        {
            var board = Board;

            if (SelectedLayer is null || SelectedLayer.Index == 0 || board is null || _topLayoutLayer is null)
            {
                return topLayerKeyCode;
            }

            var topKey = _topLayoutLayer.FindByOriginalKeyCode(topLayerKeyCode);

            return topKey is null ? null : board.FindByIndex(topKey.Index)?.Key.OriginalKey.Code;
        }

        /// <summary>
        /// Erases every per-key color of the layer after the confirmation of
        /// specs/07-lighting.md §4. The prompt is the spec's wording verbatim.
        /// </summary>
        private async Task ResetAllAsync()
        {
            var state = SelectedLayer?.State;

            if (state is null)
            {
                return;
            }

            MessageBoxOutcome outcome;

            try
            {
                outcome = await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = ResetAllTitle,
                    Message = ResetAllConfirmation,
                    Icon = MessageBoxIcon.Confirmation,
                    Buttons = MessageBoxButtons.YesNo,
                    YesCaption = ResetAllConfirmCaption,
                    NoCaption = ResetAllDeclineCaption
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // A confirmation that cannot be put on screen must not erase anything, and must
                // not bring the app down either.
                return;
            }

            if (outcome.Result != MessageBoxResult.Yes)
            {
                return;
            }

            state.ClearKeyColors();

            RefreshBoard();
            RaiseModelChanged();
        }

        /// <summary>
        /// Re-draws the board at the preview's current instant. Core's model announces nothing
        /// (docs/app/keyboard-editor.md, invariant 3), so every write on this panel ends here —
        /// including while the clock is frozen, so that a mode or a colour picked under
        /// reduce-motion still shows.
        /// <para>
        /// The Keys tab shares these very cap view models, so the colours land on its caps too —
        /// but its picture draws no lighting at all (<c>KeyboardView.ShowsLighting</c>), so nothing
        /// of this is visible there.
        /// </para>
        /// </summary>
        private void RefreshBoard()
        {
            _preview.Draw();
        }

        /// <summary>
        /// Announces a write into the profile's lighting model. Every one of the write sites on
        /// this panel ends here — the mode, the speed, the direction, either colour swatch, the
        /// painted selection, "Reset All", and the direction <b>normalization</b> of
        /// <see cref="RefreshDirections"/> — because <see cref="ModelChanged"/> is what turns the
        /// editor's Save amber.
        /// <para>
        /// Since issue #128 a <b>selection gesture on the board</b> is among them — a cap click, a
        /// shift-click run or a zone button paints what it added (<see cref="PaintWhatTheGestureSelects"/>)
        /// — while `Select all` still is not, because it paints nothing.
        /// </para>
        /// </summary>
        private void RaiseModelChanged()
        {
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Re-asks the one command whose availability the mode decides. The paint commands are
        /// deliberately absent: none of them carries a gate the <i>mode</i> can move, because the
        /// colours they manage are the layer's rather than the effect's.
        /// <see cref="PaintSelectionCommand"/>'s own gate is the selection, and it is re-asked from
        /// <see cref="OnSelectionChanged"/> instead.
        /// </summary>
        private void NotifyCommands()
        {
            SetSpeedCommand.NotifyCanExecuteChanged();
        }
    }
}
