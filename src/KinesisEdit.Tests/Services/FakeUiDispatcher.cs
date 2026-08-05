using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IUiDispatcher"/>: runs callbacks inline by default, or queues them
    /// when <see cref="IsDeferred"/> is set, so tests can prove that an event really travels
    /// through the dispatcher instead of being raised on the polling thread.
    /// </summary>
    internal sealed class FakeUiDispatcher : IUiDispatcher
    {
        public bool IsDeferred { get; set; }

        public int PostCount { get; private set; }

        public int PendingCount => _pending.Count;

        private readonly List<Action> _pending = [];

        public void Post(Action action)
        {
            PostCount++;

            if (IsDeferred)
            {
                _pending.Add(action);
                return;
            }

            action();
        }

        public void DrainPending()
        {
            var pending = _pending.ToArray();
            _pending.Clear();

            foreach (var action in pending)
            {
                action();
            }
        }
    }
}
