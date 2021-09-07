namespace slskd.Management.API
{
    public class CheckVersionResponse
    {
        public bool? UpdateAvailable { get; init; }
        public string LatestVersion { get; init; }
    }
}
