using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>Hand-rolled <see cref="IUrlLauncher"/> recording every opened URL.</summary>
    internal sealed class FakeUrlLauncher : IUrlLauncher
    {
        public bool ResultToReturn { get; set; } = true;

        public List<string> OpenedUrls { get; } = [];

        public bool Open(string url)
        {
            OpenedUrls.Add(url);

            return ResultToReturn;
        }
    }
}
