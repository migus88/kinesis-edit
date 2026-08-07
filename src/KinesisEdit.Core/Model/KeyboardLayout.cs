using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The editable state of one device profile: its layers of key positions plus — on the
    /// Advantage360, which keeps macros in one flat per-layout list instead of per-key slots
    /// (specs/06-macros.md §1) — that macro list. Built from a
    /// <see cref="DeviceDefinition"/> and its <see cref="DeviceGeometry"/>
    /// (specs/05-key-model.md §1.4, §1.5). Limits are read from the device catalog (04 §5.3,
    /// 06 §6) and only ever reported by <see cref="Validate"/>, never enforced — field files
    /// written by older firmware must still load (the read-tolerantly rule of the project's
    /// CLAUDE.md).
    /// </summary>
    public sealed class KeyboardLayout
    {
        /// <summary>The device this layout belongs to; the source of every limit (specs/02-devices.md).</summary>
        public DeviceDefinition Device { get; }

        /// <summary>The token dialect the device's files are written in (05 §3 legend).</summary>
        public TokenDialect Dialect { get; }

        /// <summary>Layout variant of the underlying geometry (QWERTY/Dvorak on the Advantage2; §4.5, §4.6).</summary>
        public LayoutVariant Variant { get; }

        /// <summary>The device's layers in spec order (§1.5).</summary>
        public IReadOnlyList<KeyboardLayer> Layers { get; }

        /// <summary>
        /// Whether macros live in <see cref="Macros"/> instead of the keys' slots
        /// (<see cref="MacroCapability.UsesFlatMacroList"/>; Advantage360 only, 06 §1).
        /// </summary>
        public bool UsesFlatMacroList => Device.Macros.UsesFlatMacroList;

        /// <summary>
        /// The flat macro list of the Gen2 dialect, each macro tagged with its trigger key and
        /// layer (06 §1). Always empty on slot-based devices.
        /// </summary>
        public IReadOnlyList<Macro> Macros => _macros;

        /// <summary>Number of remapped key positions across every layer (§1.3).</summary>
        public int ModifiedKeyCount => CountKeys(static key => key.IsModified);

        /// <summary>
        /// Number of macros in the layout — everything <see cref="EnumerateMacros"/> yields: the
        /// Gen2 flat list plus every populated macro slot of every key of every layer (06 §1, §6).
        /// </summary>
        public int MacroCount
        {
            get
            {
                var count = 0;

                foreach (var _ in EnumerateMacros())
                {
                    count++;
                }

                return count;
            }
        }

        /// <summary>
        /// The layout's keystroke total against the 7200 budget: 1 per keystroke plus 2 per
        /// attached modifier, summed over every macro (04 §5.3, 06 §6). Not the Advantage360
        /// per-macro metric, which measures the serialized keystroke text.
        /// </summary>
        public int TotalKeystrokes
        {
            get
            {
                var total = 0;

                foreach (var macro in EnumerateMacros())
                {
                    total += macro.WeightedKeystrokeCount;
                }

                return total;
            }
        }

        /// <summary>Number of tap-and-hold assignments across every layer (§5.6; 11 §11.1).</summary>
        public int TapAndHoldCount => CountKeys(static key => key.IsTapAndHold);

        /// <summary>
        /// The dialect <paramref name="deviceId"/>'s files are written in, <b>without building a
        /// layout to ask</b>. It is the very rule <see cref="Dialect"/> is set from (05 §3 legend),
        /// exposed because it is a fact about the <em>device</em> and not about any profile: the
        /// token picker needs a catalog before a profile has been read, and re-deriving the mapping
        /// at the call site would be a second place for it to disagree with this one.
        /// </summary>
        public static TokenDialect DialectFor(DeviceId deviceId)
        {
            return KeyboardLayoutBuilder.ResolveDialect(deviceId);
        }

        private readonly List<Macro> _macros = [];

        /// <summary>
        /// Builds the runtime model of <paramref name="device"/> from <paramref name="geometry"/>
        /// (§1.4, §1.5): every geometry position becomes a <see cref="KeyboardKey"/> whose tokens
        /// are resolved in the device's dialect.
        /// </summary>
        public KeyboardLayout(DeviceDefinition device, DeviceGeometry geometry)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(geometry);

            Device = device;
            Dialect = KeyboardLayoutBuilder.ResolveDialect(device.Id);
            Variant = geometry.Variant;
            Layers = KeyboardLayoutBuilder.BuildLayers(geometry, device, Dialect);
        }

        /// <summary>
        /// Builds the layout of a programmable device straight from the catalogs
        /// (<see cref="DeviceCatalog"/> + <see cref="GeometryCatalog"/>). Throws for devices without
        /// key-layer geometry (the Savant Elite2 pedal, the CROSSFIRE keypad, the Advantage360
        /// Professional).
        /// </summary>
        public static KeyboardLayout Create(DeviceId deviceId, LayoutVariant variant = LayoutVariant.None)
        {
            var device = DeviceCatalog.GetById(deviceId);

            if (!GeometryCatalog.TryGet(deviceId, variant, out var geometry))
            {
                throw new ArgumentException($"Device {deviceId} has no layer geometry for variant {variant}.", nameof(deviceId));
            }

            return new KeyboardLayout(device, geometry);
        }

        /// <summary>Returns the layer with <see cref="KeyboardLayer.Index"/> <paramref name="index"/>, or null (§1.5).</summary>
        public KeyboardLayer? FindLayer(int index)
        {
            foreach (var layer in Layers)
            {
                if (layer.Index == index)
                {
                    return layer;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates an empty macro carrying the device's speed and repeat defaults
        /// (<see cref="MacroCapability.Speed"/>/<see cref="MacroCapability.Repeat"/>, 06 §4).
        /// </summary>
        public Macro CreateMacro()
        {
            return new Macro(Device.Macros);
        }

        /// <summary>
        /// Appends a macro to the Gen2 flat list (06 §1). Throws on slot-based devices, whose
        /// macros belong in <see cref="KeyboardKey.SetMacro"/> slots.
        /// </summary>
        public void AddMacro(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            if (!UsesFlatMacroList)
            {
                throw new InvalidOperationException(
                    $"Device {Device.Id} stores macros in per-key slots, not in a flat layout list.");
            }

            _macros.Add(macro);
        }

        /// <summary>Removes a macro from the flat list by identity; returns whether it was there (06 §1).</summary>
        public bool RemoveMacro(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            for (var i = 0; i < _macros.Count; i++)
            {
                if (!ReferenceEquals(_macros[i], macro))
                {
                    continue;
                }

                _macros.RemoveAt(i);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Every macro of the layout, flat list first and then every populated key slot in layer /
        /// key / slot order (06 §1). A well-formed model uses one storage or the other — Gen2
        /// appends every macro to the flat list (04 §4.2), the slot-based families never fill it —
        /// but a tolerantly-loaded model may hold both.
        /// <para>
        /// A macro instance parked in both stores is yielded once: it is one macro, and counting it
        /// twice would push the layout over the 100/7200 caps of 04 §5.3 that it does not breach.
        /// Identity is by reference — two equal-looking macros really are two macros (06 §1 gives
        /// each its own <see cref="Macro.Id"/>).
        /// </para>
        /// <para>
        /// <see cref="MacroCount"/> and <see cref="TotalKeystrokes"/> are the only things derived
        /// from this traversal, so those two and the layout-budget checks that read them always
        /// agree. The rest of <see cref="Validate"/> walks the key slots and the flat list itself
        /// and reports a macro once per *place* it sits, which is why one instance sitting in two
        /// different key slots is measured twice: it really is on two positions.
        /// </para>
        /// </summary>
        public IEnumerable<Macro> EnumerateMacros()
        {
            var seen = new HashSet<Macro>(ReferenceEqualityComparer.Instance);

            foreach (var macro in _macros)
            {
                if (seen.Add(macro))
                {
                    yield return macro;
                }
            }

            foreach (var macro in EnumerateSlotMacros())
            {
                if (seen.Add(macro))
                {
                    yield return macro;
                }
            }
        }

        /// <summary>
        /// Returns the flat-list macros of one trigger key on one layer (06 §1: Gen2 macros are
        /// tagged with trigger key and layer). Empty on slot-based devices.
        /// </summary>
        public IReadOnlyList<Macro> FindMacros(int layerIndex, int triggerKeyCode)
        {
            var matches = new List<Macro>();

            foreach (var macro in _macros)
            {
                if (macro.LayerIndex == layerIndex && macro.TriggerKey == triggerKeyCode)
                {
                    matches.Add(macro);
                }
            }

            return matches;
        }

        /// <summary>
        /// Resets every key of every layer to its factory state and empties the flat macro list
        /// (§1.3 reset, applied layout-wide).
        /// </summary>
        public void Reset()
        {
            foreach (var layer in Layers)
            {
                layer.Reset();
            }

            _macros.Clear();
        }

        /// <summary>
        /// Reports every device limit and macro rule the current state breaches, most general
        /// first (limits: 04 §5.3, 06 §6; 11 §11.1). An empty result means the layout is within the
        /// catalog's limits; a non-empty one never means an edit was refused.
        /// </summary>
        public IReadOnlyList<ModelViolation> Validate()
        {
            return KeyboardLayoutValidator.Validate(this);
        }

        private IEnumerable<Macro> EnumerateSlotMacros()
        {
            foreach (var layer in Layers)
            {
                foreach (var key in layer.Keys)
                {
                    foreach (var macro in key.Macros)
                    {
                        if (macro is not null)
                        {
                            yield return macro;
                        }
                    }
                }
            }
        }

        private int CountKeys(Func<KeyboardKey, bool> predicate)
        {
            var count = 0;

            foreach (var layer in Layers)
            {
                foreach (var key in layer.Keys)
                {
                    if (predicate(key))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
