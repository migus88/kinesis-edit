namespace KinesisEdit.Core.Profiles
{
    /// <summary>
    /// Thrown when <see cref="ProfileSession.Load"/>, <see cref="ProfileSession.Save"/>, or
    /// <see cref="ProfileSession.SaveAs"/> targets profile 0 on a device whose
    /// <see cref="Devices.LayoutFileScheme.HasReadOnlyFactoryProfile"/> is true: the Advantage
    /// 360's factory profile has no on-disk file at all (specs/02-devices.md "Profiles 0-9") and
    /// can only be reached from the keyboard's own memory, never edited or written by the app.
    /// </summary>
    public sealed class ProfileReadOnlyException : Exception
    {
        private const string DefaultMessageText =
            "Profile 0 is non-programmable so you must use the Save As Button...";

        /// <summary>Creates the exception with the spec 02 wording verbatim.</summary>
        public ProfileReadOnlyException()
            : base(DefaultMessageText)
        {
        }

        /// <summary>Creates the exception with a custom message.</summary>
        public ProfileReadOnlyException(string message)
            : base(message)
        {
        }

        /// <summary>Creates the exception with a custom message and inner exception.</summary>
        public ProfileReadOnlyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
