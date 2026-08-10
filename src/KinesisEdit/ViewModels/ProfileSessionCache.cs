using System.Globalization;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Every numbered profile the editor has opened, by number — <b>the editor's whole multi-profile
    /// state</b> — and the two questions asked of it: <em>is anything unsaved anywhere?</em> and
    /// <em>which profiles does one press of Save write?</em>
    /// <para>
    /// It is a type of its own rather than a dictionary and eight more methods on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What earns it the
    /// file is invariant 31 (docs/app/keyboard-editor.md): <b>the write set is the cache's changed
    /// profiles, and validating it is all-or-nothing.</b> The set is chosen here, pre-validated here
    /// and worded here, so the three can never drift apart — a save that walked a second collection
    /// of "sessions we happen to be holding" is exactly what one dictionary with one owner exists to
    /// prevent.
    /// </para>
    /// <para>
    /// <b>A session enters in <c>Apply</c> and leaves in <c>Dispose</c>, and nowhere else.</b>
    /// <c>Apply</c> is the one place a session becomes the open one, so the first load, a switch, an
    /// import and a discard all keep the cache correct without any of them remembering to. Since
    /// issue #133 a profile switch <b>keeps</b> the session it leaves, with its unsaved edits and
    /// its unsaved macro renames, which is the whole of "a switch writes nothing and loses nothing"
    /// (invariant 30) — and it is why <see cref="Dispose"/> is the only path that lets a session go.
    /// </para>
    /// <para>
    /// <b>It fills lazily — on first visit, never all nine.</b> A drive may legitimately ship with
    /// gaps (specs/03-vdrive-and-files.md §5.3) and <c>IVDriveFileService.ReadAllLines</c> throws
    /// for a missing file, so reading every profile up front would need an "empty slot" concept Core
    /// does not have. It is also what makes "a profile with no changes is never rewritten" true by
    /// construction: a profile nobody opened has no session, so there is nothing for a save to walk.
    /// </para>
    /// <para>
    /// <b>It is not a view model.</b> The editor publishes the picker, the dirty flag and the save
    /// toast under its own names, so a view never sees this type — and it holds no editor reference
    /// either: the two facts it cannot know (which session is the open one, which profiles carry an
    /// unsaved macro rename) arrive as parameters. The spec strings the messages are spelled in stay
    /// public consts on <see cref="KeyboardEditorViewModel"/>, where the suites assert them.
    /// </para>
    /// </summary>
    public sealed class ProfileSessionCache : IDisposable
    {
        /// <summary>
        /// The violation report of a multi-profile save that must not start, or <b>null</b> when
        /// there is nothing to stop.
        /// <para>
        /// This is the pre-pass that makes a save of several profiles <b>all-or-nothing</b>. Core
        /// validates inside its own <c>Save</c> and refuses without writing (04 §5.3) — perfectly
        /// correct for one file, and not enough for five: profiles 1 and 3 would already be on the
        /// drive by the time profile 5 was refused, leaving a state nothing on screen could explain.
        /// Asking the very same question first — <c>KeyboardLayout.Validate()</c>, the call
        /// <c>ProfileSession</c> itself makes — costs one extra walk per profile and keeps the drive
        /// consistent.
        /// </para>
        /// <para>
        /// <b>It deliberately does nothing when the set holds one profile</b>, and that is not an
        /// optimization. With one file there is nothing to be atomic about, Core's own gate already
        /// writes nothing, and a second gate in the app would be a second policy over the same
        /// question — the exact shape of duplication this app refuses everywhere else. So the
        /// common press of Save is byte-for-byte the sequence it always was.
        /// </para>
        /// </summary>
        public static string? BuildPreflightViolationMessage(IReadOnlyList<IProfileSession> sessions)
        {
            ArgumentNullException.ThrowIfNull(sessions);

            if (sessions.Count < 2)
            {
                return null;
            }

            var lines = new List<string>();

            foreach (var session in sessions)
            {
                foreach (var violation in session.Layout.Validate())
                {
                    lines.Add(KeyboardEditorViewModel.BuildProfileCaption(session.ProfileNumber) + ": " + violation.Message);
                }
            }

            if (lines.Count == 0)
            {
                return null;
            }

            lines.Insert(0, KeyboardEditorViewModel.SaveRejectedProfilesMessage);

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Runs one <c>Save</c> per profile in file order and pairs each result with the number it
        /// came from. It runs on a background thread, so it touches nothing but the sessions.
        /// </summary>
        public static List<ProfileSaveOutcome> SaveAll(IReadOnlyList<IProfileSession> sessions)
        {
            ArgumentNullException.ThrowIfNull(sessions);

            var outcomes = new List<ProfileSaveOutcome>(sessions.Count);

            foreach (var session in sessions)
            {
                outcomes.Add(new ProfileSaveOutcome(session.ProfileNumber, session.Save()));
            }

            return outcomes;
        }

        /// <summary>
        /// The report of a save Core refused after the pre-pass let it through — a race the pre-pass
        /// cannot close (the model is not frozen between the two calls), so it is reported rather
        /// than asserted away. Null when every write landed.
        /// </summary>
        public static string? BuildRejectedSaveMessage(IReadOnlyList<ProfileSaveOutcome> outcomes)
        {
            ArgumentNullException.ThrowIfNull(outcomes);

            var lines = new List<string>();
            var isSingle = outcomes.Count == 1;

            foreach (var outcome in outcomes)
            {
                if (outcome.Result.Success)
                {
                    continue;
                }

                foreach (var violation in outcome.Result.Violations)
                {
                    lines.Add(isSingle
                        ? violation.Message
                        : KeyboardEditorViewModel.BuildProfileCaption(outcome.ProfileNumber) + ": " + violation.Message);
                }
            }

            if (lines.Count == 0)
            {
                return null;
            }

            lines.Insert(0, isSingle
                ? KeyboardEditorViewModel.SaveRejectedMessage
                : KeyboardEditorViewModel.SaveRejectedProfilesMessage);

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// The toast's sentence for a save that landed. <b>One profile keeps Core's wording exactly
        /// as it always arrived</b> — that is the common case and nothing about it moved. Several
        /// get one aggregated line naming them, followed by the <em>distinct</em> refresh wordings:
        /// <c>ProfileSaveMessageCatalog</c> is per profile number, so nine profiles would otherwise
        /// be nine toasts, and dropping the wording entirely would lose the one instruction that
        /// tells the user how to get the profile onto the board. Identical wordings collapse (the FS
        /// Edge/Pro family has a single one for every profile).
        /// </summary>
        public static string? BuildSavedProfilesMessage(IReadOnlyList<ProfileSaveOutcome> outcomes)
        {
            ArgumentNullException.ThrowIfNull(outcomes);

            if (outcomes.Count == 0)
            {
                return null;
            }

            if (outcomes.Count == 1)
            {
                return outcomes[0].Result.PostSaveMessage;
            }

            var numbers = new List<int>(outcomes.Count);
            var wordings = new List<string>();

            foreach (var outcome in outcomes)
            {
                numbers.Add(outcome.ProfileNumber);

                if (outcome.Result.PostSaveMessage is { Length: > 0 } wording && !wordings.Contains(wording))
                {
                    wordings.Add(wording);
                }
            }

            wordings.Insert(0, BuildSavedProfilesSentence(numbers));

            return string.Join(" ", wordings);
        }

        /// <summary>
        /// <c>Saved profiles 1, 3 and 5.</c> — the app's own sentence, in the design's plain voice.
        /// Two numbers read <c>1 and 3</c>; three or more are comma-separated with <c>and</c> before
        /// the last. It is the only list this app builds, so the rule lives here and nowhere else.
        /// </summary>
        public static string BuildSavedProfilesSentence(IReadOnlyList<int> profileNumbers)
        {
            ArgumentNullException.ThrowIfNull(profileNumbers);

            var names = new List<string>(profileNumbers.Count);

            foreach (var number in profileNumbers)
            {
                names.Add(number.ToString(CultureInfo.InvariantCulture));
            }

            var list = names.Count == 2
                ? names[0] + KeyboardEditorViewModel.SavedProfilesConjunction + names[1]
                : string.Join(", ", names.Take(names.Count - 1))
                  + KeyboardEditorViewModel.SavedProfilesConjunction
                  + names[^1];

            return KeyboardEditorViewModel.SavedProfilesPrefix + list + ".";
        }

        private readonly Dictionary<int, IProfileSession> _sessions = [];

        /// <summary>
        /// Files a session under its own profile number as it becomes the open one. Called from
        /// <c>Apply</c> and nowhere else, so the first load, a switch, an import and a discard all
        /// keep the cache correct without any of them having to remember to. Re-filing a session
        /// already held is a no-op.
        /// </summary>
        public void Add(IProfileSession? session)
        {
            if (session is null)
            {
                return;
            }

            _sessions[session.ProfileNumber] = session;
        }

        /// <summary>
        /// The profile as the editor already holds it, or null when this is its first visit. The
        /// caller marks a hit as one (<c>LoadOutcome.IsCacheHit</c>) because <c>Apply</c> must not
        /// re-stamp the stored macro names over a session whose names the user has since edited.
        /// </summary>
        public IProfileSession? Find(int profileNumber)
        {
            return _sessions.TryGetValue(profileNumber, out var cached) ? cached : null;
        }

        /// <summary>
        /// Whether any profile the user has opened re-serializes to something different from what
        /// was read — the layout half of <c>IsDirty</c>, across the whole cache rather than the open
        /// profile alone.
        /// <para>
        /// Each answer re-serializes that profile's layout and led model (<c>ProfileSession.IsDirty</c>
        /// is a pull), so this is called from <c>RefreshDirtyState</c> and never from a binding, and
        /// it stops at the first dirty one. <paramref name="openSession"/> is asked <b>first</b>: it
        /// is the profile the edit that triggered the refresh landed in, so on the overwhelmingly
        /// common path the answer costs exactly what it always cost — one serialization — and the
        /// other eight are never asked.
        /// </para>
        /// </summary>
        public bool AnyIsDirty(IProfileSession? openSession)
        {
            if (openSession is { IsDirty: true })
            {
                return true;
            }

            foreach (var session in _sessions.Values)
            {
                if (!ReferenceEquals(session, openSession) && session.IsDirty)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The profiles one press of <c>Save</c> writes, in file order: <b>every opened profile that
        /// changed, and nothing else.</b> Empty is a legitimate answer — with nothing changed
        /// anywhere no file is written at all, which is the user's own request verbatim (*"if there
        /// were no changes - no need to override the file"*) and matters because a v-Drive is flash.
        /// <c>TrySaveAsync</c> reports that case rather than leaving the press silent.
        /// <para>
        /// So a clean profile is never rewritten — not while another one is dirty, and not when it
        /// is the one on screen — and a profile nobody opened is never written at all, because it
        /// has no session. <b>This reads the sessions directly</b>, which is only correct because
        /// Core moves a saved session's dirty baseline to the lines it wrote
        /// (docs/app/profiles.md): with a baseline frozen at load, every profile written in this
        /// sitting would report itself dirty for ever and each later press of Save would rewrite the
        /// lot.
        /// </para>
        /// <para>
        /// <b>A macro rename counts as a change, and the session cannot see it.</b> Names ride
        /// <c>app_settings.txt</c>, not <c>layout&lt;n&gt;.txt</c>, so a profile whose only edit is a
        /// rename re-serializes identically and answers <c>IsDirty</c> false — while
        /// <c>PersistMacroNames</c> only ever runs after a save that wrote something. Without
        /// <paramref name="renamedProfiles"/> a rename would therefore be unsaveable: the write set
        /// would be empty, the press would report nothing to save, and the name would sit in memory
        /// until the editor closed. The profile's files are rewritten with identical content in that
        /// case, which is the one rewrite left and is the honest reading of "this profile changed".
        /// </para>
        /// <para>
        /// Read-only profiles are excluded here as well as by <c>CanSave()</c>: the Advantage 360's
        /// factory profile cannot be reached from the picker, but a write set that could contain it
        /// would be one guard away from a <c>ProfileReadOnlyException</c> mid-loop.
        /// </para>
        /// </summary>
        public IReadOnlyList<IProfileSession> CollectSessionsToSave(IReadOnlySet<int> renamedProfiles)
        {
            ArgumentNullException.ThrowIfNull(renamedProfiles);

            var pending = new List<IProfileSession>();

            foreach (var session in _sessions.Values)
            {
                if (!session.CanSave)
                {
                    continue;
                }

                if (session.IsDirty || renamedProfiles.Contains(session.ProfileNumber))
                {
                    pending.Add(session);
                }
            }

            pending.Sort(static (left, right) => left.ProfileNumber.CompareTo(right.ProfileNumber));

            return pending;
        }

        /// <summary>
        /// Releases every session the editor has opened. <see cref="IProfileSession"/> is not
        /// <see cref="IDisposable"/> today — Core's <c>ProfileSession</c> holds parsed lines and
        /// no handle, reading and writing one <c>IVDriveFileService</c> call at a time — so this is
        /// the guard rather than the call. Since the cache holds every profile the user visited,
        /// this is the <b>only</b> path that lets a session go: a switch keeps them all.
        /// </summary>
        public void Dispose()
        {
            foreach (var session in _sessions.Values)
            {
                if (session is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _sessions.Clear();
        }
    }

    /// <summary>One profile's half of a multi-profile save: which number, and what Core said.</summary>
    public sealed record ProfileSaveOutcome(int ProfileNumber, ProfileSaveResult Result);
}
