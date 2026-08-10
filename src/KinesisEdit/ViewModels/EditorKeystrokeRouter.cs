using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Input;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The remap state machine of specs/10-apps-and-ui.md ("click an on-screen key — the key enters
    /// 'listening' state; the next physical keypress captured by the app becomes the new
    /// assignment") and the keystroke routing around it: the one key that listens, the capture
    /// service it turns on, the <b>single</b> subscription every physical keystroke arrives on, the
    /// three-branch precedence that decides who gets it, and the latch that says a sink already
    /// took one.
    /// <para>
    /// It is a type of its own rather than fifteen more members on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What earns it the
    /// file is that all of it answers <b>one</b> question — <i>what does the next physical keystroke
    /// mean?</i> — and that the answer has to be given in exactly one place: invariant 5's "one
    /// keystroke, one target" is a property of this ordering and of nothing else. The state lived
    /// across two partials before, and the Escape latch was written from both of them.
    /// </para>
    /// <para>
    /// <b>It is not a view model.</b> The editor publishes <see cref="ListeningKey"/>,
    /// <see cref="IsListening"/>, <see cref="IsCaptureActive"/> and both commands under its own
    /// names — the listening banner binds two of them and
    /// <c>Views/KeyboardEditorView.axaml.cs</c> reads the third — so a view never sees this type.
    /// Everything it reads about the editor arrives through <see cref="IEditorKeystrokeHost"/>
    /// rather than a back-reference, and it <b>announces nothing</b>: a property may only be raised
    /// by the class that declares it, so the host is told that the listening key moved and raises
    /// the names itself.
    /// </para>
    /// <para>
    /// <b>The applied remap ends in the host's <c>RefreshCounters()</c></b>
    /// (docs/app/keyboard-editor.md, invariant 16): the funnel lives on the editor and stays there,
    /// reached as a call rather than an inlined copy.
    /// </para>
    /// </summary>
    public sealed class EditorKeystrokeRouter : IDisposable
    {
        /// <summary>The key waiting for the next physical keypress, or null when nothing is listening.</summary>
        public KeyboardKeyViewModel? ListeningKey
        {
            get => _listeningKey;
            private set
            {
                // The same "one write per real change" the ObservableObject.SetProperty this
                // replaces enforced: the host raises three names and re-asks every command, and
                // doing that for a key that did not move would put the whole command set through a
                // cycle on every keystroke that was routed somewhere else.
                if (EqualityComparer<KeyboardKeyViewModel?>.Default.Equals(_listeningKey, value))
                {
                    return;
                }

                _listeningKey = value;

                _host.OnListeningChanged();
            }
        }

        /// <summary>Whether a key is currently listening for its new assignment.</summary>
        public bool IsListening => ListeningKey is not null;

        /// <summary>
        /// Whether the next physical keystroke already belongs to somebody: a key waiting for its
        /// new assignment, an armed field of a modal panel, or an armed record button in the key
        /// inspector rail. These are the consumers of "one keystroke, one target" (invariant 5),
        /// and while any of them is live the editor's keyboard grammar is <b>off entirely</b> — a
        /// user assigning ⌘S to a key must get <c>s</c>-with-Meta recorded, not a save.
        /// <para>
        /// <b>The rail's term is not optional here.</b> The rail is not modal, so gate 2 of the
        /// view's key handler (an open overlay owns the keyboard) never fires for it — without it
        /// ⌘S would start serializing the model while a hold action was being recorded.
        /// </para>
        /// </summary>
        public bool IsCaptureActive =>
            IsListening
            || _host.IsOverlayAwaitingKeystroke
            || _host.Inspector.IsRecording;

        /// <summary>Puts the selected key into listening state.</summary>
        public IRelayCommand BeginRemapCommand { get; }

        /// <summary>Leaves listening state without changing anything (the view binds Escape to it).</summary>
        public IRelayCommand CancelRemapCommand { get; }

        private readonly IEditorKeystrokeHost _host;
        private readonly IKeystrokeCaptureService _capture;
        private readonly Action<CapturedKeystroke> _keystrokeCapturedHandler;
        private KeyboardKeyViewModel? _listeningKey;

        /// <summary>
        /// Whether the capture service is running <b>because a rail panel asked for it</b>. It is
        /// the "never stop a capture you did not start" rule made explicit (invariant 4): the rail
        /// raises <see cref="KeyInspectorViewModel.RecordingChanged"/> on every mode switch and
        /// every selection change too, and answering one of those with an unconditional
        /// <c>_capture.Stop()</c> would deafen a panel that really is armed.
        /// </summary>
        private bool _isInspectorCapturing;

        /// <summary>
        /// Set by <see cref="OnKeystrokeCaptured"/> whenever a keystroke went to <b>any</b> sink —
        /// the open modal's, or an armed key-inspector panel's — and read-and-cleared by the view on
        /// the very key event that produced it. See <see cref="TryTakeOverlayKeystroke"/>.
        /// <para>
        /// It is latched by the <em>router</em> rather than announced by the panel, so there is one
        /// answer to "did a sink take this one" no matter which sink took it — and, since issue
        /// #154, one field: it used to be written from two partials of the editor and cleared from
        /// two more.
        /// </para>
        /// </summary>
        private bool _hasOverlayTakenKeystroke;

        private bool _isDetached;

        /// <summary>
        /// Builds the state machine over the editor it remaps and the app-wide capture service it
        /// drives, and takes the editor's <b>one</b> subscription to
        /// <see cref="IKeystrokeCaptureService.KeystrokeCaptured"/> — which is what keeps spec 10's
        /// routing order in a single method.
        /// <para>
        /// <paramref name="capture"/> is the same instance <see cref="EditorOverlayHost"/> is handed:
        /// two consumers of one service, which is exactly why both of them only ever stop what they
        /// started.
        /// </para>
        /// </summary>
        public EditorKeystrokeRouter(IEditorKeystrokeHost host, IKeystrokeCaptureService capture)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));

            BeginRemapCommand = new RelayCommand(BeginRemap, () => CanBeginRemap());
            CancelRemapCommand = new RelayCommand(CancelRemap, () => IsListening);

            _keystrokeCapturedHandler = OnKeystrokeCaptured;
            _capture.KeystrokeCaptured += _keystrokeCapturedHandler;
        }

        /// <summary>
        /// Whether the selected position may start listening. False for a locked position
        /// (<c>CanEdit == false</c>, 05 §5.3), while loading, while saving, <b>while a rail panel
        /// records and while a modal feature panel is open</b> — the last two would fight over the
        /// same keystrokes.
        /// <para>
        /// <b>It is deliberately *not* false while the key inspector is merely open</b>
        /// (invariant 26): the rail is not modal, and clicking the selected cap again must still
        /// start a remap.
        /// </para>
        /// </summary>
        public bool CanBeginRemap()
        {
            // An armed rail panel and a listening key would fight over the same keystrokes, and an
            // open feature panel owns them outright, so neither may start a remap.
            return _host.SelectedKey is not null
                   && _host.SelectedKey.CanEdit
                   && !_host.IsLoading
                   && !_host.IsBusy
                   && !_host.Inspector.IsRecording
                   && _host.ActiveOverlay is null;
        }

        /// <summary>
        /// Enters listening state: from here the next captured keystroke becomes the assignment.
        /// Only one key ever listens, so an in-flight listen is cancelled first.
        /// </summary>
        public void BeginRemap()
        {
            if (!CanBeginRemap())
            {
                return;
            }

            CancelRemap();

            // A listening key and an armed copy are two different answers to "what does the next
            // input mean", so only one of them is ever live. Arming the copy ends a listen for the
            // same reason (ArmCopyKey), which is what makes the Escape order unambiguous.
            _host.CancelCopyKey();

            var key = _host.SelectedKey!;

            key.IsListening = true;
            ListeningKey = key;

            _capture.Start();
        }

        /// <summary>Leaves listening state and stops capture; the model is untouched.</summary>
        public void CancelRemap()
        {
            if (ListeningKey is null)
            {
                return;
            }

            StopListening();
        }

        /// <summary>
        /// Leaves listening state and hands the capture service back, whether or not anything was
        /// listening. The editor's <c>Dispose</c> is the caller that needs the unconditional shape.
        /// </summary>
        public void StopListening()
        {
            if (ListeningKey is not null)
            {
                ListeningKey.IsListening = false;
                ListeningKey = null;
            }

            // Capture is never left running: it swallows keystrokes from the rest of the app while
            // it is on (docs/app/keystroke-capture.md).
            _capture.Stop();
        }

        /// <summary>
        /// Whether the keystroke being handled right now was already taken by an open panel's or an
        /// armed rail panel's own keystroke sink — and clears the record as it answers, so one
        /// keystroke is reported once.
        /// <para>
        /// It exists because <c>IsOverlayAwaitingKeystroke</c> cannot answer the question. The
        /// capture service previews the window's key events in the <b>tunnel</b> phase, above the
        /// editor's view, so by the time the view's own handler runs the sink has already received
        /// the key <em>and disarmed</em> — leaving the panel looking idle. Escape read that as
        /// "nothing is waiting" and closed the whole panel in the same keypress it was meant only to
        /// fill an armed field with.
        /// </para>
        /// <para>
        /// The view reads it on <b>every</b> key down it sees, not only on Escape, so a latch set by
        /// an ordinary keystroke cannot survive into a later Escape.
        /// </para>
        /// </summary>
        public bool TryTakeOverlayKeystroke()
        {
            var taken = _hasOverlayTakenKeystroke;

            _hasOverlayTakenKeystroke = false;

            return taken;
        }

        /// <summary>
        /// Drops the latch without reading it: the claim belongs to the panel that set it, so the
        /// editor calls this when the open modal is swapped or dismissed.
        /// </summary>
        public void ClearOverlayKeystroke()
        {
            _hasOverlayTakenKeystroke = false;
        }

        /// <summary>
        /// The showing rail panel started or stopped waiting for a physical keystroke. Capture
        /// belongs to the editor and never to a panel (invariant 4), so this is where the service is
        /// turned on and off — with one guard a plain start/stop would not need:
        /// <see cref="_isInspectorCapturing"/>, because the rail announces its recording state on
        /// every mode switch and every selection change, not only when something really armed.
        /// </summary>
        public void OnInspectorRecordingChanged()
        {
            var isRecording = _host.Inspector.IsRecording;

            _host.OnCaptureActiveChanged();

            if (isRecording == _isInspectorCapturing)
            {
                return;
            }

            _isInspectorCapturing = isRecording;

            if (isRecording)
            {
                // A listening key, a recording macro and an armed rail field are three answers to
                // "what does the next keystroke mean", and only one of them may be live
                // (invariant 5).
                CancelRemap();

                _capture.Start();
            }
            else
            {
                _capture.Stop();
            }

            _host.NotifyCommands();
        }

        /// <summary>
        /// Belt to the brace of <see cref="OnInspectorRecordingChanged"/>, run by the editor as it
        /// drops the rail's subscriptions: a panel that never announced itself cannot leave a claim
        /// on the capture service behind.
        /// </summary>
        public void StopInspectorCapture()
        {
            if (!_isInspectorCapturing)
            {
                return;
            }

            _isInspectorCapturing = false;

            _capture.Stop();
        }

        /// <summary>
        /// Comes off the capture service for good. The service is app-wide and outlives the editor,
        /// so a subscription left behind would go on routing the dashboard's keystrokes into a
        /// board that is gone. Safe to call multiple times.
        /// </summary>
        public void Detach()
        {
            if (_isDetached)
            {
                return;
            }

            _isDetached = true;

            _capture.KeystrokeCaptured -= _keystrokeCapturedHandler;
        }

        /// <summary>
        /// The routing rule of specs/10-apps-and-ui.md: "a captured key is forwarded to the Tap
        /// and Hold dialog <b>if that dialog is open</b>; otherwise it is applied as a remap, or
        /// appended to the active macro when the macro-entry box is focused". One keystroke reaches
        /// exactly one target, in that order — an armed rail panel first, then an open modal sink,
        /// then a listening key. This class owns the single subscription, so the order lives in one
        /// place.
        /// <para>
        /// A <b>modal</b> sink takes precedence on being <em>open</em>, not on being armed: a panel
        /// with no field armed swallows the keystroke and discards it, which is what keeps anything
        /// under a scrim from quietly consuming keys aimed at the panel. The <b>rail</b> is the
        /// opposite, and deliberately so — see <see cref="TryRouteToInspector"/>.
        /// </para>
        /// </summary>
        private void OnKeystrokeCaptured(CapturedKeystroke keystroke)
        {
            if (keystroke is null)
            {
                return;
            }

            // Ahead of everything, because an armed record button in the rail is the most specific
            // claim on the next keystroke there is. It is also the only branch here that tests
            // WantsKeystrokes rather than mere existence.
            if (TryRouteToInspector(keystroke))
            {
                return;
            }

            if (_host.ActiveOverlay is IKeystrokeSink overlaySink)
            {
                // Latched for the view, which is still to see this same key event: the sink may
                // disarm as it takes the key, and "no field is armed any more" must not read as
                // "the panel was idle, close it" (see TryTakeOverlayKeystroke).
                _hasOverlayTakenKeystroke = true;

                overlaySink.ReceiveKeystroke(keystroke);

                return;
            }

            ApplyRemap(keystroke);
        }

        /// <summary>
        /// Whether the keystroke being routed belongs to an armed rail panel — the first branch of
        /// <see cref="OnKeystrokeCaptured"/>.
        /// <para>
        /// <b>Armed, not merely open</b>, and that is the one real difference from the modal the
        /// rail replaces. A feature panel took a keystroke on being <em>open</em>, because the scrim
        /// under it meant nothing else could want one; the rail is not modal, so a panel that is
        /// merely showing must let the keystroke fall through to the cap the user is remapping
        /// beside it.
        /// </para>
        /// </summary>
        private bool TryRouteToInspector(CapturedKeystroke keystroke)
        {
            if (_host.Inspector.ActivePanel is not IKeystrokeSink { WantsKeystrokes: true } sink)
            {
                return false;
            }

            // Latched for the view, which is still to see this same key event: the field disarms as
            // it takes the key, and "nothing is armed any more" must not read as "the rail was idle,
            // close it" (see TryTakeOverlayKeystroke). It is set BEFORE the dispatch for exactly
            // that reason — afterwards is already too late.
            _hasOverlayTakenKeystroke = true;

            sink.ReceiveKeystroke(keystroke);

            return true;
        }

        /// <summary>
        /// The last branch: the keystroke becomes the listening key's assignment. <b>Escape lands
        /// here like any other key</b> (invariant 6) — a keyboard must be able to carry an Escape
        /// remap, so there is deliberately no Escape branch anywhere above.
        /// </summary>
        private void ApplyRemap(CapturedKeystroke keystroke)
        {
            var key = ListeningKey;

            if (key is null)
            {
                return;
            }

            // Remap(originalKey) is how "assign the key its own action" clears the remap
            // (04 §2.1) — the capture path deliberately goes through the editor path so that
            // pressing a key's own default un-does it, exactly like the legacy apps.
            key.Key.Remap(keystroke.Key);

            StopListening();

            key.RefreshFromModel();
            _host.RefreshCounters();
        }

        /// <summary>
        /// Comes off the service and leaves listening state, in that order. The editor runs the two
        /// halves <b>separately</b> — <see cref="Detach"/> first and <see cref="StopListening"/>
        /// last — because its own teardown has the rail's and the overlay host's between them, and
        /// the order of that sequence is load-bearing; this is the shape for everybody else.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            Detach();

            StopListening();
        }
    }

    /// <summary>
    /// What <see cref="EditorKeystrokeRouter"/> needs from the editor it remaps: the selection a
    /// remap is about, the two surfaces that outrank it, the two flags every editing gate carries,
    /// and the five calls routing a keystroke makes.
    /// <para>
    /// A narrow interface rather than a bundle of delegates because there are eleven members, and a
    /// <see cref="KeyboardEditorViewModel"/> back-reference rather than either would let the router
    /// reach the whole god class it was split out of. It is implemented by the editor, explicitly
    /// where the member is not already part of that class's public surface — a split must not make
    /// a private method public.
    /// </para>
    /// <para>
    /// <b>The last three are announcement callbacks</b>, and they are what keeps rule 3 ("a
    /// collaborator announces nothing") intact while five bindable members live out here: the
    /// router reports that something moved and the editor raises its own property names.
    /// </para>
    /// </summary>
    public interface IEditorKeystrokeHost
    {
        /// <summary>The key every key-scoped action applies to; the position a remap is started on.</summary>
        KeyboardKeyViewModel? SelectedKey { get; }

        /// <summary>
        /// The key inspector rail: whether a panel of it is recording, and which panel is showing.
        /// It is the router's first branch and one term of <c>CanBeginRemap</c> — but never a
        /// modal, so it does not stop a remap merely by being open (invariant 26).
        /// </summary>
        KeyInspectorViewModel Inspector { get; }

        /// <summary>The feature panel over the editor, or null. An open one owns the keystrokes outright.</summary>
        EditorOverlayViewModel? ActiveOverlay { get; }

        /// <summary>
        /// Whether that panel is itself waiting for the next physical keystroke — an armed Tap and
        /// Hold field. One term of <c>IsCaptureActive</c>, read from the editor rather than
        /// recomputed here so there is one expression for it.
        /// </summary>
        bool IsOverlayAwaitingKeystroke { get; }

        /// <summary>Whether the profile is still being read.</summary>
        bool IsLoading { get; }

        /// <summary>Whether a save is in flight.</summary>
        bool IsBusy { get; }

        /// <summary>
        /// Drops an armed <c>Copy key…</c>. A listening key and an armed copy are two answers to
        /// "what does the next input mean", so starting a remap ends one.
        /// </summary>
        void CancelCopyKey();

        /// <summary>
        /// The editor's one post-write funnel (docs/app/keyboard-editor.md, invariant 16). An
        /// applied remap ends in it, as a call rather than an inlined copy.
        /// </summary>
        void RefreshCounters();

        /// <summary>Re-asks every editing command's predicate; the editor decides which ones those are.</summary>
        void NotifyCommands();

        /// <summary>
        /// The listening key moved: the editor raises <c>ListeningKey</c> and <c>IsListening</c>,
        /// then re-evaluates the commands — the three things its own setter did before the state
        /// moved out here, in that order and no others.
        /// <para>
        /// <c>IsCaptureActive</c> is deliberately <b>not</b> among them, because the setter it
        /// replaces did not raise it either: nothing binds that property, it is read on demand by
        /// the view's key handler, and adding a raise here would be a behaviour change dressed as
        /// tidiness.
        /// </para>
        /// </summary>
        void OnListeningChanged();

        /// <summary>
        /// A rail panel armed or disarmed: the editor raises <c>IsCaptureActive</c>. Unconditional,
        /// exactly as before — the rail announces its recording state on every mode switch, and the
        /// raise is what the view's grammar gate re-reads.
        /// </summary>
        void OnCaptureActiveChanged();
    }
}
