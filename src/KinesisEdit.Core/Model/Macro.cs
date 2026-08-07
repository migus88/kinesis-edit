using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// An ordered list of keystrokes plus its trigger metadata — the legacy <c>TKeyList</c> in its
    /// macro role (specs/05-key-model.md §1.2, specs/06-macros.md §1-§6). Mutable POCO. Playback
    /// speed and repeat default to the device's values (<see cref="MacroCapability"/>, 06 §4); no
    /// limit is enforced here — <see cref="KeyboardLayout.Validate"/> reports breaches instead
    /// (limits: 06 §6, 04 §5.3).
    /// </summary>
    public sealed class Macro
    {
        /// <summary>Co-trigger slots the legacy model holds (<c>CoTrigger1..4</c>, §1.2, 06 §5).</summary>
        public const int CoTriggerSlotCount = 4;

        /// <summary>Lowest macro slot number (§1.2 <c>MacroIdx</c> 1..5).</summary>
        public const int MinMacroIndex = 1;

        /// <summary>Highest macro slot number (§1.2 <c>MacroIdx</c> 1..5).</summary>
        public const int MaxMacroIndex = 5;

        /// <summary>Value both <see cref="TriggerKey"/> and <see cref="LayerIndex"/> start at (§1.2).</summary>
        public const int UnassignedIndex = -1;

        /// <summary>Identity created with the macro; used to tell macros apart while editing (§1.2, 06 §1).</summary>
        public Guid Id { get; }

        /// <summary>
        /// User-facing name of the macro; the empty string means unnamed. Sanitized on the way in
        /// through <see cref="MacroNaming.Sanitize"/> — trimmed, single line, at most
        /// <see cref="MacroNaming.MaxNameLength"/> characters — because a newline here would
        /// corrupt the line-oriented settings file the name is stored in.
        /// <para>
        /// <b>The name is not in the layout file.</b> 04 has no comment syntax and an unrecognised
        /// line is dropped on the next save (<c>LayoutInvalidLine.Keep</c> defaults false), so
        /// there is nowhere in <c>layoutN.txt</c> to put it. It is persisted by the settings layer
        /// instead, as <c>macro_name_&lt;profile&gt;_&lt;layer&gt;_&lt;trigger&gt;_&lt;slot&gt;</c>
        /// in this app's own <c>settings/app_settings.txt</c>
        /// (<see cref="Settings.MacroNameKey"/>). A freshly parsed layout therefore always has
        /// empty names until that file is applied over it
        /// (<see cref="MacroLibrary.ApplyNames"/>).
        /// </para>
        /// <para>
        /// <b>Naming never changes a macro's identity as a macro.</b> <see cref="IsEquivalentTo"/>
        /// (the §1.2 content comparison) and <see cref="CollidesWith"/> (the 06 §5 duplicate-trigger
        /// rule) deliberately ignore this property: those two answer content and trigger questions
        /// that decide what the firmware does, and a naming feature must never be able to turn a
        /// collision into a non-collision. Grouping *by name* is <see cref="MacroLibrary"/>'s job.
        /// </para>
        /// </summary>
        public string Name
        {
            get => _name;
            set => _name = MacroNaming.Sanitize(value);
        }

        /// <summary>
        /// The legacy <c>MultiKey</c> flag (§1.2). Always true here: this type only models
        /// multi-keystroke lists, so the equality rule of §1.2 reduces to count + per-item equality.
        /// </summary>
        public bool IsMultiKey => true;

        /// <summary>Key code of the trigger key, -1 until assigned (§1.2; Gen2 flat list, 06 §1).</summary>
        public int TriggerKey { get; set; } = UnassignedIndex;

        /// <summary>Layer the macro belongs to, -1 until assigned (§1.2; Gen2 flat list, 06 §1).</summary>
        public int LayerIndex { get; set; } = UnassignedIndex;

        /// <summary>Macro slot 1..5 this list occupies on its key (§1.2 <c>MacroIdx</c>); 0 in the Gen2 flat list.</summary>
        public int MacroIndex { get; set; }

        /// <summary>
        /// Playback speed (06 §4). 0 means "use the keyboard's global speed"; the device's
        /// <see cref="MacroCapability.Speed"/> gives the accepted range and the default.
        /// </summary>
        public int Speed { get; set; }

        /// <summary>Repeat/multiplay factor — how often the macro plays per trigger press (06 §4).</summary>
        public int RepeatFrequency { get; set; }

        /// <summary>The keystrokes in playback order (§1.2).</summary>
        public IReadOnlyList<Keystroke> Keystrokes => _keystrokes;

        /// <summary>Modifier keys that must be held with the trigger, in slot order (§1.2, 06 §5).</summary>
        public IReadOnlyList<KeyDefinition> CoTriggers => _coTriggers;

        /// <summary>Number of populated co-trigger slots — the count the collision rule compares (06 §5).</summary>
        public int CoTriggerCount => _coTriggers.Count;

        /// <summary>Whether the macro has no keystrokes (a Gen2 validation failure, 06 §5).</summary>
        public bool IsEmpty => _keystrokes.Count == 0;

        /// <summary>
        /// Cost of the macro against the 7200 keystroke budget, and the measure of the 300 per-macro
        /// cap on every non-Gen2 family: 1 per keystroke plus 2 per attached modifier
        /// (specs/04-layout-file-format.md §5.3, 06 §6). This is not the Advantage360 per-macro
        /// metric — that one measures the serialized keystroke text
        /// (<see cref="MacroKeystrokeRenderer.KeystrokeTextLength"/>).
        /// </summary>
        public int WeightedKeystrokeCount
        {
            get
            {
                var total = 0;

                foreach (var keystroke in _keystrokes)
                {
                    total += keystroke.WeightedKeystrokeCount;
                }

                return total;
            }
        }

        private string _name = string.Empty;

        private readonly List<Keystroke> _keystrokes = [];
        private readonly List<KeyDefinition> _coTriggers = [];

        /// <summary>Creates an empty macro with speed and repeat 0 — the Advantage2/Freestyle defaults (06 §4).</summary>
        public Macro()
        {
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// Creates an empty macro whose speed and repeat carry the device's defaults
        /// (<see cref="MacroCapability.Speed"/>/<see cref="MacroCapability.Repeat"/>, 06 §4);
        /// 0 where the device states none.
        /// </summary>
        public Macro(MacroCapability capability)
            : this()
        {
            ArgumentNullException.ThrowIfNull(capability);

            Speed = capability.Speed?.Default ?? 0;
            RepeatFrequency = capability.Repeat?.Default ?? 0;
        }

        /// <summary>Appends a keystroke (§1.2).</summary>
        public void AddKeystroke(Keystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);

            _keystrokes.Add(keystroke);
        }

        /// <summary>Appends several keystrokes in order (§1.2).</summary>
        public void AddKeystrokes(IEnumerable<Keystroke> keystrokes)
        {
            ArgumentNullException.ThrowIfNull(keystrokes);

            foreach (var keystroke in keystrokes)
            {
                AddKeystroke(keystroke);
            }
        }

        /// <summary>Inserts a keystroke at <paramref name="index"/> (§1.2).</summary>
        public void InsertKeystroke(int index, Keystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _keystrokes.Count);

            _keystrokes.Insert(index, keystroke);
        }

        /// <summary>Removes the keystroke at <paramref name="index"/> (§1.2).</summary>
        public void RemoveKeystrokeAt(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _keystrokes.Count);

            _keystrokes.RemoveAt(index);
        }

        /// <summary>Removes every keystroke, keeping the trigger metadata (§1.2).</summary>
        public void ClearKeystrokes()
        {
            _keystrokes.Clear();
        }

        /// <summary>
        /// Appends a co-trigger (06 §5). Nothing is refused here: a macro may exceed the device's
        /// <see cref="MacroCapability.MaxCoTriggersPerMacro"/> and
        /// <see cref="KeyboardLayout.Validate"/> reports it (06 §6).
        /// <para>
        /// The append does not de-duplicate. 06 §5 defines <see cref="CoTriggerCount"/> as "the
        /// number of populated slots", and a co-trigger token that a field file repeats is content
        /// that must survive a load/save round trip — collapsing it here would silently rewrite the
        /// file. <see cref="CollidesWith"/> therefore compares the two sets symmetrically instead of
        /// relying on the entries being distinct.
        /// </para>
        /// </summary>
        public void AddCoTrigger(KeyDefinition coTrigger)
        {
            ArgumentNullException.ThrowIfNull(coTrigger);

            _coTriggers.Add(coTrigger);
        }

        /// <summary>Removes the co-trigger in slot <paramref name="index"/> (06 §5).</summary>
        public void RemoveCoTriggerAt(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _coTriggers.Count);

            _coTriggers.RemoveAt(index);
        }

        /// <summary>Removes every co-trigger (06 §5).</summary>
        public void ClearCoTriggers()
        {
            _coTriggers.Clear();
        }

        /// <summary>Whether any keystroke fires the key with code <paramref name="keyCode"/> (§1.2 membership by key code).</summary>
        public bool ContainsKeyCode(int keyCode)
        {
            foreach (var keystroke in _keystrokes)
            {
                if (keystroke.Key.Code == keyCode)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a co-trigger with code <paramref name="keyCode"/> is populated (06 §5).</summary>
        public bool ContainsCoTrigger(int keyCode)
        {
            foreach (var coTrigger in _coTriggers)
            {
                if (coTrigger.Code == keyCode)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The duplicate-trigger rule of 06 §5: two macros on the same trigger key collide when
        /// every co-trigger of one exists in the other and both have the same co-trigger count, or
        /// when both have no co-triggers at all. The containment is tested in both directions, so
        /// the answer is the same whichever macro is asked — a one-way test would call
        /// <c>[lctrl, lctrl]</c> and <c>[lctrl, lshft]</c> equal, since the counts match and every
        /// entry of the first is present in the second.
        /// <para>
        /// <see cref="Name"/> takes no part in it either: this is what the firmware sees, and no
        /// name a user types may turn a duplicate trigger into a legal one.
        /// </para>
        /// </summary>
        public bool CollidesWith(Macro other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (_coTriggers.Count == 0 && other._coTriggers.Count == 0)
            {
                return true;
            }

            if (_coTriggers.Count != other._coTriggers.Count)
            {
                return false;
            }

            return ContainsEveryCoTriggerOf(other) && other.ContainsEveryCoTriggerOf(this);
        }

        /// <summary>
        /// The up/down integrity rule of 06 §5/§6 (Gen2): every explicit key-down needs a later
        /// matching key-up. An up without a preceding down is a single tap, not a violation (06 §2.2).
        /// </summary>
        public bool HasBalancedKeyDirections()
        {
            var open = new Dictionary<int, int>();

            foreach (var keystroke in _keystrokes)
            {
                var code = keystroke.Key.Code;

                switch (keystroke.EffectiveDirection)
                {
                    case KeyDirection.Down:
                        open[code] = open.GetValueOrDefault(code) + 1;
                        break;

                    case KeyDirection.Up when open.GetValueOrDefault(code) > 0:
                        open[code] -= 1;
                        break;
                }
            }

            foreach (var pending in open.Values)
            {
                if (pending > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The macro comparison of §1.2: same multi-key flag, same keystroke count, per-item key
        /// equality. Kept as an explicit method rather than an <c>Equals</c> override — macros are
        /// mutable, so reference identity stays their list identity.
        /// <para>
        /// <see cref="Name"/> takes no part in it: this is the content question, and two macros
        /// that type the same thing are equivalent whatever they are called.
        /// </para>
        /// </summary>
        public bool IsEquivalentTo(Macro? other)
        {
            if (other is null || IsMultiKey != other.IsMultiKey || _keystrokes.Count != other._keystrokes.Count)
            {
                return false;
            }

            for (var i = 0; i < _keystrokes.Count; i++)
            {
                if (!_keystrokes[i].IsEquivalentTo(other._keystrokes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Deep copy: trigger info, macro settings and <see cref="Name"/> are copied and every
        /// keystroke is cloned (§1.2 assignment semantics). The copy gets a fresh <see cref="Id"/>
        /// — it is a distinct macro. Co-trigger entries are shared key-table records, which are
        /// immutable.
        /// </summary>
        public Macro Clone()
        {
            var clone = new Macro
            {
                Name = _name,
                TriggerKey = TriggerKey,
                LayerIndex = LayerIndex,
                MacroIndex = MacroIndex,
                Speed = Speed,
                RepeatFrequency = RepeatFrequency
            };

            clone._coTriggers.AddRange(_coTriggers);

            foreach (var keystroke in _keystrokes)
            {
                clone._keystrokes.Add(keystroke.Clone());
            }

            return clone;
        }

        private bool ContainsEveryCoTriggerOf(Macro other)
        {
            foreach (var coTrigger in other._coTriggers)
            {
                if (!ContainsCoTrigger(coTrigger.Code))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
