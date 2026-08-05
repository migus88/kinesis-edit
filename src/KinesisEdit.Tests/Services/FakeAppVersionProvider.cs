using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>Hand-rolled <see cref="IAppVersionProvider"/> reporting a pinned version.</summary>
    internal sealed class FakeAppVersionProvider : IAppVersionProvider
    {
        /// <summary>The version reported, unless <see cref="ExceptionToThrow"/> is set.</summary>
        public string Version
        {
            get
            {
                if (ExceptionToThrow is not null)
                {
                    throw ExceptionToThrow;
                }

                return _version;
            }

            set => _version = value;
        }

        /// <summary>
        /// When set, reading <see cref="Version"/> throws it — the seam for a failure that happens
        /// outside the endpoint request, where the real provider reads a build resource.
        /// </summary>
        public Exception? ExceptionToThrow { get; set; }

        private string _version = "2.0.20.0";
    }
}
