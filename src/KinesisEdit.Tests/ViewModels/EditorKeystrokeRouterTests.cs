using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The remap state machine and spec 10's keystroke routing on their own — <b>no editor, no
    /// profile session and no drive</b>. That is the point of the type existing: every claim about
    /// what a captured keystroke means, who owns the capture service and when a key stops
    /// listening used to have to be made through a loaded <c>KeyboardEditorViewModel</c>.
    /// <para>
    /// The editor is a fake host that records the five calls the router makes on it, so this suite
    /// watches the router's own behaviour rather than a board it does not own. The rail is real —
    /// <see cref="KeyInspectorViewModel"/> is the type the router's first branch is written
    /// against — with one recording panel in it, wired to the router exactly as the editor's
    /// <c>CreateInspector</c> wires it.
    /// </para>
    /// </summary>
    public class EditorKeystrokeRouterTests
    {
        [AvaloniaFact]
        public void BeginRemap_PutsTheSelectedKeyIntoListening_AndStartsCapture()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            Assert.Same(key, scene.Router.ListeningKey);
            Assert.True(scene.Router.IsListening);
            Assert.True(scene.Router.IsCaptureActive);
            Assert.True(key.IsListening);
            Assert.Equal(1, scene.Capture.StartCount);
            Assert.Equal(0, scene.Capture.StopCount);

            // The listening key moved once, so the editor was asked to announce it once — the
            // "one write per real change" rule the ObservableObject setter used to enforce.
            Assert.Equal(1, scene.Host.ListeningChangedCalls);

            // An armed copy is the other answer to "what does the next input mean", so it is ended
            // on the way in.
            Assert.Equal(1, scene.Host.CancelCopyKeyCalls);
        }

        [AvaloniaFact]
        public void ACapturedKeystroke_WhileAKeyListens_IsAppliedAndEndsTheListen()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Equal(TestLayouts.Gen1Key("z"), key.Key.ModifiedOrOriginalKey);
            Assert.True(key.Key.IsModified);
            Assert.False(scene.Router.IsListening);
            Assert.False(key.IsListening);

            // Capture is never left running, and the write ends in the editor's one funnel
            // (invariant 16) — as a call, never as an inlined copy.
            Assert.Equal(1, scene.Capture.StopCount);
            Assert.False(scene.Capture.IsCapturing);
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaFact]
        public void CancelRemap_LeavesListeningAndStopsCapture_WithTheModelUntouched()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            Assert.True(scene.Router.CancelRemapCommand.CanExecute(null));

            scene.Router.CancelRemapCommand.Execute(null);

            Assert.False(scene.Router.IsListening);
            Assert.False(key.IsListening);
            Assert.False(key.Key.IsModified);
            Assert.Equal(1, scene.Capture.StopCount);

            // Nothing was written, so nothing went through the funnel — and the command is
            // unavailable again, which is what keeps the view's Escape route from double-firing.
            Assert.Equal(0, scene.Host.RefreshCountersCalls);
            Assert.False(scene.Router.CancelRemapCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void Dispose_WhileAKeyListens_StopsCaptureAndComesOffTheService()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            Assert.True(scene.Capture.IsCapturing);
            Assert.True(scene.Capture.HasSubscribers);

            scene.Router.Dispose();

            Assert.False(scene.Router.IsListening);
            Assert.False(key.IsListening);
            Assert.False(scene.Capture.IsCapturing);

            // The service is app-wide and outlives the editor: a subscription left behind would go
            // on routing the dashboard's keystrokes into a board that is gone.
            Assert.False(scene.Capture.HasSubscribers);

            // Idempotent — the editor's own teardown runs the two halves apart, and either may be
            // reached twice.
            scene.Router.Dispose();

            Assert.False(scene.Capture.HasSubscribers);
        }

        /// <summary>
        /// The three-branch precedence of "one keystroke, one target" (invariant 5), from the top:
        /// an armed rail panel is the most specific claim on the next keystroke there is, so it
        /// takes it ahead of an open modal <b>and</b> a listening key.
        /// </summary>
        [AvaloniaFact]
        public void AnArmedRailPanel_TakesTheKeystrokeAheadOfAModalAndAListeningKey()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();
            var overlay = new SinkOverlay();

            scene.Router.BeginRemapCommand.Execute(null);

            scene.Host.ActiveOverlay = overlay;
            scene.Host.Panel.Arm();

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Equal(TestLayouts.Gen1Key("z"), Assert.Single(scene.Host.Panel.Received).Key);
            Assert.Empty(overlay.Received);
            Assert.False(key.Key.IsModified);
        }

        /// <summary>
        /// Branch 2. A <b>modal</b> sink wins on being <em>open</em>, which is spec 10's own wording
        /// ("forwarded to the Tap and Hold dialog <b>if that dialog is open</b>"): an unarmed modal
        /// still swallows the keystroke and drops it, so nothing under a scrim can consume keys
        /// aimed at the panel.
        /// </summary>
        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void AnOpenModalSink_TakesTheKeystrokeAheadOfAListeningKey_ArmedOrNot(bool wantsKeystrokes)
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            scene.Host.ActiveOverlay = new SinkOverlay { WantsKeystrokes = wantsKeystrokes };

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Single(((SinkOverlay)scene.Host.ActiveOverlay).Received);
            Assert.False(key.Key.IsModified);
            Assert.True(scene.Router.IsListening);
        }

        /// <summary>
        /// Branch 1 is the only one that tests <c>WantsKeystrokes</c> rather than mere existence,
        /// and that difference is the one real behavioural gap between the rail and the modals it
        /// replaced: a modal covers the board, the rail sits beside a live one.
        /// </summary>
        [AvaloniaFact]
        public void AnOpenButUnarmedRailPanel_LetsTheKeystrokeFallThroughToTheBoard()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            Assert.Same(scene.Host.Panel, scene.Host.Inspector.ActivePanel);

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Empty(scene.Host.Panel.Received);
            Assert.Equal(TestLayouts.Gen1Key("z"), key.Key.ModifiedOrOriginalKey);
        }

        [AvaloniaFact]
        public void AKeystrokeWithNothingListening_WritesNothingAnywhere()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.False(key.Key.IsModified);
            Assert.Equal(0, scene.Host.RefreshCountersCalls);
        }

        /// <summary>
        /// Who turns the service on, and who is allowed to turn it off: a listening key starts it
        /// for itself, an armed rail panel starts it for itself, and each stops only what it
        /// started.
        /// </summary>
        [AvaloniaFact]
        public void CaptureIsStartedByWhicheverConsumerNeedsIt_AndByNobodyElse()
        {
            var scene = new Scene();

            scene.SelectFirstKey();

            Assert.False(scene.Capture.IsCapturing);
            Assert.False(scene.Router.IsCaptureActive);

            scene.Host.Panel.Arm();

            Assert.True(scene.Capture.IsCapturing);
            Assert.True(scene.Router.IsCaptureActive);
            Assert.Equal(1, scene.Capture.StartCount);

            scene.Host.Panel.Disarm();

            Assert.False(scene.Capture.IsCapturing);
            Assert.False(scene.Router.IsCaptureActive);
            Assert.Equal(1, scene.Capture.StopCount);

            // An armed rail panel and a listening key are two answers to the same question, so
            // arming one ends the other (invariant 5).
            scene.Router.BeginRemapCommand.Execute(null);

            Assert.True(scene.Router.IsListening);

            scene.Host.Panel.Arm();

            Assert.False(scene.Router.IsListening);
            Assert.True(scene.Capture.IsCapturing);
        }

        /// <summary>
        /// <b>Invariant 4 — never stop a capture you did not start.</b> The rail announces its
        /// recording state on every mode switch and every selection change, not only when something
        /// really armed, so answering one of those with an unconditional <c>Stop()</c> would
        /// silently deafen the key that owns the service.
        /// </summary>
        [AvaloniaFact]
        public void TheRailsRecordingAnnouncement_NeverStopsACaptureItDidNotStart()
        {
            var scene = new Scene();

            scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            Assert.True(scene.Capture.IsCapturing);

            // A mode switch: the rail says "recording state" again, unchanged and false.
            scene.Router.OnInspectorRecordingChanged();
            scene.Router.OnInspectorRecordingChanged();

            Assert.True(scene.Capture.IsCapturing);
            Assert.True(scene.Router.IsListening);
            Assert.Equal(0, scene.Capture.StopCount);

            // ...and it is raised unconditionally, because the view's grammar gate re-reads it.
            Assert.Equal(2, scene.Host.CaptureActiveChangedCalls);

            // The commands are re-asked only where something really moved: once for the listen
            // that started, and not at all for the two announcements that changed nothing.
            Assert.Equal(1, scene.Host.NotifyCommandsCalls);
        }

        /// <summary>
        /// The same flag from the other side: the editor drops the rail's subscriptions on the way
        /// out and asks the router to release any claim the rail still holds — belt to invariant 4's
        /// brace, and a no-op when the rail never armed.
        /// </summary>
        [AvaloniaFact]
        public void StopInspectorCapture_ReleasesOnlyAClaimTheRailReallyMade()
        {
            var scene = new Scene();

            scene.SelectFirstKey();
            scene.Router.StopInspectorCapture();

            Assert.Equal(0, scene.Capture.StopCount);

            scene.Host.Panel.Arm();

            Assert.True(scene.Capture.IsCapturing);

            scene.Router.StopInspectorCapture();

            Assert.False(scene.Capture.IsCapturing);
            Assert.Equal(1, scene.Capture.StopCount);
        }

        /// <summary>
        /// The Escape latch, which exists because the capture service previews the window's key
        /// events <em>above</em> the editor's view: a sink disarms as it takes the key, so "nothing
        /// is armed any more" must not read as "the panel was idle, close it". It is set for
        /// <b>every</b> sink, which is why no panel carries a signal of its own.
        /// </summary>
        [AvaloniaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void TheOverlayLatch_IsSetByEitherSink_AndReadExactlyOnce(bool throughTheRail)
        {
            var scene = new Scene();

            scene.SelectFirstKey();

            if (throughTheRail)
            {
                scene.Host.Panel.Arm();
            }
            else
            {
                scene.Host.ActiveOverlay = new SinkOverlay();
            }

            Assert.False(scene.Router.TryTakeOverlayKeystroke());

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.True(scene.Router.TryTakeOverlayKeystroke());

            // Read-and-clear: the view reads it on every key down, so a latch set by an ordinary
            // keystroke cannot answer for a later Escape.
            Assert.False(scene.Router.TryTakeOverlayKeystroke());
        }

        [AvaloniaFact]
        public void TheOverlayLatch_IsDroppedWhenTheOpenModalChanges()
        {
            var scene = new Scene();

            scene.SelectFirstKey();

            scene.Host.ActiveOverlay = new SinkOverlay();

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            // The claim belongs to the panel that set it, so the editor drops it on a swap or a
            // dismiss — the one clear that does not also read the answer.
            scene.Router.ClearOverlayKeystroke();

            Assert.False(scene.Router.TryTakeOverlayKeystroke());
        }

        [AvaloniaFact]
        public void AKeystrokeNobodyTook_LeavesTheLatchAlone()
        {
            var scene = new Scene();

            scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.False(scene.Router.TryTakeOverlayKeystroke());
        }

        /// <summary>
        /// <b>Invariant 6 — Escape is a remappable key, not a shortcut.</b> The capture service
        /// swallows every physical position it resolves while a key listens, Escape included, so
        /// the keystroke arrives here like any other and becomes the assignment. There is
        /// deliberately no Escape branch anywhere in this class.
        /// </summary>
        [AvaloniaFact]
        public void EscapeCapturedWhileAKeyListens_IsAssignedRatherThanTreatedAsCancel()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            scene.Router.BeginRemapCommand.Execute(null);

            Assert.True(scene.Router.CancelRemapCommand.CanExecute(null));

            scene.Capture.RaiseKeystroke(TestLayouts.Gen1Key("esc"));

            Assert.True(key.Key.IsModified);
            Assert.Equal(TestLayouts.Gen1Key("esc"), key.Key.ModifiedOrOriginalKey);
            Assert.False(scene.Router.IsListening);

            // By the time the view's own Escape handler runs, listening is over — which is what
            // stops the assignment and the cancel from double-firing.
            Assert.False(scene.Router.CancelRemapCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void CanBeginRemap_NeedsAnEditableSelection()
        {
            var scene = new Scene();

            Assert.False(scene.Router.CanBeginRemap());

            scene.SelectFirstKey();

            Assert.True(scene.Router.CanBeginRemap());

            var locked = Scene.Locked();

            locked.Host.SelectedKey = locked.Key(1);

            // A locked position (05 §5.3): clicking it twice selects it and does nothing else.
            Assert.False(locked.Router.CanBeginRemap());
            Assert.False(locked.Host.SelectedKey!.CanEdit);
        }

        [AvaloniaTheory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void CanBeginRemap_IsRefusedWhileTheEditorIsReadingOrWriting(bool isLoading, bool isBusy)
        {
            var scene = new Scene();

            scene.SelectFirstKey();

            Assert.True(scene.Router.CanBeginRemap());

            scene.Host.IsLoading = isLoading;
            scene.Host.IsBusy = isBusy;

            Assert.False(scene.Router.CanBeginRemap());
            Assert.False(scene.Router.BeginRemapCommand.CanExecute(null));
        }

        /// <summary>
        /// The two states that would fight over the same keystrokes, and the one that deliberately
        /// does not: <b><c>CanBeginRemap</c> is not false while the key inspector is merely
        /// open</b> (invariant 26). The rail is not modal, and clicking the selected cap again must
        /// still start a remap.
        /// </summary>
        [AvaloniaFact]
        public void CanBeginRemap_IsRefusedByARecordingRailAndAnOpenModal_ButNeverByAnOpenRail()
        {
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            // The rail really is open on the position, which is what the editor's own selection
            // path leaves behind (RefreshInspector, then Open).
            scene.Host.Inspector.Refresh(key, scene.Layers[0], scene.Layout, EditorAdvisories.Empty);
            scene.Host.Inspector.Open();

            Assert.True(scene.Host.Inspector.IsOpen);
            Assert.True(scene.Router.CanBeginRemap());

            scene.Host.Panel.Arm();

            Assert.False(scene.Router.CanBeginRemap());

            scene.Host.Panel.Disarm();

            Assert.True(scene.Router.CanBeginRemap());

            scene.Host.ActiveOverlay = new SinkOverlay();

            Assert.False(scene.Router.CanBeginRemap());
        }

        [AvaloniaFact]
        public void BeginRemap_OnAPositionItRefuses_ArmsNothing()
        {
            var scene = Scene.Locked();

            scene.Host.SelectedKey = scene.Key(1);

            scene.Router.BeginRemapCommand.Execute(null);

            Assert.False(scene.Router.IsListening);
            Assert.Equal(0, scene.Capture.StartCount);
            Assert.Equal(0, scene.Host.ListeningChangedCalls);
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingCollaborator()
        {
            var host = new FakeEditorKeystrokeHost();
            var capture = new FakeKeystrokeCaptureService();

            Assert.Throws<ArgumentNullException>(() => new EditorKeystrokeRouter(null!, capture));
            Assert.Throws<ArgumentNullException>(() => new EditorKeystrokeRouter(host, null!));
        }

        /// <summary>A router over a real RGB model, a real rail and a fake editor, with nothing else.</summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public IReadOnlyList<KeyboardLayerViewModel> Layers { get; }

            public FakeEditorKeystrokeHost Host { get; }

            public FakeKeystrokeCaptureService Capture { get; }

            public EditorKeystrokeRouter Router { get; }

            public Scene()
                : this(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb), VisualCatalog.FreestyleEdgeRgb)
            {
            }

            private Scene(KeyboardLayout layout, KeyboardVisual visual)
            {
                Layout = layout;
                Layers = KeyboardLayerViewModel.BuildAll(layout, visual, lighting: null);

                Host = new FakeEditorKeystrokeHost();
                Capture = new FakeKeystrokeCaptureService();
                Router = new EditorKeystrokeRouter(Host, Capture);

                // The one line KeyboardEditorViewModel.CreateInspector writes: the panels announce
                // that they armed or disarmed, and the router turns the service on and off around
                // them. Capture belongs to the editor and never to a panel (invariant 4).
                Host.Inspector.RecordingChanged += (_, _) => Router.OnInspectorRecordingChanged();
            }

            /// <summary>A scene over the fixture whose second position can never be remapped.</summary>
            public static Scene Locked()
            {
                return new Scene(TestLayouts.CreateLockedKeyLayout(), TestLayouts.CreateVisual(0, 1, 2));
            }

            public KeyboardKeyViewModel Key(int index)
            {
                return Layers[0].FindByIndex(index)!;
            }

            public KeyboardKeyViewModel SelectFirstKey()
            {
                var key = Layers[0].Keys[0];

                Host.SelectedKey = key;

                return key;
            }
        }

        /// <summary>
        /// The editor as <see cref="EditorKeystrokeRouter"/> sees it: the state the routing and the
        /// remap gate are decided from, and a tally of the five calls the router makes back.
        /// </summary>
        private sealed class FakeEditorKeystrokeHost : IEditorKeystrokeHost
        {
            public KeyboardKeyViewModel? SelectedKey { get; set; }

            public KeyInspectorViewModel Inspector { get; }

            /// <summary>The rail's one panel — a sink that can be armed and disarmed by hand.</summary>
            public RecordingPanel Panel { get; }

            public EditorOverlayViewModel? ActiveOverlay { get; set; }

            /// <summary>The editor's own expression, verbatim: the router reads it rather than recomputing it.</summary>
            public bool IsOverlayAwaitingKeystroke => ActiveOverlay is IKeystrokeSink { WantsKeystrokes: true };

            public bool IsLoading { get; set; }

            public bool IsBusy { get; set; }

            public int CancelCopyKeyCalls { get; private set; }

            public int RefreshCountersCalls { get; private set; }

            public int NotifyCommandsCalls { get; private set; }

            public int ListeningChangedCalls { get; private set; }

            public int CaptureActiveChangedCalls { get; private set; }

            public FakeEditorKeystrokeHost()
            {
                Panel = new RecordingPanel();

                Inspector = new KeyInspectorViewModel(
                    new RelayCommand(() => { }),
                    new RelayCommand(() => { }),
                    new RelayCommand(() => { }),
                    [Panel]);
            }

            public void CancelCopyKey()
            {
                CancelCopyKeyCalls++;
            }

            public void RefreshCounters()
            {
                RefreshCountersCalls++;
            }

            public void NotifyCommands()
            {
                NotifyCommandsCalls++;
            }

            public void OnListeningChanged()
            {
                ListeningChangedCalls++;

                NotifyCommands();
            }

            public void OnCaptureActiveChanged()
            {
                CaptureActiveChangedCalls++;
            }
        }

        /// <summary>
        /// A rail panel that arms and disarms on demand — the shape every real one has: it never
        /// touches the capture service, it says whether it is waiting through
        /// <see cref="IsRecording"/>, and it takes the keystroke as an
        /// <see cref="IKeystrokeSink"/>.
        /// </summary>
        private sealed class RecordingPanel : KeyInspectorPanelViewModel, IKeystrokeSink
        {
            public override KeyInspectorMode Mode => KeyInspectorMode.Remap;

            public override string Title => "Remap";

            public override bool IsRecording => _isRecording;

            public bool WantsKeystrokes => _isRecording;

            public List<CapturedKeystroke> Received { get; } = [];

            private bool _isRecording;

            public void Arm()
            {
                SetRecording(true);
            }

            public void Disarm()
            {
                SetRecording(false);
            }

            public override void Refresh(
                KeyboardKeyViewModel? key,
                KeyboardLayerViewModel? layer,
                KeyboardLayout? layout,
                EditorAdvisories advisories)
            {
            }

            public override void Deactivate()
            {
                SetRecording(false);
            }

            public void ReceiveKeystroke(CapturedKeystroke keystroke)
            {
                Received.Add(keystroke);
            }

            private void SetRecording(bool value)
            {
                if (_isRecording == value)
                {
                    return;
                }

                _isRecording = value;

                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(WantsKeystrokes));
                OnRecordingChanged();
            }
        }

        /// <summary>A feature panel that consumes captured keystrokes — the Tap and Hold shape.</summary>
        private sealed class SinkOverlay : EditorOverlayViewModel, IKeystrokeSink
        {
            public bool WantsKeystrokes { get; init; } = true;

            public List<CapturedKeystroke> Received { get; } = [];

            public SinkOverlay() : base("Tap and Hold")
            {
            }

            public void ReceiveKeystroke(CapturedKeystroke keystroke)
            {
                Received.Add(keystroke);
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }
    }
}
