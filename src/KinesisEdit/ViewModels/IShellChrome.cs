using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The part of the shell an editor that draws <b>its own</b> chrome has to reach: the way back
    /// to the dashboard, and the v-Drive status the shell's own bar used to show
    /// (docs/design/handoff.md § "Editor shell" — one 46 px bar while editing, carrying Home, the
    /// device name, the drive path, Save <em>and</em> the status pill).
    /// <para>
    /// <b>Why an interface and not a <c>$parent[MainWindow]</c> binding.</b> Every headless suite
    /// renders an editor view standalone in a plain host window
    /// (<c>KinesisEdit.Tests/Headless/ThemedHost</c>), so a visual-tree lookup for the window's
    /// <c>DataContext</c> would resolve to nothing: the Home button and the status pill would
    /// render blank in the tests <em>and</em> in the frames used to verify the screen. A view must
    /// get its data from its own <c>DataContext</c>, so the shell is handed to the editor as data —
    /// <see cref="DeviceEditorViewModel.Shell"/> — and the editor binds through it.
    /// </para>
    /// <para>
    /// It is deliberately three members wide. The editor is not being given the shell; it is being
    /// given the two things the bar it now draws used to get for free.
    /// </para>
    /// </summary>
    public interface IShellChrome : INotifyPropertyChanged
    {
        /// <summary>Closes the editor and returns to the dashboard (never ejects — 03 §3.5).</summary>
        IAsyncRelayCommand HomeCommand { get; }

        /// <summary>The status chip's caption: <c>v-Drive OK</c>, <c>v-Drive Error</c> or <c>Demo Mode</c>.</summary>
        string StatusIndicatorText { get; }

        /// <summary>The severity the chip's four faces are keyed on.</summary>
        StatusSeverity StatusIndicatorSeverity { get; }
    }
}
