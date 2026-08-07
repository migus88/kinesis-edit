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
    /// redesigned around design mockup 2f — <b>the mode is rendered on the board</b> rather than
    /// named in a dropdown. A mode rail beside the board, the board animating the selected mode at
    /// its real speed and direction, and under it the paint selection the colour picker applies to.
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

        /// <summary>The rail's own header (mockup 2f), verbatim.</summary>
        public const string ModeRailCaption = "Mode — click to preview on the board";

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

        /// <summary>The device's mode rail for its firmware (§3, mockup 2f).</summary>
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
                    NotifyCommands();
                }
            }
        }

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
        /// The four direction arrows — always four, each carrying
        /// <see cref="LightingDirectionViewModel.IsAvailable"/>, because 2f keeps the ones a mode
        /// cannot use in place and strikes them through.
        /// </summary>
        public IReadOnlyList<LightingDirectionViewModel> Directions
        {
            get => _directions;
            private set => SetProperty(ref _directions, value);
        }

        /// <summary>The device's zone buttons (§4), built once.</summary>
        public IReadOnlyList<LightingZoneViewModel> Zones { get; }

        /// <summary>
        /// The keys a colour, or a Clear, applies to — the lighting board's own multi-selection,
        /// which is <b>not</b> the editor's single selection (see
        /// <see cref="LightingPaintSelection"/>).
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

        /// <summary>Paints the picker's color onto every key of a zone (§4).</summary>
        public IRelayCommand<LightingZoneViewModel> ApplyZoneCommand { get; }

        /// <summary>Adds a key to the paint selection or takes it out — what a click on a cap runs.</summary>
        public IRelayCommand<KeyboardKeyViewModel> SelectKeyCommand { get; }

        /// <summary>Extends the paint selection to a key — what a shift-click on a cap runs.</summary>
        public IRelayCommand<KeyboardKeyViewModel> ExtendSelectionCommand { get; }

        /// <summary>Selects every key of the layer (mockup 2f's "Select all").</summary>
        public IRelayCommand SelectAllKeysCommand { get; }

        /// <summary>
        /// Turns the selected keys off — mockup 2f's "Clear". They go hatched, because off is
        /// hatched and never black.
        /// </summary>
        public IRelayCommand ClearKeyColorsCommand { get; }

        /// <summary>
        /// Paints the picker's current color onto the whole selection. Choosing a colour does this
        /// on its own; the command is what re-applies the colour already in the picker to a
        /// selection made afterwards.
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
        public LightingTabViewModel(
            DeviceSnapshot device,
            INotificationService notifications,
            IAppPreferencesStore preferences,
            IMotionSettings? motionSettings = null)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _motionSettings = motionSettings;

            ArgumentNullException.ThrowIfNull(preferences);

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
            ApplyZoneCommand = new RelayCommand<LightingZoneViewModel>(ApplyZone);
            SelectKeyCommand = new RelayCommand<KeyboardKeyViewModel>(Selection.Toggle);
            ExtendSelectionCommand = new RelayCommand<KeyboardKeyViewModel>(Selection.Extend);
            SelectAllKeysCommand = new RelayCommand(Selection.SelectAll);
            ClearKeyColorsCommand = new RelayCommand(ClearKeyColors);
            PaintSelectionCommand = new RelayCommand(() => PaintSelection(Picker.Color));
            ResetAllCommand = new AsyncRelayCommand(ResetAllAsync);
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

                foreach (var mode in Modes)
                {
                    mode.IsSelected = mode.Mode == SelectedMode;
                }

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

            state.Mode = mode.Mode;
            SelectedMode = mode.Mode;

            foreach (var entry in Modes)
            {
                entry.IsSelected = ReferenceEquals(entry, mode);
            }

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
        /// Recomputes what the selected mode accepts, rebuilds the direction arrows, and makes sure
        /// the picker is pointed at a swatch the mode actually has.
        /// </summary>
        private void RefreshParameters()
        {
            Parameters = LightingModeParameters.For(_device.DeviceId, SelectedMode, IsLayerCustomizationAvailable);

            EffectColor.IsVisible = Parameters.AcceptsEffectColor;
            BaseColor.IsVisible = Parameters.AcceptsBaseColor;

            RefreshDirections();
            RefreshSelectedColorSlot();
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

            // A struck-through arrow is not hit-testable, so this is the second line rather than
            // the only one — but a mode that cannot run that way must not have it written.
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

        /// <summary>
        /// Turns every selected key off — mockup 2f's "Clear". It goes through
        /// <see cref="LayerLightingState.SetKeyColor"/> with black rather than inventing a second
        /// erase path, because black <i>is</i> "no colour" (§2.1) and the map never holds it.
        /// </summary>
        private void ClearKeyColors()
        {
            PaintSelection(LedColor.Black);
        }

        private bool PaintSelectedKeys(LedColor color)
        {
            var state = SelectedLayer?.State;

            if (state is null || !Selection.HasSelection)
            {
                return false;
            }

            foreach (var key in Selection.Keys)
            {
                state.SetKeyColor(key.Key.OriginalKey.Code, color);
            }

            return true;
        }

        /// <summary>
        /// Paints a whole zone. Zone membership is authored against the <b>top layer</b>, so on
        /// the Fn layer every code is re-resolved to the same physical position first
        /// (§2.4 item 6: "Fn-layer per-key lines address keys by top-layer position; the color is
        /// applied to the same physical key on the Fn layer").
        /// </summary>
        private void ApplyZone(LightingZoneViewModel? zone)
        {
            var state = SelectedLayer?.State;

            if (zone is null || state is null)
            {
                return;
            }

            var color = Picker.Color;

            foreach (var topLayerKeyCode in zone.KeyCodes)
            {
                if (ResolveKeyCode(topLayerKeyCode) is { } keyCode)
                {
                    state.SetKeyColor(keyCode, color);
                }
            }

            RefreshBoard();
            RaiseModelChanged();
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
        /// painted selection, "Clear", a painted zone, "Reset All", and the direction
        /// <b>normalization</b> of <see cref="RefreshDirections"/> — because
        /// <see cref="ModelChanged"/> is what turns the editor's Save amber.
        /// </summary>
        private void RaiseModelChanged()
        {
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Re-asks the one command whose availability the mode decides. The paint commands are
        /// deliberately absent: they carry no gate at all, because the colours they manage are the
        /// layer's rather than the effect's.
        /// </summary>
        private void NotifyCommands()
        {
            SetSpeedCommand.NotifyCanExecuteChanged();
        }
    }
}
