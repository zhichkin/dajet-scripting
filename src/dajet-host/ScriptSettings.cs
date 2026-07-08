using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Host
{
    public sealed class ScriptSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static ScriptSettings()
        {
            Default = new ScriptSettings();

            JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }
        [JsonIgnore] public static ScriptSettings Default { get; }
        public static ScriptSettings Create(in string filePath)
        {
            if (!File.Exists(filePath))
            {
                return Default;
            }

            ScriptSettings settings = null;

            try
            {
                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();

                    settings = JsonSerializer.Deserialize<ScriptSettings>(json, JsonOptions);
                }
            }
            catch (Exception exception)
            {
                string message = $"[ERROR][{Path.GetFileName(filePath)}] Failed to read script settings: {exception.Message}";

                throw new InvalidOperationException(message);
            }

            return settings;
        }
        
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("service")] public bool Service { get; set; } // Call, Auto
        [JsonPropertyName("durable")] public bool Durable { get; set; } // long running
        [JsonPropertyName("logger")] public bool Logger { get; set; } // has own private log
        [JsonPropertyName("singleton")] public bool Singleton { get; set; } // only one at a time
    }
}