using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The three reset scopes on their own — <b>no editor, no profile session and no drive</b>.
    /// That is the point of the type existing: every claim about what a reset erases, what it asks
    /// first and what the prompt names used to have to be made through a loaded
    /// <c>KeyboardEditorViewModel</c>.
    /// <para>
    /// The editor is a fake host that records the three calls an erase makes, so this suite watches
    /// the coordinator's own behaviour rather than a board it does not own.
    /// </para>
    /// </summary>
    public class ResetScopeCoordinatorTests
    {
        [AvaloniaFact]
        public void ResetKey_ClearsTheSelectedKeysRemap_AndAsksNothing()
        {
            // Reset Key is the one scope with no confirmation: it drops one position's remap, and
            // spec 10's own reset confirmation covers macros rather than remaps.
            var scene = new Scene();
            var key = scene.SelectFirstKey();

            key.Key.Remap(TestLayouts.Gen1Key("esc"));
            key.RefreshFromModel();

            scene.Coordinator.ResetKeyCommand.Execute(null);

            Assert.False(key.Key.IsModified);
            Assert.Empty(scene.Notifications.MessageBoxes);
            Assert.Equal(1, scene.Host.CancelRemapCalls);
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaFact]
        public void ResetKey_OnAPositionThatCannotBeEdited_WritesNothing()
        {
            var scene = Scene.Locked();

            scene.Host.SelectedKey = scene.Key(1);

            Assert.False(scene.Coordinator.ResetKeyCommand.CanExecute(null));

            scene.Coordinator.ResetKeyCommand.Execute(null);

            Assert.Equal(0, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaTheory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void EveryScope_IsRefusedWhileTheEditorIsReadingOrWriting(bool isLoading, bool isBusy)
        {
            // The same !IsLoading && !IsBusy guard CanBeginRemap and CanSave carry: a save
            // serializes the model on a background thread, so erasing it here would race the write.
            var scene = new Scene();

            scene.SelectFirstKey();

            Assert.True(scene.Coordinator.ResetKeyCommand.CanExecute(null));
            Assert.True(scene.Coordinator.ResetLayerCommand.CanExecute(null));
            Assert.True(scene.Coordinator.ResetLayoutCommand.CanExecute(null));

            scene.Host.IsLoading = isLoading;
            scene.Host.IsBusy = isBusy;

            Assert.False(scene.Coordinator.ResetKeyCommand.CanExecute(null));
            Assert.False(scene.Coordinator.ResetLayerCommand.CanExecute(null));
            Assert.False(scene.Coordinator.ResetLayoutCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void EveryScope_IsRefusedWithNothingToResetIt()
        {
            var scene = new Scene { Host = { Layout = null, SelectedLayer = null, SelectedKey = null } };

            Assert.False(scene.Coordinator.ResetKeyCommand.CanExecute(null));
            Assert.False(scene.Coordinator.ResetLayerCommand.CanExecute(null));
            Assert.False(scene.Coordinator.ResetLayoutCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public async Task ResetLayer_AsksBeforeItErasesAnything_UnderTheSharedSuppressionKey()
        {
            var scene = new Scene();

            await scene.RunAsync(scene.Coordinator.ResetLayerCommand);

            var request = Assert.Single(scene.Notifications.MessageBoxes);

            Assert.Equal(ResetScopeCoordinator.ResetLayerTitle, request.Title);
            Assert.Equal(ResetScopeCoordinator.ResetLayerConfirmation, request.Message);
            Assert.Equal(MessageBoxButtons.YesNo, request.Buttons);
            Assert.Equal(MessageBoxIcon.Confirmation, request.Icon);
            Assert.Equal(ResetScopeCoordinator.ResetLayerConfirmCaption, request.YesCaption);
            Assert.Equal(ResetScopeCoordinator.ResetDeclineCaption, request.NoCaption);

            // One preference answers "should a reset stop and ask me?" for both scopes, and a
            // suppressed confirmation means "go ahead" rather than "do nothing".
            Assert.Equal(NotificationKeys.ResetLayer, request.SuppressionKey);
            Assert.Equal(MessageBoxResult.Yes, request.SuppressedResult);
        }

        [AvaloniaFact]
        public async Task ResetLayer_WhenConfirmed_ErasesTheShownLayerAndNothingElse()
        {
            var scene = new Scene();

            scene.Layers[0].Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));
            scene.Layers[1].Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));

            scene.Confirm();

            await scene.RunAsync(scene.Coordinator.ResetLayerCommand);

            Assert.False(scene.Layers[0].Layer.Keys[0].IsModified);
            Assert.True(scene.Layers[1].Layer.Keys[0].IsModified);
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaFact]
        public async Task ResetLayout_WhenConfirmed_ErasesEveryLayerOfTheOpenProfile()
        {
            var scene = new Scene();

            foreach (var layer in scene.Layers)
            {
                layer.Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));
            }

            scene.Confirm();

            await scene.RunAsync(scene.Coordinator.ResetLayoutCommand);

            Assert.Equal(0, scene.Layout.ModifiedKeyCount);
            Assert.Equal(1, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AWiderReset_ThatWasDeclinedOrCouldNotBeAsked_ErasesNothing(bool boxThrows)
        {
            // A confirmation that failed must not erase anything, and must not bring the app down
            // either — the same rule the lighting tab's Reset All follows.
            var scene = new Scene();

            scene.Layers[0].Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));

            if (boxThrows)
            {
                scene.Notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no window");
            }
            else
            {
                scene.Notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };
            }

            await scene.RunAsync(scene.Coordinator.ResetLayerCommand);
            await scene.RunAsync(scene.Coordinator.ResetLayoutCommand);

            Assert.True(scene.Layers[0].Layer.Keys[0].IsModified);
            Assert.Equal(0, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaFact]
        public async Task AWiderReset_StandsTheListeningKeyAndTheRailDown_AndOnlyThose()
        {
            // Two of the three stand-downs, deliberately: an armed copy is not ended by a reset,
            // which is why this coordinator is not handed a way to end one.
            var scene = new Scene();

            scene.Confirm();

            await scene.RunAsync(scene.Coordinator.ResetLayerCommand);

            Assert.Equal(1, scene.Host.CancelRemapCalls);
            Assert.Equal(1, scene.Host.DeactivateInspectorCalls);

            await scene.RunAsync(scene.Coordinator.ResetLayoutCommand);

            Assert.Equal(2, scene.Host.CancelRemapCalls);
            Assert.Equal(2, scene.Host.DeactivateInspectorCalls);
        }

        [AvaloniaFact]
        public async Task TheWholeProfilePrompt_AndItsAffirmative_NameTheSameScope()
        {
            // Invariant 34: a destructive prompt names its scope, and the sentence and the button
            // are built from one ResolveResetScope so the two can never disagree. Issue #135 is
            // what a caption reading `Clear all layers` beside "this profile" cost.
            var scene = new Scene();

            scene.Host.ProfileCaption = "Profile 3";

            await scene.RunAsync(scene.Coordinator.ResetLayoutCommand);

            var request = Assert.Single(scene.Notifications.MessageBoxes);

            Assert.Equal(ResetScopeCoordinator.ResetLayoutTitle, request.Title);
            Assert.Equal(ResetScopeCoordinator.BuildResetLayoutConfirmation("Profile 3"), request.Message);
            Assert.Equal("Clear Profile 3", request.YesCaption);
            Assert.Contains("Profile 3", request.Message, StringComparison.Ordinal);
            Assert.Contains("No other profile is touched", request.Message, StringComparison.Ordinal);

            // And the layer's wording stays a different sentence, because the two share one
            // suppression key and only the sentence says which scope is about to run.
            Assert.NotEqual(ResetScopeCoordinator.ResetLayerConfirmation, request.Message);
        }

        [AvaloniaFact]
        public async Task WithNoProfileToName_BothStringsFallBackToTheSameWords()
        {
            // Demo mode reads no profile file, so there is no number to name.
            var scene = new Scene();

            scene.Host.ProfileCaption = string.Empty;

            await scene.RunAsync(scene.Coordinator.ResetLayoutCommand);

            var request = Assert.Single(scene.Notifications.MessageBoxes);

            Assert.Contains(ResetScopeCoordinator.ResetLayoutFallbackScope, request.Message, StringComparison.Ordinal);
            Assert.Equal("Clear " + ResetScopeCoordinator.ResetLayoutFallbackScope, request.YesCaption);
            Assert.Equal(
                ResetScopeCoordinator.BuildResetLayoutConfirmCaption(string.Empty),
                request.YesCaption);
        }

        [AvaloniaFact]
        public async Task AWiderReset_RereadsWhatItIsAbout_AfterTheBoxCameBack()
        {
            // The confirmation is modal but the selection is not frozen behind it, and erasing a
            // layer the user is no longer looking at would be the worst kind of bug.
            var scene = new Scene();

            scene.Confirm();

            scene.Layers[0].Layer.Keys[0].Remap(TestLayouts.Gen1Key("esc"));

            scene.Notifications.MessageBoxShowing = _ => scene.Host.SelectedLayer = null;

            await scene.RunAsync(scene.Coordinator.ResetLayerCommand);

            Assert.True(scene.Layers[0].Layer.Keys[0].IsModified);
            Assert.Equal(0, scene.Host.RefreshCountersCalls);
        }

        [AvaloniaFact]
        public void Constructor_RefusesAMissingCollaborator()
        {
            var host = new FakeResetScopeHost();
            var notifications = new FakeNotificationService();

            Assert.Throws<ArgumentNullException>(() => new ResetScopeCoordinator(null!, notifications));
            Assert.Throws<ArgumentNullException>(() => new ResetScopeCoordinator(host, null!));
        }

        /// <summary>A coordinator over a real RGB model and a fake editor, with nothing else.</summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public IReadOnlyList<KeyboardLayerViewModel> Layers { get; }

            public FakeResetScopeHost Host { get; init; }

            public FakeNotificationService Notifications { get; }

            public ResetScopeCoordinator Coordinator { get; }

            public Scene()
                : this(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb), VisualCatalog.FreestyleEdgeRgb)
            {
            }

            private Scene(KeyboardLayout layout, KeyboardVisual visual)
            {
                Layout = layout;
                Layers = KeyboardLayerViewModel.BuildAll(layout, visual, lighting: null);

                Host = new FakeResetScopeHost
                {
                    Layout = layout,
                    Layers = Layers,
                    SelectedLayer = Layers[0]
                };

                Notifications = new FakeNotificationService();
                Coordinator = new ResetScopeCoordinator(Host, Notifications);
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

            public void Confirm()
            {
                Notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };
            }

            /// <summary>Runs one of the two async resets to completion.</summary>
            public Task RunAsync(IRelayCommand command)
            {
                return ((IAsyncRelayCommand)command).ExecuteAsync(null);
            }
        }

        /// <summary>
        /// The editor as <see cref="ResetScopeCoordinator"/> sees it: the state the three scopes are
        /// decided from, and a tally of the three calls an erase makes.
        /// </summary>
        private sealed class FakeResetScopeHost : IResetScopeHost
        {
            public KeyboardLayout? Layout { get; set; }

            public IReadOnlyList<KeyboardLayerViewModel> Layers { get; set; } = [];

            public KeyboardLayerViewModel? SelectedLayer { get; set; }

            public KeyboardKeyViewModel? SelectedKey { get; set; }

            public string ProfileCaption { get; set; } = string.Empty;

            public bool IsLoading { get; set; }

            public bool IsBusy { get; set; }

            public int CancelRemapCalls { get; private set; }

            public int DeactivateInspectorCalls { get; private set; }

            public int RefreshCountersCalls { get; private set; }

            public void CancelRemap()
            {
                CancelRemapCalls++;
            }

            public void DeactivateInspector()
            {
                DeactivateInspectorCalls++;
            }

            public void RefreshCounters()
            {
                RefreshCountersCalls++;
            }
        }
    }
}
