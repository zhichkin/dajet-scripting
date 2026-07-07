using System.Text.Json.Serialization;

namespace DaJet.Host
{
    public sealed class HostSettings
    {
        [JsonPropertyName("root")] public string Root { get; set; } = "api";
    }
}