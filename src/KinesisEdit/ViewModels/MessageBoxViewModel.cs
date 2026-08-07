using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The app-wide message box of specs/11-feature-dialogs.md §11.9: title, message, icon, the
    /// standard Yes/No/OK/Cancel buttons plus caller-supplied custom buttons, and the
    /// 'Don't ask this again' checkbox — which appears only when the request carries a
    /// suppression key. The answer leaves as a structured <see cref="MessageBoxOutcome"/>; the
    /// legacy "+100 modal result" encoding is not reproduced.
    /// <para>
    /// Mockup 1k labels the buttons by <b>outcome</b> ("Cancel · Key data only · Include macros"),
    /// so each standard button's caption can be overridden per request. Only the label moves: Yes
    /// is still Yes to every caller, which is what keeps their <c>switch</c> and their
    /// <see cref="MessageBoxRequest.SuppressedResult"/> honest.
    /// </para>
    /// <para>
    /// One answer may additionally be marked <b>destructive</b>, which is what puts the red
    /// <c>discard</c> face on it. There is deliberately no such flag for Cancel: Cancel is the way
    /// out of the dialog and can never be the answer that loses data, so a red Cancel would be a
    /// contradiction rather than a state.
    /// </para>
    /// </summary>
    public sealed class MessageBoxViewModel : ViewModelBase
    {
        /// <summary>
        /// Caption of the suppression checkbox when the request names none of its own. Mockup 1k's
        /// wording, which supersedes spec 11 §11.9's "Hide this notification?" — the flag it writes
        /// is unchanged (specs/08-settings.md §3).
        /// </summary>
        public const string DefaultSuppressionCaption = "Don't ask this again";

        /// <summary>Label of the Yes button when the request names no outcome of its own.</summary>
        public const string DefaultYesCaption = "Yes";

        /// <summary>Label of the No button when the request names no outcome of its own.</summary>
        public const string DefaultNoCaption = "No";

        /// <summary>Label of the OK button when the request names no outcome of its own.</summary>
        public const string DefaultOkCaption = "OK";

        /// <summary>Label of the Cancel button when the request names no outcome of its own.</summary>
        public const string DefaultCancelCaption = "Cancel";

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

        /// <summary>Label of the Yes button — the request's own outcome name, or "Yes".</summary>
        public string YesCaption => Caption(Request.YesCaption, DefaultYesCaption);

        /// <summary>Label of the No button — the request's own outcome name, or "No".</summary>
        public string NoCaption => Caption(Request.NoCaption, DefaultNoCaption);

        /// <summary>Label of the OK button — the request's own outcome name, or "OK".</summary>
        public string OkCaption => Caption(Request.OkCaption, DefaultOkCaption);

        /// <summary>Label of the Cancel button — the request's own outcome name, or "Cancel".</summary>
        public string CancelCaption => Caption(Request.CancelCaption, DefaultCancelCaption);

        /// <summary>
        /// Caption of the suppression checkbox — the request's own wording, or
        /// <see cref="DefaultSuppressionCaption"/>. A box whose opt-out promises something
        /// ("Don't ask again — always save on leaving") has to say so; the flag it writes is the
        /// same <c>*_msg</c> key either way.
        /// </summary>
        public string SuppressionCaption => Request.SuppressionCaption ?? DefaultSuppressionCaption;

        /// <summary>
        /// Whether the card takes the design's wide (420 px) width rather than the default 330 px
        /// one — the leave-with-unsaved modal of docs/design/handoff.md §2, whose body is a
        /// paragraph rather than a sentence and whose three answers are named by outcome.
        /// </summary>
        public bool IsWide => Request.IsWide;

        /// <summary>Whether Yes is the answer that loses data, and so wears the red face.</summary>
        public bool IsYesDestructive => Request.DestructiveResult == MessageBoxResult.Yes;

        /// <summary>Whether No is the answer that loses data, and so wears the red face.</summary>
        public bool IsNoDestructive => Request.DestructiveResult == MessageBoxResult.No;

        /// <summary>Whether OK is the answer that loses data, and so wears the red face.</summary>
        public bool IsOkDestructive => Request.DestructiveResult == MessageBoxResult.Ok;

        /// <summary>Whether the 'Don't ask this again' checkbox belongs on this dialog.</summary>
        public bool IsSuppressionAvailable => Request.HasSuppressionOption;

        /// <summary>State of the 'Don't ask this again' checkbox.</summary>
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

        private static string Caption(string? requested, string standard)
        {
            return string.IsNullOrWhiteSpace(requested) ? standard : requested;
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
