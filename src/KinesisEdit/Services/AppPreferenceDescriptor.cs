using KinesisEdit.Core.Settings;

namespace KinesisEdit.Services
{
    /// <summary>
    /// One row of <see cref="AppPreferenceCatalog"/>: an <c>app_settings.txt</c> key, the words the
    /// settings screen shows for it, which way round its stored value reads, and the pair of
    /// accessors that reach its <see cref="AppSettings"/> property.
    /// <para>
    /// <b>Everything above the file speaks in <see cref="GetValue"/>/<see cref="SetValue"/>, never
    /// in the stored flag.</b> Those two apply <see cref="Polarity"/>, so a view model that binds a
    /// checkbox to <see cref="GetValue"/> is right for both a suppression flag (where <c>on</c>
    /// means hide, and the checkbox is the negation) and a display preference (where it is not).
    /// That is the whole reason this type exists rather than a bare key list.
    /// </para>
    /// </summary>
    public sealed class AppPreferenceDescriptor
    {
        /// <summary>The key as written in <c>app_settings.txt</c>, lowercase (spec 08 §1, §3).</summary>
        public string Key { get; }

        /// <summary>The option as the settings screen states it, phrased so that enabled = the thing happens.</summary>
        public string Caption { get; }

        /// <summary>One sentence saying which prompt or behaviour the option governs.</summary>
        public string Description { get; }

        /// <summary>Which way round the stored <c>on</c>/<c>off</c> value reads.</summary>
        public AppPreferencePolarity Polarity { get; }

        /// <summary>
        /// Whether something in the app reads this preference today. False marks a
        /// <i>forward-declared</i> preference whose prompt is not built yet — the key exists so the
        /// feature that lands later does not have to invent one, and so the user can pre-answer it.
        /// </summary>
        public bool HasLiveConsumer { get; }

        /// <summary>
        /// The user-facing value when the key is absent from the file — <b>derived from
        /// <see cref="Polarity"/>, never declared</b>, because absent always means <c>off</c> on
        /// disk and no other default is expressible. Suppression preferences default to enabled
        /// ("ask me"), display preferences to disabled. Equals <c>GetValue(AppSettings.Empty)</c>.
        /// </summary>
        public bool DefaultValue => Polarity == AppPreferencePolarity.Suppression;

        /// <summary>Whether this preference can carry a message box's <c>SuppressionKey</c>.</summary>
        public bool IsSuppression => Polarity == AppPreferencePolarity.Suppression;

        private readonly Func<AppSettings, bool?> _readStoredFlag;
        private readonly Func<AppSettings, bool, AppSettings> _writeStoredFlag;

        /// <summary>Creates a descriptor; <paramref name="readStoredFlag"/>/<paramref name="writeStoredFlag"/> reach the raw file flag.</summary>
        public AppPreferenceDescriptor(
            string key,
            string caption,
            string description,
            AppPreferencePolarity polarity,
            bool hasLiveConsumer,
            Func<AppSettings, bool?> readStoredFlag,
            Func<AppSettings, bool, AppSettings> writeStoredFlag)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

            Key = key;
            Caption = caption;
            Description = description;
            Polarity = polarity;
            HasLiveConsumer = hasLiveConsumer;

            _readStoredFlag = readStoredFlag ?? throw new ArgumentNullException(nameof(readStoredFlag));
            _writeStoredFlag = writeStoredFlag ?? throw new ArgumentNullException(nameof(writeStoredFlag));
        }

        /// <summary>
        /// The option's value as the user sees it: for a suppression preference the negation of
        /// the stored flag (stored <c>on</c> = hidden = option off), for a display preference the
        /// stored flag itself. An absent key yields <see cref="DefaultValue"/>.
        /// </summary>
        public bool GetValue(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var stored = _readStoredFlag(settings);

            return Polarity == AppPreferencePolarity.Suppression ? stored != true : stored == true;
        }

        /// <summary>
        /// Returns a copy of <paramref name="settings"/> carrying <paramref name="value"/> as the
        /// user-facing option, storing whichever flag <see cref="Polarity"/> calls for. The key is
        /// always written after this — including when the value equals the default — so the answer
        /// is durable rather than implied by absence.
        /// </summary>
        public AppSettings SetValue(AppSettings settings, bool value)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var stored = Polarity == AppPreferencePolarity.Suppression ? !value : value;

            return _writeStoredFlag(settings, stored);
        }
    }
}
