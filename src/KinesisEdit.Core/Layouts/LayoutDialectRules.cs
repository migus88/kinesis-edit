using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// How a dialect's parser reacts to a speed/repeat value outside 0..9
    /// (specs/06-macros.md §2.2 "Additional parse details").
    /// </summary>
    internal enum OutOfRangeValueHandling
    {
        /// <summary>The token is consumed and the value discarded; the default applies (Gen1 RGB family).</summary>
        Ignore = 0,

        /// <summary>The device's default value is stored instead (old Freestyle and Advantage2 parsers).</summary>
        SubstituteDefault = 1,

        /// <summary>The value is clamped into the device's range (Advantage360; 06 §4 "clamps up").</summary>
        Clamp = 2
    }

    /// <summary>
    /// The per-dialect parse/serialize knobs of specs/04-layout-file-format.md §4.1 and
    /// specs/06-macros.md §2-§3, resolved once per parser/serializer from the device catalog so
    /// the engine code stays dialect-agnostic. Persisted slot/co-trigger counts come from
    /// <see cref="MacroCapability"/> — they exist there precisely so the serializer variants are
    /// data-driven.
    /// </summary>
    internal sealed class LayoutDialectRules
    {
        /// <summary>The Gen1/Adv2 macro speed token prefixes of specs/05-key-model.md §5.8.</summary>
        private const string GenericSpeedPrefix = "s";
        private const string LegacySpeedPrefix = "speed";

        /// <summary>The dialect these rules describe (04 §4.1).</summary>
        public LayoutDialect Dialect { get; }

        /// <summary>The token dialect file tokens resolve in (05 §3 legend).</summary>
        public TokenDialect Tokens { get; }

        /// <summary>The device the layout belongs to — the source of every persisted count and range.</summary>
        public DeviceDefinition Device { get; }

        /// <summary>04 §1.3: the older FS and Adv2 parsers require the line to start with '[' or '{'.</summary>
        public bool RequiresBracketAtLineStart => Dialect is LayoutDialect.Freestyle or LayoutDialect.Advantage2;

        /// <summary>04 §3.1: the Gen1 family encodes the bottom layer with the <c>fn </c> line prefix.</summary>
        public bool UsesFnLinePrefix => Dialect is LayoutDialect.Freestyle or LayoutDialect.Gen1Rgb;

        /// <summary>04 §3.2: the Advantage2 encodes the keypad layer per token (<c>kp-</c> / exception list).</summary>
        public bool UsesKeypadTokenPrefix => Dialect is LayoutDialect.Advantage2;

        /// <summary>04 §3.3: Gen2 partitions the file with <c>&lt;...&gt;</c> layer header lines.</summary>
        public bool UsesLayerHeaders => Dialect is LayoutDialect.Gen2;

        /// <summary>
        /// 04 §1.3 scopes the multi-modifier rule-type test to the Gen1 RGB/TKO and Gen2 parser;
        /// 04 §2.3 has the same serializer write the lines. On the older dialects a multi-modifier
        /// value is an unknown output token, i.e. an invalid line.
        /// </summary>
        public bool ReadsAndWritesMultiModifierLines => Dialect is LayoutDialect.Gen1Rgb or LayoutDialect.Gen2;

        /// <summary>
        /// 04 §2.2: the older FS and Adv2 parsers substitute the default delay for an unparseable
        /// (missing, non-numeric, or negative) tap-and-hold delay; the Gen1 RGB-family/Gen2 parser
        /// marks the line invalid instead.
        /// </summary>
        public bool SubstitutesInvalidTapAndHoldDelay => Dialect is LayoutDialect.Freestyle or LayoutDialect.Advantage2;

        /// <summary>The device's default hold threshold (250 ms; 11 §11.1, 04 §2.2).</summary>
        public int DefaultTapAndHoldDelay => Device.TapAndHold.DelayMilliseconds?.Default ?? 250;

        /// <summary>The per-macro speed token prefix: <c>s</c>, or <c>speed</c> on the Advantage2 (06 §2.2, 05 §5.8).</summary>
        public string SpeedTokenPrefix => Dialect == LayoutDialect.Advantage2 ? LegacySpeedPrefix : GenericSpeedPrefix;

        /// <summary>
        /// Whether the parser honours a leading <c>{xN}</c> repeat token. The Advantage2 dialect
        /// has no repeat token at all — its serializer writes none (06 §3) and 06 §4 lists no
        /// repeat maximum for it.
        /// </summary>
        public bool ParsesRepeatToken => Dialect != LayoutDialect.Advantage2;

        /// <summary>What happens to a speed/repeat value outside 0..9 (06 §2.2, §4).</summary>
        public OutOfRangeValueHandling OutOfRangeValues
        {
            get
            {
                if (Device.Macros.ClampsOutOfRangeValues)
                {
                    return OutOfRangeValueHandling.Clamp;
                }

                return Dialect == LayoutDialect.Gen1Rgb
                    ? OutOfRangeValueHandling.Ignore
                    : OutOfRangeValueHandling.SubstituteDefault;
            }
        }

        /// <summary>
        /// 06 §2.2, 05 §3.12: on RGB/TKO the legacy <c>d125</c>/<c>d500</c> tokens are not
        /// registered as the distinct legacy delay keys — they resolve to the generated
        /// <c>dNNN</c> range instead.
        /// </summary>
        public bool ResolvesLegacyDelaysAsGenerated => Dialect == LayoutDialect.Gen1Rgb;

        /// <summary>
        /// Smallest speed/repeat value written as a token (06 §3): the RGB-family/Gen2 serializer
        /// writes every value 0..9 including <c>{s0}</c>; the old FS serializer and the Adv2
        /// <c>{speedN}</c> variant omit 0.
        /// </summary>
        public int MinimumWrittenSpeedOrRepeat =>
            Dialect is LayoutDialect.Freestyle or LayoutDialect.Advantage2 ? 1 : 0;

        /// <summary>Macro slots written per key (06 §1: FS/Adv2 slots 1-3, RGB/TKO all 5); 0 on Gen2.</summary>
        public int PersistedMacroSlots => Device.Macros.PersistedSlotsPerKey ?? 0;

        /// <summary>Co-triggers written per macro line (06 §2.1, §3: FS 1, Adv2 3, RGB/TKO/Gen2 4).</summary>
        public int PersistedCoTriggers => Device.Macros.PersistedCoTriggersPerMacro ?? Model.Macro.CoTriggerSlotCount;

        private LayoutDialectRules(LayoutDialect dialect, DeviceDefinition device)
        {
            Dialect = dialect;
            Tokens = LayoutDialectResolver.GetTokenDialect(dialect);
            Device = device;
        }

        /// <summary>
        /// Resolves the rules for a device (04 §4.1). Throws for devices without an in-scope
        /// layout dialect (SE2 pedal, CROSSFIRE, Advantage360 Professional).
        /// </summary>
        public static LayoutDialectRules Create(DeviceId deviceId)
        {
            var dialect = LayoutDialectResolver.Resolve(deviceId);

            if (dialect == LayoutDialect.None)
            {
                throw new ArgumentException($"Device {deviceId} has no supported layout-file dialect.", nameof(deviceId));
            }

            return new LayoutDialectRules(dialect, DeviceCatalog.GetById(deviceId));
        }
    }
}
