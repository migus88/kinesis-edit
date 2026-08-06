using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// A stand-in for the shell, for tests that only need an editor to <em>have</em> chrome to bind
    /// through — the status text, the severity, and a Home command whose invocations are counted.
    /// <para>
    /// The real <see cref="MainWindowViewModel"/> is used wherever the wiring itself is the subject
    /// (<c>MainWindowViewModelTests</c>, and the "exactly one 46 px bar" frame check); this exists
    /// so a view-model test does not have to build a detection loop to render a chip.
    /// </para>
    /// </summary>
    internal sealed class FakeShellChrome : ObservableObject, IShellChrome
    {
        /// <inheritdoc />
        public IAsyncRelayCommand HomeCommand { get; }

        /// <inheritdoc />
        public string StatusIndicatorText
        {
            get => _statusIndicatorText;
            set => SetProperty(ref _statusIndicatorText, value);
        }

        /// <inheritdoc />
        public StatusSeverity StatusIndicatorSeverity
        {
            get => _statusIndicatorSeverity;
            set => SetProperty(ref _statusIndicatorSeverity, value);
        }

        /// <summary>How often <see cref="HomeCommand"/> ran.</summary>
        public int HomeCallCount { get; private set; }

        private string _statusIndicatorText = MainWindowViewModel.VDriveOkIndicator;
        private StatusSeverity _statusIndicatorSeverity = StatusSeverity.Ok;

        public FakeShellChrome()
        {
            HomeCommand = new AsyncRelayCommand(() =>
            {
                HomeCallCount++;

                return Task.CompletedTask;
            });
        }
    }
}
