using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's Lighting tab: the per-layer LED editor of specs/07-lighting.md §3 and §4 —
    /// mode menu, effect/base colors, speed, direction, per-key coloring, zone buttons and
    /// "Reset All" — over the profile's <see cref="LightingModel"/>.
    /// <para>
    /// It owns no lighting rules. Mode membership and firmware gating are
    /// <see cref="LightingAvailability"/>'s, the per-mode panel matrix is
    /// <see cref="LightingPanelVisibility"/>'s reading of <see cref="LightingModeCatalog"/>, the
    /// zones are <see cref="LightingZoneCatalog"/>'s, and the file is written by
    /// <c>ProfileSession.Save</c> — this panel mutates the model the session handed out, so the
    /// editor's existing Save persists lighting with no save path of its own.
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

        /// <summary>The note shown in demo mode (03 §3.5): everything is explorable, nothing is written.</summary>
        public const string DemoModeHint =
            "This device is open in demo mode, so lighting can be explored but not saved.";

        /// <summary>Why the Fn layer and the base-color swatch are unavailable (specs/07-lighting.md §3).</summary>
        public const string LayerCustomizationLockedHint =
            "Fn-layer lighting and per-effect base colors need LED firmware 1.0.44 or newer.";

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

        /// <summary>The keyboard picture of <see cref="SelectedLayer"/>, for per-key coloring.</summary>
        public KeyboardLayerViewModel? Board
        {
            get => _board;
            private set => SetProperty(ref _board, value);
        }

        /// <summary>The device's mode menu for its firmware (§3).</summary>
        public IReadOnlyList<LightingModeViewModel> Modes { get; }

        /// <summary>The selected layer's mode.</summary>
        public LightingMode SelectedMode
        {
            get => _selectedMode;
            private set => SetProperty(ref _selectedMode, value);
        }

        /// <summary>Which parameter panels the selected mode shows (§3).</summary>
        public LightingPanelVisibility Panels
        {
            get => _panels;
            private set
            {
                if (SetProperty(ref _panels, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>The effect-color swatch.</summary>
        public LightingColorSlotViewModel EffectColor { get; }

        /// <summary>The base-color swatch of the two-line effects.</summary>
        public LightingColorSlotViewModel BaseColor { get; }

        /// <summary>The effect speed, always inside <see cref="MinimumSpeed"/>..<see cref="MaximumSpeed"/>.</summary>
        public int Speed
        {
            get => _speed;
            set
            {
                var clamped = Math.Clamp(value, MinimumSpeed, MaximumSpeed);

                if (SetProperty(ref _speed, clamped) && SelectedLayer is not null)
                {
                    SelectedLayer.State.Speed = clamped;

                    RaiseModelChanged();
                }
            }
        }

        /// <summary>Lowest speed the knob offers (specs/07-lighting.md §2.1).</summary>
        public int MinimumSpeed => LayerLightingState.MinimumSpeed;

        /// <summary>Highest speed the knob offers (§2.1).</summary>
        public int MaximumSpeed => LayerLightingState.MaximumSpeed;

        /// <summary>The direction entries the selected mode offers, empty when it has no panel.</summary>
        public IReadOnlyList<LightingDirectionViewModel> Directions
        {
            get => _directions;
            private set => SetProperty(ref _directions, value);
        }

        /// <summary>The device's zone buttons (§4), built once.</summary>
        public IReadOnlyList<LightingZoneViewModel> Zones { get; }

        /// <summary>The shared color picker; its color is what a key click or a zone paints with (§4).</summary>
        public ColorPickerViewModel Picker { get; }

        /// <summary>Switches the tab to another layer, re-reading every control from it (§4).</summary>
        public IRelayCommand<LightingLayerViewModel> SelectLayerCommand { get; }

        /// <summary>Sets the selected layer's mode.</summary>
        public IRelayCommand<LightingModeViewModel> SelectModeCommand { get; }

        /// <summary>Points the picker at one of the two color swatches.</summary>
        public IRelayCommand<LightingColorSlotViewModel> SelectColorSlotCommand { get; }

        /// <summary>Sets the selected layer's direction.</summary>
        public IRelayCommand<LightingDirectionViewModel> SelectDirectionCommand { get; }

        /// <summary>Paints the picker's color onto every key of a zone (§4).</summary>
        public IRelayCommand<LightingZoneViewModel> ApplyZoneCommand { get; }

        /// <summary>Paints the picker's color onto one key — what a click on the picture runs (§4).</summary>
        public IRelayCommand<KeyboardKeyViewModel> AssignKeyColorCommand { get; }

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
        private IReadOnlyList<LightingLayerViewModel> _layers = [];
        private IReadOnlyList<LightingDirectionViewModel> _directions = [];
        private LightingPanelVisibility _panels = LightingPanelVisibility.None;
        private LightingLayerViewModel? _selectedLayer;
        private KeyboardLayerViewModel? _board;
        private LightingModel? _model;
        private KeyboardLayer? _topLayoutLayer;
        private LightingMode _selectedMode = LightingMode.Disabled;
        private int _speed = LayerLightingState.DefaultSpeed;
        private bool _isSynchronizing;

        /// <summary>
        /// Creates the panel for <paramref name="device"/>. Construction touches no file and needs
        /// no profile — the menus come from the catalogs — so the editor builds it eagerly and
        /// hands the model over in <see cref="Attach"/> once the profile has been read.
        /// </summary>
        public LightingTabViewModel(
            DeviceSnapshot device,
            ISettingsService settings,
            INotificationService notifications)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

            IsAvailable = IsSupported(device.Device);
            IsLayerCustomizationAvailable = LightingAvailability.IsFnLayerLightingAvailable(
                device.DeviceId,
                device.Firmware);

            StatusMessage = device.IsDemoMode || device.Location is null ? DemoModeHint : string.Empty;

            Modes = LightingModeViewModel.CreateAll(device.DeviceId, device.Firmware);
            Zones = LightingZoneViewModel.CreateAll(device.DeviceId);

            EffectColor = LightingColorSlotViewModel.CreateEffectColor();
            BaseColor = LightingColorSlotViewModel.CreateBaseColor();

            Picker = new ColorPickerViewModel(device, settings);
            Picker.ColorChanged += OnPickerColorChanged;

            SelectLayerCommand = new RelayCommand<LightingLayerViewModel>(SelectLayer);
            SelectModeCommand = new RelayCommand<LightingModeViewModel>(SelectMode);
            SelectColorSlotCommand = new RelayCommand<LightingColorSlotViewModel>(SelectColorSlot);
            SelectDirectionCommand = new RelayCommand<LightingDirectionViewModel>(SelectDirection);
            ApplyZoneCommand = new RelayCommand<LightingZoneViewModel>(ApplyZone, _ => Panels.ShowsZones);
            AssignKeyColorCommand = new RelayCommand<KeyboardKeyViewModel>(
                AssignKeyColor,
                _ => Panels.ShowsPerKeyColors);
            ResetAllCommand = new AsyncRelayCommand(ResetAllAsync, () => Panels.ShowsResetAll);
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
        /// controls from the newly active layer's values").
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

                SetProperty(ref _speed, Math.Clamp(state?.Speed ?? LayerLightingState.DefaultSpeed, MinimumSpeed, MaximumSpeed), nameof(Speed));

                RefreshPanels();
            }
            finally
            {
                _isSynchronizing = false;
            }

            RefreshOverlays();
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
                RefreshPanels();
            }
            finally
            {
                _isSynchronizing = false;
            }

            RaiseModelChanged();
        }

        /// <summary>
        /// Recomputes the per-mode panel matrix, rebuilds the direction entries, and makes sure
        /// the picker is pointed at a swatch the mode actually shows.
        /// </summary>
        private void RefreshPanels()
        {
            Panels = LightingPanelVisibility.For(_device.DeviceId, SelectedMode, IsLayerCustomizationAvailable);

            EffectColor.IsVisible = Panels.ShowsEffectColor;
            BaseColor.IsVisible = Panels.ShowsBaseColor;

            RefreshDirections();
            RefreshSelectedColorSlot();
        }

        private void RefreshDirections()
        {
            Directions = LightingDirectionViewModel.CreateAll(_device.DeviceId, SelectedMode);

            var state = SelectedLayer?.State;

            if (Directions.Count == 0 || state is null)
            {
                return;
            }

            var current = FindDirection(state.Direction) ?? Directions[0];

            // A direction the mode does not accept would be written as the default anyway
            // (specs/07-lighting.md §2.4 item 5), so the control and the file are kept in step.
            //
            // THIS IS A WRITE INTO THE PROFILE'S MODEL, and it is announced like the other six.
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
                if (entry.Direction == direction)
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
        /// The picker moved: the selected swatch follows it into the model. While the tab is
        /// re-reading a layer the flow is the other way round, so the write is suppressed.
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

            RaiseModelChanged();
        }

        private void SelectDirection(LightingDirectionViewModel? direction)
        {
            var state = SelectedLayer?.State;

            if (direction is null || state is null)
            {
                return;
            }

            state.Direction = direction.Direction;

            foreach (var entry in Directions)
            {
                entry.IsSelected = ReferenceEquals(entry, direction);
            }

            RaiseModelChanged();
        }

        /// <summary>
        /// Paints one key with the picker's color. The map is keyed by <b>memory key code</b>
        /// (§4), and black clears the key rather than storing it — that is
        /// <see cref="LayerLightingState.SetKeyColor"/>'s contract (§2.1), honoured rather than
        /// worked around.
        /// </summary>
        private void AssignKeyColor(KeyboardKeyViewModel? key)
        {
            var state = SelectedLayer?.State;

            if (key is null || state is null || !Panels.ShowsPerKeyColors)
            {
                return;
            }

            state.SetKeyColor(key.Key.OriginalKey.Code, Picker.Color);

            RefreshOverlays();
            RaiseModelChanged();
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

            if (zone is null || state is null || !Panels.ShowsZones)
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

            RefreshOverlays();
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

            if (state is null || !Panels.ShowsResetAll)
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
                    Buttons = MessageBoxButtons.YesNo
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

            RefreshOverlays();
            RaiseModelChanged();
        }

        /// <summary>
        /// Re-paints the colour strips of the shown picture. Core's model announces nothing
        /// (docs/app/keyboard-editor.md, invariant 3), so the map has to be pushed in by hand.
        /// <para>
        /// The Keys tab shares these very cap view models, so the colours land on its caps too —
        /// but its picture asks for no LED strip (<c>KeyboardView.ShowsLedStrips</c>), so nothing
        /// of this is drawn there.
        /// </para>
        /// </summary>
        private void RefreshOverlays()
        {
            var board = Board;

            if (board is null || _model is null)
            {
                return;
            }

            board.ApplyColorOverlays(KeyColorOverlay.Build(_device.Device, _model, board.Layer));
        }

        /// <summary>
        /// Announces a write into the profile's lighting model. Every one of the eight write sites
        /// on this panel ends here — the mode, the speed, the direction, either colour swatch, a
        /// painted key, a painted zone, "Reset All", and the direction <b>normalization</b> of
        /// <see cref="RefreshDirections"/> — because <see cref="ModelChanged"/> is what turns the
        /// editor's Save amber.
        /// </summary>
        private void RaiseModelChanged()
        {
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NotifyCommands()
        {
            ApplyZoneCommand.NotifyCanExecuteChanged();
            AssignKeyColorCommand.NotifyCanExecuteChanged();
            ResetAllCommand.NotifyCanExecuteChanged();
        }
    }
}
