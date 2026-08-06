using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The presenter draws nothing: it is the lazy forwarder onto the overlay that does, built
    /// before the shell window exists. What matters here is the deferral and the null-host branch.
    /// </summary>
    public class MessageBoxPresenterTests
    {
        [Fact]
        public void Constructor_WithoutAHostAccessor_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MessageBoxPresenter(null!));
        }

        [Fact]
        public async Task PresentAsync_WithoutARequest_ThrowsBeforeReachingTheHost()
        {
            var presenter = new MessageBoxPresenter(() => null);

            await Assert.ThrowsAsync<ArgumentNullException>(() => presenter.PresentAsync(null!));
        }

        [Fact]
        public async Task PresentAsync_WithNoHostRegistered_AnswersRatherThanHanging()
        {
            // A Task that never completed would deadlock the caller. The unpresented box reports
            // what Escape would have: Cancel where there is a Cancel button.
            var presenter = new MessageBoxPresenter(() => null);

            var outcome = await presenter.PresentAsync(new MessageBoxRequest
            {
                Title = "Save",
                Message = "Save changes?",
                Buttons = MessageBoxButtons.YesNoCancel
            });

            Assert.Equal(MessageBoxResult.Cancel, outcome.Result);
        }

        [Fact]
        public async Task PresentAsync_WithNoHostAndNoCancelButton_ReportsNone()
        {
            var presenter = new MessageBoxPresenter(() => null);

            var outcome = await presenter.PresentAsync(new MessageBoxRequest
            {
                Title = "Saved",
                Message = "Profile 1 Saved",
                Buttons = MessageBoxButtons.Ok
            });

            Assert.Equal(MessageBoxResult.None, outcome.Result);
        }

        [Fact]
        public async Task PresentAsync_ResolvesTheHostPerCall_NotAtConstruction()
        {
            // This is the whole point of the callback: App builds the presenter before MainWindow,
            // so a host captured at construction would be null for the app's whole life.
            var host = new RecordingHost();
            IMessageBoxPresenter? current = null;
            var presenter = new MessageBoxPresenter(() => current);
            var request = new MessageBoxRequest
            {
                Title = "Save",
                Message = "Save changes?",
                Buttons = MessageBoxButtons.YesNo
            };

            var beforeHost = await presenter.PresentAsync(request);

            Assert.Equal(MessageBoxResult.None, beforeHost.Result);
            Assert.Empty(host.Requests);

            current = host;

            var afterHost = await presenter.PresentAsync(request);

            Assert.Equal(MessageBoxResult.Yes, afterHost.Result);
            Assert.Single(host.Requests);
        }

        [Fact]
        public async Task PresentAsync_WhileCaptureIsLive_SuspendsItForAsLongAsTheBoxIsUp()
        {
            // The box shares the shell's TopLevel now. A live capture previews that TopLevel's key
            // events in the tunnel phase and swallows every key it resolves — the dialog's own
            // Enter and Escape included — and hands them to whatever is recording, so an Escape
            // aimed at the box would be assigned as a remap instead of closing it.
            var capture = new FakeKeystrokeCaptureService();
            var host = new BlockingHost();
            var presenter = new MessageBoxPresenter(() => host, () => capture);

            capture.Start();

            var answer = presenter.PresentAsync(CreateRequest());

            Assert.True(capture.IsSuspended);
            Assert.Equal(1, capture.SuspendCount);
            Assert.Equal(0, capture.ResumeCount);

            host.Answer(MessageBoxResult.Yes);

            await answer;

            Assert.False(capture.IsSuspended);
            Assert.Equal(1, capture.ResumeCount);
            Assert.True(capture.IsCapturing, "The box stopped capture rather than suspending it.");
        }

        [Fact]
        public async Task PresentAsync_WithASecondBoxBehindTheFirst_StaysSuspendedUntilBothAreAnswered()
        {
            // The host queues a box that arrives while one is up and answers the first one first,
            // so a resume tied to a single box would hand the keyboard back with the second card
            // still on screen.
            var capture = new FakeKeystrokeCaptureService();
            var host = new BlockingHost();
            var presenter = new MessageBoxPresenter(() => host, () => capture);

            capture.Start();

            var first = presenter.PresentAsync(CreateRequest());
            var second = presenter.PresentAsync(CreateRequest());

            host.Answer(MessageBoxResult.Yes);

            await first;

            Assert.True(capture.IsSuspended, "Capture came back while a queued box was still up.");
            Assert.Equal(0, capture.ResumeCount);

            host.Answer(MessageBoxResult.No);

            await second;

            Assert.False(capture.IsSuspended);
            Assert.Equal(1, capture.SuspendCount);
            Assert.Equal(1, capture.ResumeCount);
        }

        [Fact]
        public async Task PresentAsync_WhenSomethingElseAlreadyHoldsTheSuspension_LeavesItAlone()
        {
            // A text-entry feature panel suspends capture for its whole lifetime and raises boxes
            // of its own (EditorOverlayHost, ExportOverlayViewModel). Only a suspension this
            // presenter took is one it may release.
            var capture = new FakeKeystrokeCaptureService();
            var host = new BlockingHost();
            var presenter = new MessageBoxPresenter(() => host, () => capture);

            capture.Start();
            capture.Suspend();

            var answer = presenter.PresentAsync(CreateRequest());

            host.Answer(MessageBoxResult.Yes);

            await answer;

            Assert.True(capture.IsSuspended, "The box resumed a suspension it did not take.");
            Assert.Equal(1, capture.SuspendCount);
            Assert.Equal(0, capture.ResumeCount);
        }

        [Fact]
        public async Task PresentAsync_WithNoHost_TouchesCaptureAtAll()
        {
            // Nothing reached the screen, so no key can be aimed at it.
            var capture = new FakeKeystrokeCaptureService();
            var presenter = new MessageBoxPresenter(() => null, () => capture);

            capture.Start();

            await presenter.PresentAsync(CreateRequest());

            Assert.Equal(0, capture.SuspendCount);
            Assert.Equal(0, capture.ResumeCount);
        }

        [Fact]
        public async Task PresentAsync_WhenTheHostThrows_StillGivesTheKeyboardBack()
        {
            var capture = new FakeKeystrokeCaptureService();
            var presenter = new MessageBoxPresenter(() => new ThrowingHost(), () => capture);

            capture.Start();

            await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.PresentAsync(CreateRequest()));

            Assert.False(capture.IsSuspended);
            Assert.Equal(1, capture.ResumeCount);
        }

        private static MessageBoxRequest CreateRequest()
        {
            return new MessageBoxRequest
            {
                Title = "Save",
                Message = "Save changes?",
                Buttons = MessageBoxButtons.YesNo
            };
        }

        private sealed class RecordingHost : IMessageBoxPresenter
        {
            public List<MessageBoxRequest> Requests { get; } = [];

            public Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request)
            {
                Requests.Add(request);

                return Task.FromResult(new MessageBoxOutcome
                {
                    Result = MessageBoxResult.Yes
                });
            }
        }

        /// <summary>
        /// A host that holds every box open until the test answers it, in arrival order — the
        /// queueing <c>NotificationOverlay</c> does, without a UI.
        /// </summary>
        private sealed class BlockingHost : IMessageBoxPresenter
        {
            private readonly Queue<TaskCompletionSource<MessageBoxOutcome>> _pending = new();

            public Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request)
            {
                var completion = new TaskCompletionSource<MessageBoxOutcome>();

                _pending.Enqueue(completion);

                return completion.Task;
            }

            /// <summary>Answers the oldest box still up.</summary>
            public void Answer(MessageBoxResult result)
            {
                _pending.Dequeue().SetResult(new MessageBoxOutcome { Result = result });
            }
        }

        private sealed class ThrowingHost : IMessageBoxPresenter
        {
            public Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request)
            {
                throw new InvalidOperationException("The window is already gone.");
            }
        }
    }
}
