using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// <c>Discard changes</c> — <b>throw this profile's unsaved edits away and go back to what was
    /// loaded</b> (issue #133). Split out of <see cref="KeyboardEditorViewModel"/>'s main file for
    /// the reason <c>…Profiles.cs</c>, <c>…Legend.cs</c>, <c>…Inspector.cs</c> and
    /// <c>…MacroNames.cs</c> were: that file is the largest in the app and
    /// docs/guides/Coding Conventions.md forbids growing it into a god class. Everything here is the
    /// same class and runs on the same rules.
    ///
    /// <para><b>Two scopes, and the open tab picks between them.</b> The user's own words were
    /// *"only the open profile and only the page it is open on … I don't want it to discard lighting
    /// if I'm on the Layout page and vice versa"*, so the Keys tab reverts the
    /// <c>layout&lt;n&gt;.txt</c> half — macros included, a macro <em>is</em> layout content — and
    /// the Lighting tab reverts the <c>led&lt;n&gt;.txt</c> half. The Settings tab gets no discard
    /// at all, because the settings file is outside the session's dirty model altogether
    /// (docs/app/keyboard-editor.md, "Settings are outside the dirty model") and there is nothing
    /// here that could revert it.</para>
    ///
    /// <para><b>It is not <c>Reset Layout</c>, and the two must never be merged.</b> A reset clears
    /// the profile to factory defaults — an <em>edit</em>, and one Save would then write. A discard
    /// restores the bytes that were read off the drive, which is the opposite direction. They sit
    /// side by side in the action row on purpose.</para>
    ///
    /// <para><b>It goes back to what is on the v-Drive</b>, which is what "discard my changes"
    /// means. Core's baseline is the load for a profile nobody has saved and the <em>last save</em>
    /// for one that has — the same baseline <c>IsDirty</c> compares against, moved by each
    /// successful write (docs/app/profiles.md) — so a discard can never take back work the user
    /// already committed, and a profile discarded straight after a save is clean rather than newly
    /// amber.</para>
    /// </summary>
    public sealed partial class KeyboardEditorViewModel
    {
        /// <summary>
        /// Reverts the open profile's <b>open page</b> to what was loaded, after a confirmation.
        /// Async because it asks first; the question has <b>no suppression key</b> — the
        /// <c>*_msg</c> keys are spec 08's own <c>app_settings.txt</c> keys and this app does not
        /// invent one, and reusing <see cref="NotificationKeys.ResetLayer"/> would put a second
        /// policy behind a checkbox that says "reset".
        /// </summary>
        public IAsyncRelayCommand DiscardChangesCommand { get; }

        /// <summary>
        /// Whether there is a session, a page with something to revert, and nothing in flight. The
        /// three "busy" terms are the ones every editing command carries; <b>demo mode is
        /// deliberately absent</b> — a discard writes nowhere, and the demo session is a real one
        /// whose edits a user is as entitled to throw away as anyone else's.
        /// </summary>
        private bool CanDiscardChanges()
        {
            return _session is not null
                   && DiscardScopeOf(SelectedTab) != DiscardScope.None
                   && !IsLoading
                   && !IsBusy
                   && ActiveOverlay is null;
        }

        /// <summary>Which half of the session the open section owns. See the type's remarks.</summary>
        private static DiscardScope DiscardScopeOf(EditorTab tab)
        {
            return tab switch
            {
                EditorTab.Keys => DiscardScope.Layout,
                EditorTab.Lighting => DiscardScope.Lighting,
                _ => DiscardScope.None
            };
        }

        private async Task DiscardChangesAsync()
        {
            if (!CanDiscardChanges())
            {
                return;
            }

            var scope = DiscardScopeOf(SelectedTab);

            if (!await ConfirmDiscardAsync(scope).ConfigureAwait(true))
            {
                return;
            }

            // Re-read everything the answer was about: the box is modal but the editor is not frozen
            // behind it, and reverting the page the user has since left would be the worst kind of
            // bug. Same re-read ResetLayerAsync does after its own confirmation.
            if (_isDisposed || _session is not { } session || DiscardScopeOf(SelectedTab) != scope)
            {
                return;
            }

            // The stand-downs a profile switch performs, for the same reasons: a listening key and
            // an armed record button both own the next keystroke, and an armed copy is finished with
            // a click on a board that is about to be rebuilt under it.
            CancelRemap();
            CancelCopyKey();
            DeactivateInspector();

            if (scope == DiscardScope.Layout)
            {
                DiscardLayout(session);
            }
            else
            {
                DiscardLighting(session);
            }
        }

        /// <summary>
        /// Rebuilds the profile's key/macro model from the baseline — the lines read at load, or
        /// the ones the last successful save wrote.
        /// <para>
        /// It goes through <c>Apply</c> rather than the per-layer <c>RefreshFromModel()</c> that
        /// <c>ResetLayoutAsync</c> gets away with, and the difference is which object moved:
        /// <c>KeyboardLayout.Reset()</c> mutates the model in place, while
        /// <c>ProfileSession.RevertLayout()</c> replaces it with a <b>brand-new</b> one — exactly as
        /// a load or an import does — so every layer and cap view model over the old one is stale.
        /// <c>Apply</c> is the one place that rebuilds all of them, re-stamps the stored macro names
        /// (which is right here: the discarded state includes the names on the drive), re-parents
        /// the lighting panel onto the same led model, and ends in <c>RefreshCounters()</c>, which
        /// is invariant 16.
        /// </para>
        /// </summary>
        private void DiscardLayout(IProfileSession session)
        {
            // Before Apply, whose funnel re-reads the dirty flag. A rename is unsaved work about the
            // very macros this revert is throwing away, and it never reached app_settings.txt, so it
            // goes with them — otherwise the profile would come back from the discard still amber.
            ClearUnsavedMacroNames(session.ProfileNumber);

            session.RevertLayout();

            Apply(new LoadOutcome { Session = session, Layout = session.Layout });
        }

        /// <summary>
        /// Rebuilds the profile's led model from the baseline and hands it to the
        /// Lighting tab. The layout did not move, so the layer view models are the same instances
        /// and only their caps' colour repaints — which is why this does not go through
        /// <c>Apply</c>: rebuilding the board would throw away a selection the revert has no
        /// business touching.
        /// </summary>
        private void DiscardLighting(IProfileSession session)
        {
            session.RevertLighting();

            // The same call Apply makes after a load. The model was replaced rather than mutated,
            // so re-parenting is the whole of it.
            Lighting.Attach(session.Lighting, Layers);

            // Invariant 16. Attach may already have raised ModelChanged while normalizing a
            // direction; the funnel is idempotent, and a lighting revert has to reach the amber Save
            // whether it did or not.
            RefreshCounters();
        }

        /// <summary>
        /// Puts the scope's confirmation on screen and reports whether the revert may go ahead.
        /// False for every other answer, including a box that could not be shown at all — a
        /// confirmation that failed must not destroy anything, and must not bring the app down
        /// either (the rule both resets already follow).
        /// <para>
        /// <b>No <c>SuppressionKey</c>, and no <c>SuppressedResult</c>.</b> Every suppression key in
        /// this app is one of spec 08 §3's <c>*_msg</c> keys in the manufacturer's own
        /// <c>app_settings.txt</c>; this feature is not entitled to invent a tenth, and borrowing
        /// <see cref="NotificationKeys.ResetLayer"/> would mean a user who switched off "confirm
        /// before resetting a layer" had also silently agreed to unprompted discards.
        /// </para>
        /// </summary>
        private async Task<bool> ConfirmDiscardAsync(DiscardScope scope)
        {
            MessageBoxOutcome outcome;

            try
            {
                outcome = await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = DiscardChangesTitle,
                    Message = scope == DiscardScope.Lighting
                        ? DiscardLightingConfirmation
                        : DiscardLayoutConfirmation,
                    Icon = MessageBoxIcon.Confirmation,
                    Buttons = MessageBoxButtons.YesNo,
                    YesCaption = DiscardConfirmCaption,
                    NoCaption = DiscardDeclineCaption,
                    DestructiveResult = MessageBoxResult.Yes
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                return false;
            }

            return outcome.Result == MessageBoxResult.Yes;
        }

        /// <summary>Which half of the open profile a discard is about.</summary>
        private enum DiscardScope
        {
            /// <summary>The open section owns nothing the session can revert (Settings).</summary>
            None = 0,

            /// <summary><c>layout&lt;n&gt;.txt</c> — keys, remaps, macros (the Layout tab).</summary>
            Layout = 1,

            /// <summary><c>led&lt;n&gt;.txt</c> — the lighting model (Lighting).</summary>
            Lighting = 2
        }
    }
}
