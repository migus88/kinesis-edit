using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The published-versions endpoints of specs/09-firmware.md §3 step 3 — one per master app.
    /// Pure data: the URL to GET, never the GET itself (see <see cref="IVersionManifestClient"/>).
    /// </summary>
    public static class VersionEndpoints
    {
        /// <summary>Endpoint of the gaming master app (Edge RGB, TKO).</summary>
        public const string GamingUrl = "https://gaming.kinesis-ergo.com/wp-json/ksv/v1/get_versions";

        /// <summary>Endpoint of the office master app (Advantage2, Advantage 360, Savant Elite 2).</summary>
        public const string OfficeUrl = "https://kinesis-ergo.com/wp-json/ksv/v1/get_versions";

        /// <summary>
        /// The endpoint serving <paramref name="servingApp"/>, or null for the apps the spec gives
        /// no endpoint for (<see cref="ServingApp.None"/> and the standalone FS Edge/Pro app, which
        /// has no update dialog at all — §4).
        /// </summary>
        public static string? FindUrl(ServingApp servingApp)
        {
            return servingApp switch
            {
                ServingApp.SmartSetMasterGaming => GamingUrl,
                ServingApp.SmartSetMasterOffice => OfficeUrl,
                _ => null
            };
        }

        /// <summary>
        /// The endpoint for a device, resolved through its <see cref="DeviceDefinition.ServingApp"/>.
        /// Null for unknown ids and for devices served by no master app. Says nothing about whether
        /// the device offers the update dialog — that is <see cref="UpdateCheckEligibility"/>.
        /// </summary>
        public static string? FindUrl(DeviceId deviceId)
        {
            var device = DeviceCatalog.All.FirstOrDefault(candidate => candidate.Id == deviceId);

            return device is null ? null : FindUrl(device.ServingApp);
        }
    }
}
