using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using LoadOutcome = KinesisEdit.ViewModels.EditorProfileLoader.LoadOutcome;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The session lifecycle on its own — <b>no editor anywhere near it</b>: what a load does when
    /// the drive answers and when it does not, what <c>Apply</c> writes and in which order, and the
    /// one flag that decides whether a returning profile keeps the user's macro renames.
    /// <para>
    /// The editor is a fake host that records every write and call the loader makes on it, in order,
    /// which is what the <c>Apply</c> assertions need. Every claim here used to have to be made
    /// through a <c>KeyboardEditorViewModel</c> with a fake drive under it.
    /// </para>
    /// </summary>
    public class EditorProfileLoaderTests
    {
        /// <summary>
        /// <c>Apply</c> is the one place a profile becomes the picture, and the order is the design:
        /// the session and the model first, the picker's answer off it, the layers, the stored macro
        /// names <b>before anything reads one</b>, the shown layer, the funnel, and the lighting
        /// panel last — it is handed the very list that was just built.
        /// </summary>
        [AvaloniaFact]
        public void Apply_MakesTheOutcomeTheEditor_InOrder()
        {
            var scene = new Scene();
            var session = scene.Session(profileNumber: 3);

            scene.Loader.Apply(new LoadOutcome { Session = session, Layout = session.Layout });

            Assert.Equal(
                [
                    "Session",
                    "Layout",
                    "ProfileCaption",
                    "InvalidLineMessages",
                    "RefreshSelectedProfile",
                    "Layers",
                    "AttachMacroNames",
                    "SelectLayer",
                    "RefreshCounters",
                    "Lighting"
                ],
                scene.Host.Calls);

            Assert.Same(session, scene.Host.Session);
            Assert.Same(session.Layout, scene.Host.Layout);
            Assert.Equal("Profile 3", scene.Host.ProfileCaption);
            Assert.NotEmpty(scene.Host.Layers);
            Assert.Same(scene.Host.Layers[0], Assert.Single(scene.Host.SelectedLayers));
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
            Assert.Equal(1, scene.Host.RefreshSelectedProfileCalls);
        }

        /// <summary>
        /// The arriving session is filed in the cache by <c>Apply</c> and nowhere else, so the first
        /// load, a switch, an import and a discard all keep the editor's multi-profile state correct
        /// without any of them remembering to.
        /// </summary>
        [AvaloniaFact]
        public void Apply_FilesTheArrivingSessionInTheCache()
        {
            var scene = new Scene();
            var session = scene.Session(profileNumber: 5);

            scene.Loader.Apply(new LoadOutcome { Session = session, Layout = session.Layout });

            Assert.Same(session, scene.Cache.Find(5));
        }

        /// <summary>
        /// Invariant 30: <c>Apply</c> mutates <b>unconditionally</b>, and the outcome is decided
        /// before it is called. A guard in here would be a half-swap — so a failed outcome really
        /// does clear the picture, and it is the caller that must not hand one over.
        /// </summary>
        [AvaloniaFact]
        public void Apply_MutatesUnconditionally_EvenForAnOutcomeThatFailed()
        {
            var scene = new Scene();

            scene.Loader.Apply(new LoadOutcome { Error = new InvalidOperationException("no drive") });

            Assert.Null(scene.Host.Session);
            Assert.Null(scene.Host.Layout);
            Assert.Equal(string.Empty, scene.Host.ProfileCaption);
            Assert.Empty(scene.Host.Layers);
            Assert.Empty(scene.Host.InvalidLineMessages);

            // A failed load files nothing: a null session is not a profile.
            Assert.Null(scene.Cache.Find(1));

            // The funnel still runs — the chrome has to report the empty picture it now shows.
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
            Assert.Null(Assert.Single(scene.Host.SelectedLayers));
        }

        /// <summary>
        /// <b>The single easiest thing to get wrong here.</b> A profile handed back by the cache is
        /// the very layout the user has been renaming macros on, and <c>MacroSites.ApplyNames</c>
        /// writes every site unconditionally — so re-stamping it would silently undo every unsaved
        /// rename, with nothing on screen to say so.
        /// </summary>
        [AvaloniaFact]
        public void Apply_OnACacheHit_DoesNotReStampTheStoredMacroNames()
        {
            var scene = new Scene();
            var session = scene.Session();

            scene.Loader.Apply(new LoadOutcome { Session = session, Layout = session.Layout, IsCacheHit = true });

            var stamp = Assert.Single(scene.Host.MacroNameStamps);

            Assert.Same(session.Layout, stamp.Layout);
            Assert.False(stamp.ApplyStoredNames);
        }

        /// <summary>
        /// The other direction, and it matters just as much: a freshly parsed layout carries no macro
        /// names at all — they ride <c>app_settings.txt</c>, not <c>layout&lt;n&gt;.txt</c> — so they
        /// have to be stamped on before anything reads one.
        /// </summary>
        [AvaloniaFact]
        public void Apply_OnAGenuineLoad_StampsTheStoredMacroNames()
        {
            var scene = new Scene();
            var session = scene.Session();

            scene.Loader.Apply(new LoadOutcome { Session = session, Layout = session.Layout });

            var stamp = Assert.Single(scene.Host.MacroNameStamps);

            Assert.Same(session.Layout, stamp.Layout);
            Assert.True(stamp.ApplyStoredNames);
        }

        /// <summary>
        /// The picture is empty when either half of it is missing — a device whose board has not been
        /// authored yet (#39–#42) is the visual half.
        /// </summary>
        [AvaloniaFact]
        public void Apply_WithNoBoardPicture_LeavesTheLayersEmpty()
        {
            var scene = new Scene(hasBoardPicture: false);
            var session = scene.Session();

            scene.Loader.Apply(new LoadOutcome { Session = session, Layout = session.Layout });

            Assert.Empty(scene.Host.Layers);
            Assert.Null(Assert.Single(scene.Host.SelectedLayers));
        }

        /// <summary>And the model half — a load that failed outright still has a board picture.</summary>
        [AvaloniaFact]
        public void Apply_WithNoLayout_LeavesTheLayersEmpty()
        {
            var scene = new Scene();

            scene.Loader.Apply(new LoadOutcome());

            Assert.Empty(scene.Host.Layers);
            Assert.Null(Assert.Single(scene.Host.SelectedLayers));
        }

        /// <summary>
        /// Lines the parser could not apply are shown, never dropped silently (04 §5), and the
        /// wording is the editor's one builder so the list and the dialog cannot drift.
        /// </summary>
        [AvaloniaFact]
        public void Apply_TurnsUnappliedLinesIntoMessages()
        {
            var scene = new Scene();
            var session = scene.Session();

            session.InvalidLines =
            [
                new LayoutInvalidLine(7, 0, "[bogus]>[nonsense]", []),
                new LayoutInvalidLine(11, 0, "still nonsense", [])
            ];

            scene.Loader.Apply(new LoadOutcome { Session = session, Layout = session.Layout });

            Assert.Equal(
                ["Line 7: [bogus]>[nonsense]", "Line 11: still nonsense"],
                scene.Host.InvalidLineMessages);
        }

        /// <summary>A session with nothing unapplied reports an empty list, not a null one.</summary>
        [AvaloniaFact]
        public void Apply_WithNothingUnapplied_ReportsAnEmptyList()
        {
            var scene = new Scene();
            var session = scene.Session();

            scene.Loader.Apply(new LoadOutcome { Session = session, Layout = session.Layout });

            Assert.Empty(scene.Host.InvalidLineMessages);
        }

        /// <summary>
        /// <c>ApplyReplacedLayout</c> is what a successful import and the layout half of
        /// <c>Discard changes</c> run: the session's model was replaced in place, so everything is
        /// re-read from it — and it is <b>not</b> a cache hit, because the layout really is fresh.
        /// </summary>
        [AvaloniaFact]
        public void ApplyReplacedLayout_RebuildsFromTheSession_AndStampsTheNames()
        {
            var scene = new Scene();
            var session = scene.Session(profileNumber: 2);

            scene.Loader.ApplyReplacedLayout(session);

            Assert.Same(session, scene.Host.Session);
            Assert.Same(session.Layout, scene.Host.Layout);
            Assert.Equal("Profile 2", scene.Host.ProfileCaption);
            Assert.True(Assert.Single(scene.Host.MacroNameStamps).ApplyStoredNames);
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
        }

        /// <summary>The first load reads the device's first profile and fills the picture in.</summary>
        [AvaloniaFact]
        public async Task LoadAsync_ReadsTheFirstProfile_AndFillsThePictureIn()
        {
            var scene = new Scene();

            await scene.Loader.LoadAsync();

            var call = Assert.Single(scene.Sessions.LoadCalls);

            Assert.Equal(DeviceId.FreestyleEdgeRgb, call.Device);
            Assert.Equal(1, call.ProfileNumber);

            Assert.NotNull(scene.Host.Session);
            Assert.NotEmpty(scene.Host.Layers);

            // The indicator goes up before the read and comes down after the picture is in, whatever
            // the read produced.
            Assert.Equal([true, false], scene.Host.LoadingWrites);
        }

        /// <summary>
        /// Invariant 12, half one: <b>idempotent</b>. The shell fires the load and forgets it, and a
        /// second call must not re-read the drive — which is also why the profile picker never
        /// re-enters this method.
        /// </summary>
        [AvaloniaFact]
        public async Task LoadAsync_IsIdempotent()
        {
            var scene = new Scene();

            await scene.Loader.LoadAsync();
            await scene.Loader.LoadAsync();

            Assert.Equal(1, scene.Sessions.LoadCallCount);
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
        }

        /// <summary>
        /// Invariant 12, half two: <b>total</b>. Nothing awaits the task, so an escaping exception
        /// would be an unhandled crash — a drive that vanished degrades to the factory-default layout
        /// and a message box instead.
        /// </summary>
        [AvaloniaFact]
        public async Task LoadAsync_IsTotal_WhenTheDriveThrows()
        {
            var scene = new Scene();

            scene.Sessions.ExceptionToThrow = new IOException("the drive went away");

            await scene.Loader.LoadAsync();

            Assert.Null(scene.Host.Session);
            Assert.NotNull(scene.Host.Layout);
            Assert.Equal(string.Empty, scene.Host.ProfileCaption);
            Assert.NotEmpty(scene.Host.Layers);
            Assert.Equal([true, false], scene.Host.LoadingWrites);

            var box = Assert.Single(scene.Host.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.LoadFailureTitle, box.Title);
            Assert.StartsWith(KeyboardEditorViewModel.LoadFailureMessagePrefix, box.Message, StringComparison.Ordinal);
            Assert.Contains("the drive went away", box.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// A device with no location has no file to read: it edits a factory-default model in memory,
        /// opens no session, and its caption stays empty — which is what makes Save permanently
        /// unavailable there.
        /// </summary>
        [AvaloniaFact]
        public async Task LoadAsync_OnABoardWithNoDrive_EditsAModelInMemory()
        {
            var scene = new Scene(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb) with { Location = null });

            await scene.Loader.LoadAsync();

            Assert.Empty(scene.Sessions.LoadCalls);
            Assert.Null(scene.Host.Session);
            Assert.NotNull(scene.Host.Layout);
            Assert.Equal(string.Empty, scene.Host.ProfileCaption);
            Assert.NotEmpty(scene.Host.Layers);
            Assert.Empty(scene.Host.MessageBoxes);
        }

        /// <summary>A load that lands after the editor is gone fills a picture nobody is looking at.</summary>
        [AvaloniaFact]
        public async Task LoadAsync_AfterTheEditorWentAway_DoesNothing()
        {
            var scene = new Scene();

            scene.Host.IsDisposed = true;

            await scene.Loader.LoadAsync();

            Assert.Empty(scene.Sessions.LoadCalls);
            Assert.Empty(scene.Host.Calls);
        }

        /// <summary>
        /// The profile picker's read asks the cache first: a profile the user has already opened
        /// comes straight back with its edits intact, marked as a hit so the names are not re-stamped
        /// — and no file is touched at all.
        /// </summary>
        [AvaloniaFact]
        public async Task OpenProfileAsync_AsksTheCacheFirst_AndMarksTheHit()
        {
            var scene = new Scene();
            var session = scene.Session(profileNumber: 4);

            scene.Cache.Add(session);

            var outcome = await scene.Loader.OpenProfileAsync(scene.Location, 4);

            Assert.Same(session, outcome.Session);
            Assert.Same(session.Layout, outcome.Layout);
            Assert.True(outcome.IsCacheHit);
            Assert.Empty(scene.Sessions.LoadCalls);
        }

        /// <summary>A first visit reads, and what it read is not a cache hit.</summary>
        [AvaloniaFact]
        public async Task OpenProfileAsync_OnAFirstVisit_ReadsTheDrive()
        {
            var scene = new Scene();
            var staged = scene.Sessions.Stage(6, DeviceId.FreestyleEdgeRgb);

            var outcome = await scene.Loader.OpenProfileAsync(scene.Location, 6);

            Assert.Same(staged, outcome.Session);
            Assert.False(outcome.IsCacheHit);
            Assert.Equal(6, Assert.Single(scene.Sessions.LoadCalls).ProfileNumber);
        }

        /// <summary>
        /// A switch that could not read degrades to <b>nothing</b> rather than to a factory-default
        /// layout, and never throws: the outcome has to be decided before <c>Apply</c> is called, so
        /// that a failed switch leaves the previous profile open with its edits still saveable.
        /// </summary>
        [AvaloniaFact]
        public async Task OpenProfileAsync_WhenTheReadFails_ReportsNothingRatherThanThrowing()
        {
            var scene = new Scene();

            scene.Sessions.ExceptionToThrow = new IOException("gone");

            var outcome = await scene.Loader.OpenProfileAsync(scene.Location, 2);

            Assert.Null(outcome.Session);
            Assert.Null(outcome.Layout);
            Assert.NotNull(outcome.Error);
            Assert.False(outcome.IsCacheHit);
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingCollaborator()
        {
            var host = new FakeEditorProfileLoaderHost();
            var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
            var sessions = new FakeProfileSessionFactory();
            var cache = new ProfileSessionCache();

            Assert.Throws<ArgumentNullException>(
                () => new EditorProfileLoader(null!, device, sessions, cache, VisualCatalog.FreestyleEdgeRgb));

            Assert.Throws<ArgumentNullException>(
                () => new EditorProfileLoader(host, null!, sessions, cache, VisualCatalog.FreestyleEdgeRgb));

            Assert.Throws<ArgumentNullException>(
                () => new EditorProfileLoader(host, device, null!, cache, VisualCatalog.FreestyleEdgeRgb));

            Assert.Throws<ArgumentNullException>(
                () => new EditorProfileLoader(host, device, sessions, null!, VisualCatalog.FreestyleEdgeRgb));
        }

        /// <summary>
        /// A loader over a fake editor, a fake drive and a real session cache — the whole lifecycle
        /// with nothing else in it.
        /// </summary>
        private sealed class Scene
        {
            public FakeEditorProfileLoaderHost Host { get; }

            public FakeProfileSessionFactory Sessions { get; } = new();

            public ProfileSessionCache Cache { get; } = new();

            public EditorProfileLoader Loader { get; }

            /// <summary>The drive the profile picker's read is pointed at.</summary>
            public VDriveLocation Location { get; }

            /// <summary>
            /// <paramref name="hasBoardPicture"/> false is the device whose board has not been
            /// authored yet (#39–#42): the same layout, no picture to place it on.
            /// </summary>
            public Scene(DeviceSnapshot? device = null, bool hasBoardPicture = true)
            {
                Host = new FakeEditorProfileLoaderHost();
                Location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb);

                Loader = new EditorProfileLoader(
                    Host,
                    device ?? TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                    Sessions,
                    Cache,
                    hasBoardPicture ? VisualCatalog.FreestyleEdgeRgb : null);
            }

            /// <summary>A session over a factory-default model, filed under <paramref name="profileNumber"/>.</summary>
            public FakeProfileSession Session(int profileNumber = 1)
            {
                return new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
                {
                    ProfileNumber = profileNumber
                };
            }
        }

        /// <summary>
        /// The editor as <see cref="EditorProfileLoader"/> sees it: every property it writes kept as
        /// a readable value, every call it makes tallied, and both recorded — in order — in
        /// <see cref="Calls"/>, which is what the <c>Apply</c> ordering is asserted on.
        /// <para>
        /// <see cref="Lighting"/> and <see cref="Settings"/> are real panels over fakes, because the
        /// loader really does drive them; reading <see cref="Lighting"/> is itself recorded, so
        /// "the lighting panel is re-parented last" is a claim this suite can make.
        /// </para>
        /// </summary>
        private sealed class FakeEditorProfileLoaderHost : IEditorProfileLoaderHost
        {
            /// <summary>Every write and call the loader made, in order.</summary>
            public List<string> Calls { get; } = [];

            public bool IsDisposed { get; set; }

            /// <summary>Every value the loading flag was set to, in order.</summary>
            public List<bool> LoadingWrites { get; } = [];

            public bool IsLoading
            {
                set
                {
                    LoadingWrites.Add(value);
                }
            }

            public KeyboardLayout? Layout
            {
                get => _layout;
                set
                {
                    _layout = value;

                    Calls.Add(nameof(Layout));
                }
            }

            public string ProfileCaption
            {
                get => _profileCaption;
                set
                {
                    _profileCaption = value;

                    Calls.Add(nameof(ProfileCaption));
                }
            }

            public IReadOnlyList<string> InvalidLineMessages
            {
                get => _invalidLineMessages;
                set
                {
                    _invalidLineMessages = value;

                    Calls.Add(nameof(InvalidLineMessages));
                }
            }

            public IProfileSession? Session
            {
                get => _session;
                set
                {
                    _session = value;

                    Calls.Add(nameof(Session));
                }
            }

            public IReadOnlyList<KeyboardLayerViewModel> Layers
            {
                get => _layers;
                set
                {
                    _layers = value;

                    Calls.Add(nameof(Layers));
                }
            }

            public KeyboardSettingsViewModel Settings { get; }

            public LightingTabViewModel Lighting
            {
                get
                {
                    Calls.Add(nameof(Lighting));

                    return _lighting;
                }
            }

            /// <summary>Every macro-name stamp, with the flag that decides whether it wrote anything.</summary>
            public List<(KeyboardLayout? Layout, bool ApplyStoredNames)> MacroNameStamps { get; } = [];

            /// <summary>Every layer the loader selected — <c>Apply</c> makes exactly one such call.</summary>
            public List<KeyboardLayerViewModel?> SelectedLayers { get; } = [];

            public List<MessageBoxRequest> MessageBoxes { get; } = [];

            public int RefreshSelectedProfileCalls { get; private set; }

            public int RefreshCountersCalls { get; private set; }

            private readonly LightingTabViewModel _lighting;

            private KeyboardLayout? _layout;
            private string _profileCaption = string.Empty;
            private IReadOnlyList<string> _invalidLineMessages = [];
            private IProfileSession? _session;
            private IReadOnlyList<KeyboardLayerViewModel> _layers = [];

            public FakeEditorProfileLoaderHost()
            {
                var device = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
                var notifications = new FakeNotificationService();

                Settings = new KeyboardSettingsViewModel(
                    device,
                    new FakeSettingsService(),
                    notifications,
                    NullAppPreferencesStore.Instance);

                _lighting = new LightingTabViewModel(device, notifications, NullAppPreferencesStore.Instance);
            }

            public void RefreshSelectedProfile()
            {
                RefreshSelectedProfileCalls++;

                Calls.Add(nameof(RefreshSelectedProfile));
            }

            public void AttachMacroNames(KeyboardLayout? layout, bool applyStoredNames)
            {
                MacroNameStamps.Add((layout, applyStoredNames));

                Calls.Add(nameof(AttachMacroNames));
            }

            public void SelectLayer(KeyboardLayerViewModel? layer)
            {
                SelectedLayers.Add(layer);

                Calls.Add(nameof(SelectLayer));
            }

            public void RefreshCounters()
            {
                RefreshCountersCalls++;

                Calls.Add(nameof(RefreshCounters));
            }

            public Task<MessageBoxOutcome?> TryShowMessageBoxAsync(MessageBoxRequest request)
            {
                MessageBoxes.Add(request);

                // The editor's own helper never throws — a box that cannot be put on screen answers
                // null — so neither does this, and the load's totality does not lean on it.
                return Task.FromResult<MessageBoxOutcome?>(new MessageBoxOutcome { Result = MessageBoxResult.Ok });
            }
        }
    }
}
