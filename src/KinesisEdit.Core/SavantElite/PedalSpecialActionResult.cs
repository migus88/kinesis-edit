namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Outcome of <see cref="PedalEditSession.Apply"/> (specs/12-savant-elite.md §6): whether the
    /// item was applied and, when it was not, why. A refusal changes nothing in the session, so the
    /// caller may simply report <see cref="Failure"/>.
    /// </summary>
    public readonly record struct PedalSpecialActionResult
    {
        /// <summary>Whether the item was applied.</summary>
        public bool Applied { get; }

        /// <summary>Why the item was refused; <see cref="PedalSpecialActionFailure.None"/> when it was applied.</summary>
        public PedalSpecialActionFailure Failure { get; }

        private PedalSpecialActionResult(bool applied, PedalSpecialActionFailure failure)
        {
            Applied = applied;
            Failure = failure;
        }

        /// <summary>The item was applied.</summary>
        public static PedalSpecialActionResult Success()
        {
            return new PedalSpecialActionResult(true, PedalSpecialActionFailure.None);
        }

        /// <summary>The item was refused for <paramref name="failure"/> and the session is unchanged.</summary>
        public static PedalSpecialActionResult Refused(PedalSpecialActionFailure failure)
        {
            return new PedalSpecialActionResult(false, failure);
        }
    }
}
