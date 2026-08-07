using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The counter <see cref="DeviceLivenessWatcher"/> reads before it stats a mount point. A save
    /// writes several files and a settings merge can nest inside one, so "is the app writing" has to
    /// be a depth, not a flag — and the two sides live on different threads.
    /// </summary>
    public class VDriveWriteActivityTests
    {
        [Fact]
        public void IsWriting_BeforeAnythingWrites_IsFalse()
        {
            var activity = new VDriveWriteActivity();

            Assert.False(activity.IsWriting);
        }

        [Fact]
        public void IsWriting_WhileABracketIsOpen_IsTrue()
        {
            var activity = new VDriveWriteActivity();

            using (activity.Begin())
            {
                Assert.True(activity.IsWriting);
            }

            Assert.False(activity.IsWriting);
        }

        /// <summary>
        /// Nested writes are the normal case — a profile save writes a layout file, a lighting file
        /// and then merges the settings — so the app is writing until the outermost bracket closes.
        /// </summary>
        [Fact]
        public void IsWriting_WithNestedBrackets_StaysTrueUntilTheLastOneCloses()
        {
            var activity = new VDriveWriteActivity();

            var outer = activity.Begin();
            var inner = activity.Begin();

            Assert.True(activity.IsWriting);

            inner.Dispose();

            Assert.True(activity.IsWriting);

            outer.Dispose();

            Assert.False(activity.IsWriting);
        }

        /// <summary>
        /// A scope released twice must close its bracket once. An underflowing counter would read as
        /// "never writing" for the rest of the session, and the watcher would happily scan through
        /// every later save.
        /// </summary>
        [Fact]
        public void Begin_WhenAScopeIsDisposedTwice_NeverDrivesTheCountBelowZero()
        {
            var activity = new VDriveWriteActivity();

            var first = activity.Begin();
            var second = activity.Begin();

            second.Dispose();
            second.Dispose();

            Assert.True(activity.IsWriting);

            first.Dispose();

            Assert.False(activity.IsWriting);
        }

        /// <summary>
        /// The two sides genuinely run on different threads — a save on the thread pool, the watcher
        /// on its timer — so the counter is exercised concurrently and must land back on zero.
        /// </summary>
        [Fact]
        public async Task Begin_FromManyThreadsAtOnce_EndsBackAtNotWriting()
        {
            var activity = new VDriveWriteActivity();
            var writers = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                for (var round = 0; round < 200; round++)
                {
                    using var write = activity.Begin();

                    Assert.True(activity.IsWriting);
                }
            }));

            await Task.WhenAll(writers);

            Assert.False(activity.IsWriting);
        }
    }
}
