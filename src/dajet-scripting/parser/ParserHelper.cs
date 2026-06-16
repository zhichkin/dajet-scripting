using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    internal static class ParserHelper
    {
        private static Dictionary<string, Type> _datatype = new()
        {
            { "boolean", typeof(bool) }, // L
            { "number", typeof(decimal) }, 
            { "integer", typeof(int) },
            { "decimal", typeof(decimal) }, // N
            { "datetime", typeof(DateTime) }, // T
            { "string", typeof(string) }, // S
            { "binary", typeof(byte[]) }, // B
            { "uuid", typeof(Guid) }, // U
            { "entity", typeof(Entity) }, // #
            { "union", typeof(Union) },
            { "object", typeof(object) },
            { "array", typeof(Array) }
        };
        internal static bool IsDataType(string identifier, out Type type)
        {
            return _datatype.TryGetValue(identifier, out type);
        }
    }
}