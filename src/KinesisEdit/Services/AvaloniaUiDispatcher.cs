using Avalonia.Threading;

namespace KinesisEdit.Services
{
    /// <summary>
    /// <see cref="IUiDispatcher"/> over Avalonia's UI-thread dispatcher. The only Avalonia
    /// dependency in the services layer; every other service stays toolkit-free so it can be
    /// unit-tested without an Avalonia runtime.
    /// </summary>
    public sealed class AvaloniaUiDispatcher : IUiDispatcher
    {
        /// <summary>Posts <paramref name="action"/> to the Avalonia UI thread queue.</summary>
        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            Dispatcher.UIThread.Post(action);
        }
    }
}
