using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The editor's three reset scopes — one key, one layer, the whole profile — the confirmation
    /// the two wider ones ask first, and the wording that names what each is about to erase.
    /// <para>
    /// It is a type of its own rather than three more commands on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What earns it the
    /// file is one rule: <b>a destructive prompt names its scope</b> (docs/app/keyboard-editor.md,
    /// invariant 34). The whole-profile sentence and the button that affirms it are both built from
    /// one <see cref="ResolveResetScope"/> — issue #135 is what happens when they are allowed to
    /// disagree — and keeping the two strings in the same file as the command they guard is what
    /// keeps a later edit from forking them again.
    /// </para>
    /// <para>
    /// <b>It is not a view model.</b> The editor publishes the three commands under its own names,
    /// so a view never sees this type; <c>ResetLayoutCommand</c> is bound in
    /// <c>Views/KeyboardEditorView.axaml</c> and the other two are handed to
    /// <see cref="KeyInspectorViewModel"/> and <see cref="BoardLegendViewModel"/> as they always
    /// were. Everything it reads about the editor arrives through <see cref="IResetScopeHost"/>
    /// rather than a back-reference, and <b>every erase it performs ends in that host's
    /// <c>RefreshCounters()</c></b> — the funnel invariant 16 is about, which lives on the editor
    /// and stays there.
    /// </para>
    /// <para>
    /// <b>Two of the three stand-downs, deliberately.</b> The layer and layout resets cancel a
    /// listening key and stand the rail down; they do <em>not</em> cancel an armed copy, because a
    /// reset is not a board gesture and the pick is still the user's. That asymmetry is inherited
    /// verbatim and is not to be "normalised".
    /// </para>
    /// </summary>
    public sealed class ResetScopeCoordinator
    {
        /// <summary>Title of the confirmation raised before a layer is erased. Not a spec string.</summary>
        public const string ResetLayerTitle = "Reset Layer";

        /// <summary>
        /// The layer-reset prompt. It says what is erased and — because nothing this app does is
        /// written behind the user's back — that the drive is untouched until Save.
        /// </summary>
        public const string ResetLayerConfirmation =
            "Do you want to clear every remap and macro on this layer? Nothing is written to the keyboard until you save.";

        /// <summary>The affirmative, named after what it does rather than "Yes" (mockup 1k). It still answers <c>Yes</c>.</summary>
        public const string ResetLayerConfirmCaption = "Clear layer";

        /// <summary>Title of the confirmation raised before every layer is erased.</summary>
        public const string ResetLayoutTitle = "Reset Layout";

        /// <summary>
        /// What the reset prompts call the profile when there is no number to name — demo mode,
        /// which reads no file and so has no <c>ProfileCaption</c>.
        /// </summary>
        public const string ResetLayoutFallbackScope = "this profile";

        /// <summary>The way out of either reset prompt. It still answers <c>No</c>.</summary>
        public const string ResetDeclineCaption = "Cancel";

        /// <summary>
        /// The whole-profile prompt. Same shape as the layer's, and deliberately different words:
        /// the two scopes share one suppression key, so the sentence is the only thing that tells
        /// the user which of them is about to run.
        /// <para>
        /// <b>It names the profile, and says the others are safe</b> (issue #135). The command has
        /// always cleared the open profile and only that one — it calls
        /// <c>KeyboardLayout.Reset()</c> on the open session's layout and never walks the session
        /// cache — but the old wording ("every layer of this profile", affirmed by a button reading
        /// <c>Clear all layers</c>) was read as <i>every profile</i>, which is the one mistake a
        /// reset prompt must not invite. Since #133 the editor really does hold several profiles in
        /// memory at once, so the reassurance is load-bearing rather than decorative.
        /// </para>
        /// </summary>
        public static string BuildResetLayoutConfirmation(string profileCaption)
        {
            return "Do you want to clear every remap and macro on every layer of "
                   + ResolveResetScope(profileCaption)
                   + "? No other profile is touched, and nothing is written to the keyboard until you save.";
        }

        /// <summary>
        /// The affirmative of the whole-profile prompt, named after what it does (mockup 1k) and —
        /// since issue #135 — after <em>what it does it to</em>. It still answers <c>Yes</c>.
        /// </summary>
        public static string BuildResetLayoutConfirmCaption(string profileCaption)
        {
            return "Clear " + ResolveResetScope(profileCaption);
        }

        /// <summary>
        /// What the two reset strings call the thing being cleared: the profile's own caption when
        /// there is one, and <see cref="ResetLayoutFallbackScope"/> when there is not. One helper so
        /// the sentence and the button can never name different scopes.
        /// </summary>
        private static string ResolveResetScope(string profileCaption)
        {
            return string.IsNullOrWhiteSpace(profileCaption) ? ResetLayoutFallbackScope : profileCaption;
        }

        /// <summary>Drops the selected key's remap (specs/10-apps-and-ui.md, "Reset Key").</summary>
        public IRelayCommand ResetKeyCommand { get; }

        /// <summary>
        /// Resets every key of the shown layer, after the confirmation of
        /// <see cref="NotificationKeys.ResetLayer"/> — which the user can switch off for good on
        /// the Settings tab ("Confirm before resetting a layer", mockup 1j).
        /// </summary>
        public IRelayCommand ResetLayerCommand { get; }

        /// <summary>
        /// Resets every key of every layer, after the same confirmation under the same key; only
        /// the wording differs (see <see cref="BuildResetLayoutConfirmation"/>).
        /// </summary>
        public IRelayCommand ResetLayoutCommand { get; }

        private readonly IResetScopeHost _host;
        private readonly INotificationService _notifications;

        /// <summary>
        /// Builds the three commands over the editor they act on and the service that asks the
        /// question. It is constructed <b>before</b> <see cref="BoardLegendViewModel"/> and the key
        /// inspector, both of which take two of these commands in their own constructors.
        /// </summary>
        public ResetScopeCoordinator(IResetScopeHost host, INotificationService notifications)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

            // The !IsLoading && !IsBusy guard matches CanBeginRemap/CanSave: a save serializes the
            // model on a background thread, so mutating it from here mid-save would race it.
            ResetKeyCommand = new RelayCommand(ResetKey, CanResetKey);
            // Both resets ask first, so both are async; Reset Key does not, because it drops one
            // position's remap and the spec's own reset confirmation covers macros, not remaps.
            ResetLayerCommand = new AsyncRelayCommand(ResetLayerAsync, CanResetLayer);
            ResetLayoutCommand = new AsyncRelayCommand(ResetLayoutAsync, CanResetLayout);
        }

        private bool CanResetKey()
        {
            return _host.SelectedKey is not null && _host.SelectedKey.CanEdit && !_host.IsLoading && !_host.IsBusy;
        }

        private bool CanResetLayer()
        {
            return _host.SelectedLayer is not null && !_host.IsLoading && !_host.IsBusy;
        }

        private bool CanResetLayout()
        {
            return _host.Layout is not null && !_host.IsLoading && !_host.IsBusy;
        }

        /// <summary>
        /// Drops the selected key's remap. This is <see cref="KeyboardKey.ClearRemap"/> and not
        /// <c>Remap(OriginalKey)</c> on purpose: the latter also clears the position's tap-and-hold
        /// and multi-modifier assignment as a side effect (docs/app/keyboard-model.md,
        /// "Watch out"), which is not what "reset this key's remap" means.
        /// </summary>
        private void ResetKey()
        {
            var key = _host.SelectedKey;

            if (key is null || !key.CanEdit)
            {
                return;
            }

            _host.CancelRemap();

            key.Key.ClearRemap();
            key.RefreshFromModel();

            _host.RefreshCounters();
        }

        /// <summary>
        /// Erases the shown layer, after the confirmation of <see cref="NotificationKeys.ResetLayer"/>.
        /// <para>
        /// <b>The question is asked through the notification service and nowhere else.</b> Whether
        /// the user switched it off lives in <c>app_settings.txt</c>, and
        /// <see cref="NotificationService"/> already short-circuits a hidden key to
        /// <see cref="MessageBoxOutcome.ForSuppressed"/>; reading <c>IsHidden</c> here would be a
        /// second policy for one decision. Hence <see cref="MessageBoxRequest.SuppressedResult"/>
        /// is <c>Yes</c>: a suppressed confirmation means "go ahead", not "do nothing".
        /// </para>
        /// </summary>
        private async Task ResetLayerAsync()
        {
            var layer = _host.SelectedLayer;

            if (layer is null)
            {
                return;
            }

            if (!await ConfirmResetAsync(ResetLayerTitle, ResetLayerConfirmation, ResetLayerConfirmCaption).ConfigureAwait(true))
            {
                return;
            }

            // Re-read: the confirmation is modal but the layer selection is not frozen behind it,
            // and erasing a layer the user is no longer looking at would be the worst kind of bug.
            layer = _host.SelectedLayer;

            if (layer is null)
            {
                return;
            }

            _host.CancelRemap();
            _host.DeactivateInspector();

            layer.Layer.Reset();
            layer.RefreshFromModel();

            // KeyboardLayer.Reset clears every rule including the macro slots, so the rail is
            // sitting on macros that no longer exist. RefreshCounters rebuilds the library snapshot
            // and pushes it.
            _host.RefreshCounters();
        }

        /// <summary>
        /// Erases every layer, after a confirmation that <b>shares</b> the layer reset's
        /// suppression key.
        /// <para>
        /// One preference, not two: the catalog models a single reset confirmation
        /// ("Confirm before resetting a layer", mockup 1j), and a user who switched it off has
        /// answered the question "should a reset stop and ask me?" for both scopes. Leaving the
        /// wider scope unsuppressible would put two policies behind one checkbox and leave a prompt
        /// the settings screen cannot re-enable. What differs is the wording — the sentence names
        /// the profile — and the stakes are bounded the same way: a reset changes memory only, and
        /// nothing reaches the drive until Save.
        /// </para>
        /// </summary>
        private async Task ResetLayoutAsync()
        {
            if (_host.Layout is null)
            {
                return;
            }

            if (!await ConfirmResetAsync(
                    ResetLayoutTitle,
                    BuildResetLayoutConfirmation(_host.ProfileCaption),
                    BuildResetLayoutConfirmCaption(_host.ProfileCaption)).ConfigureAwait(true))
            {
                return;
            }

            // Re-read after the box: a load or an import may have replaced the model behind it.
            var layout = _host.Layout;

            if (layout is null)
            {
                return;
            }

            _host.CancelRemap();
            _host.DeactivateInspector();

            layout.Reset();

            foreach (var layer in _host.Layers)
            {
                layer.RefreshFromModel();
            }

            _host.RefreshCounters();
        }

        /// <summary>
        /// Puts one of the two reset confirmations on screen and reports whether the erase may go
        /// ahead. False for every other answer, including a box that could not be shown at all —
        /// a confirmation that failed must not erase anything, and must not bring the app down
        /// either (the same rule the lighting tab's Reset All follows).
        /// </summary>
        private async Task<bool> ConfirmResetAsync(string title, string message, string confirmCaption)
        {
            MessageBoxOutcome outcome;

            try
            {
                outcome = await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = title,
                    Message = message,
                    Icon = MessageBoxIcon.Confirmation,
                    Buttons = MessageBoxButtons.YesNo,
                    YesCaption = confirmCaption,
                    NoCaption = ResetDeclineCaption,
                    SuppressionKey = NotificationKeys.ResetLayer,
                    SuppressedResult = MessageBoxResult.Yes
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                return false;
            }

            return outcome.Result == MessageBoxResult.Yes;
        }
    }

    /// <summary>
    /// What <see cref="ResetScopeCoordinator"/> needs from the editor it resets: the four pieces of
    /// state the three scopes are decided from, the profile's caption for the prompt, and the three
    /// calls an erase makes on the way out.
    /// <para>
    /// A narrow interface rather than a bundle of delegates because there are ten members, and a
    /// <see cref="KeyboardEditorViewModel"/> back-reference rather than either would let the
    /// coordinator reach the whole god class it was split out of. It is implemented by the editor,
    /// explicitly where the member is not already part of that class's public surface — a split
    /// must not make a private method public.
    /// </para>
    /// </summary>
    public interface IResetScopeHost
    {
        /// <summary>The loaded model, or null while loading and after a load that failed outright.</summary>
        KeyboardLayout? Layout { get; }

        /// <summary>The device's layers, in model order — what a whole-profile reset re-reads.</summary>
        IReadOnlyList<KeyboardLayerViewModel> Layers { get; }

        /// <summary>The layer the picture is showing; the layer reset's scope.</summary>
        KeyboardLayerViewModel? SelectedLayer { get; }

        /// <summary>The key every key-scoped action applies to; the key reset's scope.</summary>
        KeyboardKeyViewModel? SelectedKey { get; }

        /// <summary>"Profile n" for a loaded profile, empty in demo mode — what the wider prompt names.</summary>
        string ProfileCaption { get; }

        /// <summary>Whether the profile is still being read.</summary>
        bool IsLoading { get; }

        /// <summary>Whether a save is in flight.</summary>
        bool IsBusy { get; }

        /// <summary>Leaves listening state; an erase must not land under a key that is capturing.</summary>
        void CancelRemap();

        /// <summary>Stands whatever the rail has armed down, without closing it.</summary>
        void DeactivateInspector();

        /// <summary>
        /// The editor's one post-write funnel (docs/app/keyboard-editor.md, invariant 16). Every
        /// erase here ends in it, as a call rather than an inlined copy.
        /// </summary>
        void RefreshCounters();
    }
}
