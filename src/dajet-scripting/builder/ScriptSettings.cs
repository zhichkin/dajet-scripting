using System.Text.Json.Serialization;

namespace DaJet.Scripting
{
    public sealed class ScriptSettings
    {
        public static ScriptSettings Default { get; }
        static ScriptSettings() { Default = new ScriptSettings(); }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }
}