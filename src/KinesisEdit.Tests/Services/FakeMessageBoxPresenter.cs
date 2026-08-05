using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>Hand-rolled <see cref="IMessageBoxPresenter"/> recording every presented request.</summary>
    internal sealed class FakeMessageBoxPresenter : IMessageBoxPresenter
    {
        public MessageBoxOutcome OutcomeToReturn { get; set; } = new MessageBoxOutcome
        {
            Result = MessageBoxResult.Ok
        };

        public List<MessageBoxRequest> Requests { get; } = [];

        public Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request)
        {
            Requests.Add(request);

            return Task.FromResult(OutcomeToReturn);
        }
    }
}
