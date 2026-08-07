namespace KinesisEdit.Services
{
    /// <summary>
    /// The app-wide <see cref="IVDriveWriteActivity"/>: an <see cref="Interlocked"/> counter of open
    /// write brackets.
    /// <para>
    /// Thread-safe by construction, because the two sides live on different threads — a save runs
    /// on a thread-pool thread and <see cref="DeviceLivenessWatcher"/> reads this from its own
    /// timer thread — and neither may block the other.
    /// </para>
    /// </summary>
    public sealed class VDriveWriteActivity : IVDriveWriteActivity
    {
        /// <summary>Whether at least one write bracket is open.</summary>
        public bool IsWriting => Volatile.Read(ref _openWriteCount) > 0;

        private int _openWriteCount;

        /// <summary>Opens a write bracket. Dispose the returned scope — once — to close it.</summary>
        public IDisposable Begin()
        {
            Interlocked.Increment(ref _openWriteCount);

            return new WriteScope(this);
        }

        private void End()
        {
            Interlocked.Decrement(ref _openWriteCount);
        }

        /// <summary>
        /// One open bracket. Its disposal is latched, so a scope released twice — by a
        /// <c>using</c> plus a stray explicit call — closes the bracket once and can never leave
        /// the counter negative, which would read as "never writing" for the rest of the session.
        /// </summary>
        private sealed class WriteScope : IDisposable
        {
            private readonly VDriveWriteActivity _owner;
            private int _isClosed;

            public WriteScope(VDriveWriteActivity owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _isClosed, 1) != 0)
                {
                    return;
                }

                _owner.End();
            }
        }
    }
}
