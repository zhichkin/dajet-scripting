using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Scripting
{
    public static class PublicFunctions
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
        }
    }
}