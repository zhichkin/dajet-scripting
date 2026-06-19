using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    internal static class DataTypeExtensions
    {
        internal static object DefaultValue(this DataType type)
        {
            object value; //TODO: move method to DaJet.TypeSystem library

            if (type.IsArray)
            {
                value = new List<Dictionary<string, object>>();
            }
            else if (type.IsObject)
            {
                value = new Dictionary<string, object>();
            }
            else if (type.IsUnion) { value = Union.Undefined; }
            else if (type.IsBoolean) { value = false; }
            else if (type.IsDecimal) { value = 0M; }
            else if (type.IsInteger) { value = type.Size == 4 ? 0 : 0L; }
            else if (type.IsDateTime) { value = DateTime.MinValue; }
            else if (type.IsString) { value = string.Empty; }
            else if (type.IsBinary) { value = Array.Empty<byte>(); }
            else if (type.IsUuid) { value = Guid.Empty; }
            else if (type.IsEntity) { value = Entity.Undefined; }
            else
            {
                value = null;
            }

            return value;
        }
    }
}