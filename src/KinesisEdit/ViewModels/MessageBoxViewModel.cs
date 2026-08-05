using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The app-wide message box of specs/11-feature-dialogs.md §11.9: title, message, icon, the
    /// standard Yes/No/OK/Cancel buttons plus caller-supplied custom buttons, and the
    /// 'Hide this notification?' checkbox — which appears only when the request carries a
    /// suppression key. The answer leaves as a structured <see cref="MessageBoxOutcome"/>; the
    /// legacy "+100 modal result" encoding is not reproduced.
    /// </summary>
    public sealed class MessageBoxViewModel : ViewModelBase
    {
        /// <summary>Caption of the suppression checkbox (specs/11-feature-dialogs.md §11.9).</summary>
        public const string SuppressionCaption = "Hide this notification?";

        /// <summary>The request being shown.</summary>
        public MessageBoxRequest Request { get; }

        /// <summary>Dialog title.</summary>
        public string Title => Request.Title;

        /// <summary>Dialog message.</summary>
        public string Message => Request.Message;

        /// <summary>Icon selected by dialog type.</summary>
        public MessageBoxIcon Icon => Request.Icon;

        /// <summary>Caller-supplied custom buttons.</summary>
        public IReadOnlyList<MessageBoxButton> CustomButtons => Request.CustomButtons;

        /// <summary>Whether the standard OK button is shown.</summary>
        public bool ShowsOk => Request.Buttons is MessageBoxButtons.Ok or MessageBoxButtons.OkCancel;

        /// <summary>Whether the standard Cancel button is shown.</summary>
        public bool ShowsCancel => Request.Buttons is MessageBoxButtons.OkCancel or MessageBoxButtons.YesNoCancel;

        /// <summary>Whether the standard Yes button is shown.</summary>
        public bool ShowsYes => Request.Buttons is MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel;

        /// <summary>Whether the standard No button is shown.</summary>
        public bool ShowsNo => Request.Buttons is MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel;

        /// <summary>Whether the 'Hide this notification?' checkbox belongs on this dialog.</summary>
        public bool IsSuppressionAvailable => Request.HasSuppressionOption;

        /// <summary>State of the 'Hide this notification?' checkbox.</summary>
        public bool IsSuppressChecked
        {
            get => _isSuppressChecked;
            set => SetProperty(ref _isSuppressChecked, value);
        }

        /// <summary>
        /// Whether Enter has anything to activate. A <see cref="MessageBoxButtons.None"/> dialog
        /// carrying only custom buttons — spec 11.9's 'Update Firmware' shape — shows no OK and no
        /// Yes, so Enter must not answer it with a result no visible button offers.
        /// </summary>
        public bool HasAcceptButton => ShowsOk || ShowsYes;

        /// <summary>
        /// Result activated by Enter (specs/11-feature-dialogs.md §11.9: "Enter activates OK/Yes");
        /// None when the dialog has neither button, in which case Enter does nothing at all.
        /// </summary>
        public MessageBoxResult AcceptResult
        {
            get
            {
                if (ShowsYes)
                {
                    return MessageBoxResult.Yes;
                }

                return ShowsOk ? MessageBoxResult.Ok : MessageBoxResult.None;
            }
        }

        /// <summary>Result produced by Escape ("Escape cancels"); None when the dialog has no Cancel button.</summary>
        public MessageBoxResult EscapeResult => ShowsCancel ? MessageBoxResult.Cancel : MessageBoxResult.None;

        /// <summary>The outcome once the dialog has been answered; null while it is open.</summary>
        public MessageBoxOutcome? Outcome
        {
            get => _outcome;
            private set => SetProperty(ref _outcome, value);
        }

        /// <summary>Closes the dialog with OK.</summary>
        public IRelayCommand OkCommand { get; }

        /// <summary>Closes the dialog with Cancel.</summary>
        public IRelayCommand CancelCommand { get; }

        /// <summary>Closes the dialog with Yes.</summary>
        public IRelayCommand YesCommand { get; }

        /// <summary>Closes the dialog with No.</summary>
        public IRelayCommand NoCommand { get; }

        /// <summary>Closes the dialog with a custom button.</summary>
        public IRelayCommand<MessageBoxButton> CustomButtonCommand { get; }

        /// <summary>Raised once, when the dialog is answered; the view closes itself on this.</summary>
        public event Action<MessageBoxOutcome>? Completed;

        private MessageBoxOutcome? _outcome;
        private bool _isSuppressChecked;

        /// <summary>Creates a view model for <paramref name="request"/>.</summary>
        public MessageBoxViewModel(MessageBoxRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));

            OkCommand = new RelayCommand(() => Complete(MessageBoxResult.Ok));
            CancelCommand = new RelayCommand(() => Complete(MessageBoxResult.Cancel));
            YesCommand = new RelayCommand(() => Complete(MessageBoxResult.Yes));
            NoCommand = new RelayCommand(() => Complete(MessageBoxResult.No));
            CustomButtonCommand = new RelayCommand<MessageBoxButton>(CompleteWithCustomButton);
        }

        /// <summary>
        /// Answers the dialog, folding in the checkbox state. Answering twice keeps the first
        /// outcome, so a double click cannot report two different answers.
        /// </summary>
        public MessageBoxOutcome Complete(MessageBoxResult result, string? customButtonId = null)
        {
            if (Outcome is not null)
            {
                return Outcome;
            }

            var outcome = new MessageBoxOutcome
            {
                Result = result,
                SuppressRequested = IsSuppressionAvailable && IsSuppressChecked,
                CustomButtonId = customButtonId
            };

            Outcome = outcome;

            Completed?.Invoke(outcome);

            return outcome;
        }

        private void CompleteWithCustomButton(MessageBoxButton? button)
        {
            if (button is null)
            {
                return;
            }

            Complete(MessageBoxResult.Custom, button.Id);
        }
    }
}
