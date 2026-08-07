using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IVolumeLivenessProbe"/>: a test says which roots have gone away, and
    /// waits on a signal for the probe that asks — no sleeping on a timer's schedule.
    /// <para>
    /// It also records the thread each probe ran on, which is how the suite proves the watcher does
    /// its stat work off whatever thread asked for the last scan.
    /// </para>
    /// </summary>
    internal sealed class FakeVolumeLivenessProbe : IVolumeLivenessProbe, IDisposable
    {
        private static readonly TimeSpan _defaultWaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>Every root this probe was asked about, in order.</summary>
        public IReadOnlyList<string> ProbedPaths
        {
            get
            {
                lock (_syncRoot)
                {
                    return [.. _probedPaths];
                }
            }
        }

        /// <summary>How many probes have run.</summary>
        public int ProbeCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _probedPaths.Count;
                }
            }
        }

        /// <summary>The managed thread each probe ran on.</summary>
        public IReadOnlyList<int> ProbeThreadIds
        {
            get
            {
                lock (_syncRoot)
                {
                    return [.. _probeThreadIds];
                }
            }
        }

        /// <summary>Whether every probe so far ran on a thread-pool thread.</summary>
        public bool RanOnlyOnThreadPoolThreads
        {
            get
            {
                lock (_syncRoot)
                {
                    return _ranOnlyOnThreadPoolThreads;
                }
            }
        }

        private readonly object _syncRoot = new();
        private readonly List<string> _probedPaths = [];
        private readonly List<int> _probeThreadIds = [];
        private readonly HashSet<string> _missingPaths = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _probed = new(0);
        private bool _ranOnlyOnThreadPoolThreads = true;

        /// <summary>Makes <paramref name="rootPath"/> answer "gone" from the next probe onward.</summary>
        public void SetMissing(string rootPath)
        {
            lock (_syncRoot)
            {
                _missingPaths.Add(rootPath);
            }
        }

        /// <summary>Makes <paramref name="rootPath"/> answer "present" again.</summary>
        public void SetPresent(string rootPath)
        {
            lock (_syncRoot)
            {
                _missingPaths.Remove(rootPath);
            }
        }

        public bool IsPresent(string rootPath)
        {
            bool isPresent;

            lock (_syncRoot)
            {
                _probedPaths.Add(rootPath);
                _probeThreadIds.Add(Environment.CurrentManagedThreadId);
                _ranOnlyOnThreadPoolThreads &= Thread.CurrentThread.IsThreadPoolThread;

                isPresent = !_missingPaths.Contains(rootPath);
            }

            _probed.Release();

            return isPresent;
        }

        /// <summary>
        /// Waits for one probe that has not been waited on yet. Bounded, so a regression fails the
        /// test instead of hanging the suite.
        /// </summary>
        public bool WaitForProbe()
        {
            return _probed.Wait(_defaultWaitTimeout);
        }

        /// <summary>
        /// Waits for one probe with the caller's own timeout — the shape a test uses to assert that
        /// nothing probes at all.
        /// </summary>
        public bool WaitForProbe(TimeSpan timeout)
        {
            return _probed.Wait(timeout);
        }

        public void Dispose()
        {
            _probed.Dispose();
        }
    }
}
