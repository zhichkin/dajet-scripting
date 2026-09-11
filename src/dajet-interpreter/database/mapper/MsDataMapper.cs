// Исключения из правил:
// - _KeyField (табличная часть) binary(4) -> int CanBeNumeric
// - _Folder (иерархические ссылочные типы) binary(1) -> bool инвертировать !!!
// - _Version (ссылочные типы) timestamp binary(8) -> IsBinary
// - _Type (тип значений характеристики) varbinary(max) -> IsBinary nullable
// - _RecordKind (вид движения накопления) numeric(1) CanBeNumeric Приход = 0, Расход = 1
// - _DimHash numeric(10) ?

// NOTE: SQL Server rowversion is unsigned big-endian value
// NOTE: 1C binary(4) is integer, unsigned big-endian value

using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;
using System.Data;
using System.Xml.Linq;

namespace DaJet.Scripting
{
    public sealed class MsDataMapper
    {
        private readonly static byte[] TRUE = [0x01];
        private readonly static byte[] FALSE = [0x00];
        private readonly static byte[] EMPTY_TYPE_CODE = [0x00000000];
        private readonly static byte[] EMPTY_UUID = [0x00000000000000000000000000000000];
        private readonly static byte[] VALUE_STORAGE = [0x01, 0x01, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xEF, 0xBB, 0xBF, 0x7B, 0x22, 0x55, 0x22, 0x7D];

        private readonly static byte[] TAG_UNDEFINED = [0x01];
        private readonly static byte[] TAG_BOOLEAN = [0x02];
        private readonly static byte[] TAG_DECIMAL = [0x03];
        private readonly static byte[] TAG_DATETIME = [0x04];
        private readonly static byte[] TAG_STRING = [0x05];
        private readonly static byte[] TAG_ENTITY = [0x08];

        public readonly static Func<object, object> ConvertTag = InputTag;
        public readonly static Func<object, object> ConvertBoolean = InputBoolean;
        public readonly static Func<object, object> ConvertNumeric = InputNumeric;
        public readonly static Func<object, int, object> ConvertDateTime = InputDateTime;
        public readonly static Func<object, object> ConvertString = InputString;
        public readonly static Func<object, object> ConvertBinary = InputBinary;
        public readonly static Func<object, object> ConvertUuid = InputUuid;
        public readonly static Func<object, object> ConvertTypeCode = InputTypeCode;
        public readonly static Func<object, object> ConvertIdentity = InputIdentity;
        
        private readonly int _yearOffset;
        private readonly byte[] _buffer = new byte[16];

        private readonly ScriptContext _context;
        private readonly SqlStatement _statement;
        private readonly EntityDefinition _outputSchema;
        private readonly Dictionary<ColumnDefinition, int> _ordinals = new();
        private string _commandText;
        public MsDataMapper(in ScriptContext context, in SqlStatement statement)
        {
            _context = context;
            _statement = statement;
            _yearOffset = statement.YearOffset;
            _outputSchema = statement.InferEntity();
            
            _commandText = _statement.Sql;
            
            PrepareOutputColumnOrdinals();
        }
        private void PrepareOutputColumnOrdinals()
        {
            int ordinal = 0; // column ordinals of SqlDataReader

            ColumnDefinition column;
            List<ColumnDefinition> columns;
            PropertyDefinition property;
            List<PropertyDefinition> properties = _outputSchema.Properties;

            for (int p = 0; p < properties.Count; p++)
            {
                property = properties[p];

                columns = property.Columns;

                for (int c = 0; c < columns.Count; c++)
                {
                    column = columns[c];

                    _ordinals.Add(column, ordinal++);
                }
            }
        }
        public string CommandText { get { return _commandText; } }
        public EntityDefinition OutputSchema { get { return _outputSchema; } }

        private static object InputTag(object value)
        {
            if (value is null) { return TAG_UNDEFINED; }
            else if (value is bool) { return TAG_BOOLEAN; }
            else if (value is decimal) { return TAG_DECIMAL; }
            else if (value is DateTime) { return TAG_DATETIME; }
            else if (value is string) { return TAG_STRING; }
            else if (value is Entity) { return TAG_ENTITY; }

            return TAG_UNDEFINED;
        }
        private static object InputBoolean(object value)
        {
            if (value is bool boolean)
            {
                return boolean ? TRUE : FALSE;
            }

            return FALSE;
        }
        private static object InputNumeric(object value)
        {
            if (value is decimal numeric)
            {
                return numeric;
            }

            return 0M;
        }
        private static object InputDateTime(object value, int yearOffset)
        {
            if (value is DateTime datetime)
            {
                return datetime.AddYears(yearOffset);
            }

            return DateTime.MinValue.AddYears(yearOffset);
        }
        private static object InputString(object value)
        {
            if (value is string text)
            {
                return text;
            }

            return string.Empty;
        }
        private static object InputBinary(object value)
        {
            if (value is bool boolean)
            {
                return boolean ? TRUE : FALSE;
            }
            else if (value is Entity entity)
            {
                return entity.Identity.ToByteArray();
            }
            else if (value is Guid uuid)
            {
                return uuid.ToByteArray();
            }
            else if (value is int integer)
            {
                Span<byte> buffer = stackalloc byte[4];

                BinaryPrimitives.WriteInt32BigEndian(buffer, integer);

                return buffer.ToArray();
            }
            else if (value is byte[] binary)
            {
                return binary;
            }

            return VALUE_STORAGE;
        }
        private static object InputUuid(object value)
        {
            if (value is Guid uuid)
            {
                return uuid.ToByteArray();
            }

            return EMPTY_UUID;
        }
        private static object InputTypeCode(object value)
        {
            if (value is Entity entity)
            {
                Span<byte> buffer = stackalloc byte[4];

                BinaryPrimitives.WriteInt32BigEndian(buffer, entity.TypeCode);

                return buffer.ToArray();
            }

            return EMPTY_TYPE_CODE;
        }
        private static object InputIdentity(object value)
        {
            if (value is Entity entity)
            {
                return entity.Identity.ToByteArray();
            }

            return EMPTY_UUID;
        }

        public void ProcessInput(in SqlCommand command)
        {
            command.Parameters.Clear();

            int ordinal = 0;

            foreach (SyntaxNode input in _statement.Input)
            {
                string name = string.Format("@p{0}", ordinal++);

                object value = _context.Evaluate(in input);

                if (value is null)
                {
                    command.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (value is bool boolean)
                {
                    command.Parameters.AddWithValue(name, boolean ? TRUE : FALSE);
                }
                else if (value is decimal)
                {
                    command.Parameters.AddWithValue(name, value);
                }
                else if (value is int int32)
                {
                    if (input is FunctionExpression function && function.Name == nameof(TYPEOF))
                    {
                        Span<byte> buffer = _buffer.AsSpan(0, 4);
                        BinaryPrimitives.WriteInt32BigEndian(buffer, int32);
                        command.Parameters.AddWithValue(name, buffer.ToArray());
                    }
                    else
                    {
                        command.Parameters.AddWithValue(name, int32);
                    }
                }
                else if (value is long int64)
                {
                    command.Parameters.AddWithValue(name, int64);
                }
                else if (value is DateTime date)
                {
                    date = date.AddYears(_yearOffset);
                    command.Parameters.AddWithValue(name, date).SqlDbType = SqlDbType.DateTime2;
                }
                else if (value is string text)
                {
                    command.Parameters.AddWithValue(name, text);
                }
                else if (value is byte[] binary)
                {
                    command.Parameters.AddWithValue(name, binary);
                }
                else if (value is Guid uuid)
                {
                    command.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else if (value is Entity entity)
                {
                    command.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                }
                else if (value is List<bool> array_boolean)
                {
                    InputArrayOfBoolean(in command, in name, in array_boolean);
                }
                else if (value is List<decimal> array_decimal)
                {
                    InputArrayOfDecimal(in command, in name, in array_decimal);
                }
                else if (value is List<int> array_int32)
                {
                    InputArrayOfInt32(in command, in name, in array_int32);
                }
                else if (value is List<long> array_int64)
                {
                    InputArrayOfInt64(in command, in name, in array_int64);
                }
                else if (value is List<DateTime> array_date)
                {
                    InputArrayOfDateTime(in command, in name, in array_date);
                }
                else if (value is List<string> array_string)
                {
                    InputArrayOfString(in command, in name, in array_string);
                }
                else if (value is List<byte[]> array_binary)
                {
                    InputArrayOfBinary(in command, in name, in array_binary);
                }
                else if (value is List<Guid> array_uuid)
                {
                    InputArrayOfUuid(in command, in name, in array_uuid);
                }
                else if (value is List<Entity> array_entity)
                {
                    InputArrayOfEntity(in command, in name, in array_entity);
                }
            }
        }
        private void InputArrayOfBoolean(in SqlCommand command, in string parameterName, in List<bool> array)
        {
            string name;
            bool value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value ? TRUE : FALSE);

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfDecimal(in SqlCommand command, in string parameterName, in List<decimal> array)
        {
            string name;
            decimal value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value);

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfInt32(in SqlCommand command, in string parameterName, in List<int> array)
        {
            string name;
            int value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value);

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfInt64(in SqlCommand command, in string parameterName, in List<long> array)
        {
            string name;
            long value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value);

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfDateTime(in SqlCommand command, in string parameterName, in List<DateTime> array)
        {
            string name;
            DateTime value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                value = value.AddYears(_yearOffset);

                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.DateTime2;

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfString(in SqlCommand command, in string parameterName, in List<string> array)
        {
            string name;
            string value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value);

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfBinary(in SqlCommand command, in string parameterName, in List<byte[]> array)
        {
            string name;
            byte[] value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value);

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfUuid(in SqlCommand command, in string parameterName, in List<Guid> array)
        {
            string name;
            Guid value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value.ToByteArray());

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }
        private void InputArrayOfEntity(in SqlCommand command, in string parameterName, in List<Entity> array)
        {
            string name;
            Entity value;
            string parameters = string.Empty;

            for (int p = 0; p < array.Count; p++)
            {
                name = string.Format("{0}_{1}", parameterName, p);

                value = array[p];

                command.Parameters.AddWithValue(name, value.Identity.ToByteArray());

                if (p > 0) { parameters += ", "; }

                parameters += name;
            }

            _commandText = _commandText.Replace(parameterName, parameters);
        }

        public void ProcessOutput(in SqlDataReader reader, in DataObject record)
        {
            foreach (PropertyDefinition property in _outputSchema.Properties)
            {
                DataType type = property.Type;

                if (type.IsUnion) { record.SetValue(property.Name, GetUnion(in reader, in property)); }
                else if (type.IsBoolean) { record.SetValue(property.Name, GetBoolean(in reader, in property)); }
                else if (type.IsDecimal) { record.SetValue(property.Name, GetDecimal(in reader, in property)); }
                else if (type.IsDateTime) { record.SetValue(property.Name, GetDateTime(in reader, in property)); }
                else if (type.IsString) { record.SetValue(property.Name, GetString(in reader, in property)); }
                else if (type.IsBinary) { record.SetValue(property.Name, GetBinary(in reader, in property)); }
                else if (type.IsUuid) { record.SetValue(property.Name, GetUuid(in reader, in property)); }
                else if (type.IsEntity)
                {
                    record.SetValue(property.Name, GetEntity(in reader, in property));
                }
                else if (type.IsInteger)
                {
                    if (type.Size == 4)
                    {
                        record.SetValue(property.Name, GetInt32(in reader, in property));
                    }
                    else
                    {
                        record.SetValue(property.Name, GetInt64(in reader, in property));
                    }
                }
            }
        }
        private bool GetBoolean(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= output.GetColumnByPurpose(ColumnPurpose.Boolean); // union type column

            if (column is null)
            {
                return false;
            }

            int ordinal = _ordinals[column];

            if (reader.IsDBNull(ordinal))
            {
                return false;
            }

            _ = reader.GetBytes(ordinal, 0L, _buffer, 0, 1);

            bool value = (_buffer[0] == 1);

            if (column.Name == "_Folder" || column.Name == "_folder") // ЭтоГруппа
            {
                value = !value; // invert - exceptional 1C case
            }

            return value;
        }
        private decimal GetDecimal(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= output.GetColumnByPurpose(ColumnPurpose.Numeric); // union type column

            if (column is null)
            {
                return 0M;
            }

            int ordinal = _ordinals[column];

            return reader.IsDBNull(ordinal) ? 0M : reader.GetDecimal(ordinal);
        }
        private int GetInt32(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value);

            if (column is null)
            {
                return 0;
            }

            int ordinal = _ordinals[column];

            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }
        private long GetInt64(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value);

            if (column is null)
            {
                return 0L;
            }

            int ordinal = _ordinals[column];

            return reader.IsDBNull(ordinal) ? 0L : reader.GetInt64(ordinal);
        }
        private DateTime GetDateTime(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= output.GetColumnByPurpose(ColumnPurpose.DateTime); // union type column

            if (column is null)
            {
                return DateTime.MinValue;
            }

            int ordinal = _ordinals[column];

            return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal).AddYears(-_yearOffset);
        }
        private string GetString(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= output.GetColumnByPurpose(ColumnPurpose.String); // union type column

            if (column is null)
            {
                return string.Empty;
            }

            int ordinal = _ordinals[column];

            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }
        private byte[] GetBinary(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value);

            if (column is null)
            {
                return Array.Empty<byte>();
            }

            int ordinal = _ordinals[column];

            return reader.IsDBNull(ordinal) ? Array.Empty<byte>() : (byte[])reader.GetValue(ordinal);
        }
        private Guid GetUuid(in SqlDataReader reader, in PropertyDefinition output)
        {
            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value);

            if (column is null)
            {
                return Guid.Empty;
            }

            int ordinal = _ordinals[column];

            if (column.Type.IsUuid)
            {
                return reader.GetGuid(ordinal); // NEWUUID function in SELECT clause
            }

            _ = reader.GetBytes(ordinal, 0L, _buffer, 0, 16);

            return new Guid(_buffer);
        }
        private Entity GetEntity(in SqlDataReader reader, in PropertyDefinition output)
        {
            int ordinal;
            int typeCode;
            Guid identity;

            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Value);

            // single value column

            if (column is not null)
            {
                ordinal = _ordinals[column];

                if (reader.IsDBNull(ordinal))
                {
                    return Entity.Undefined;
                }

                _ = reader.GetBytes(ordinal, 0L, _buffer, 0, 16);

                identity = new Guid(_buffer);
                typeCode = output.Type.TypeCode;

                return new Entity(typeCode, identity);
            }

            // union type value

            column = output.GetColumnByPurpose(ColumnPurpose.TypeCode);

            if (column is not null)
            {
                ordinal = _ordinals[column];

                if (reader.IsDBNull(ordinal))
                {
                    return Entity.Undefined;
                }

                _ = reader.GetBytes(ordinal, 0L, _buffer, 0, 4);

                typeCode = BinaryPrimitives.ReadInt32BigEndian(_buffer.AsSpan(0, 4));
            }
            else
            {
                typeCode = output.Type.TypeCode;
            }

            column = output.GetColumnByPurpose(ColumnPurpose.Identity);

            if (column is null)
            {
                return Entity.Undefined;
            }

            ordinal = _ordinals[column];

            _ = reader.GetBytes(ordinal, 0L, _buffer, 0, 16);

            identity = new Guid(_buffer);

            return new Entity(typeCode, identity);
        }
        private object GetUnion(in SqlDataReader reader, in PropertyDefinition output)
        {
            // _TYPE binary(1) may be generated by query engine if value is not stored in the database.
            // TAG value is generated by query engine in case data type addition operation takes place.
            // Type extension operations, resulting from CASE and UNION, are not implemented by DaJet.

            ColumnDefinition column = output.GetColumnByPurpose(ColumnPurpose.Tag);

            if (column is null) // IsReferenceOnlyUnion
            {
                return GetEntity(in reader, in output);
            }

            int ordinal = _ordinals[column];

            if (reader.IsDBNull(ordinal))
            {
                return Union.Undefined;
            }

            _ = reader.GetBytes(ordinal, 0L, _buffer, 0, 1);

            byte tag = _buffer[0];

            if (tag == 1) // Неопределено
            {
                return Union.Undefined;
            }
            else if (tag == 2) // Булево
            {
                return new Union.CaseBoolean(GetBoolean(in reader, in output));
            }
            else if (tag == 3) // Число
            {
                return new Union.CaseDecimal(GetDecimal(in reader, in output));
            }
            else if (tag == 4) // Дата
            {
                return new Union.CaseDateTime(GetDateTime(in reader, in output));
            }
            else if (tag == 5) // Строка
            {
                return new Union.CaseString(GetString(in reader, in output));
            }
            else if (tag == 8) // Ссылка
            {
                return new Union.CaseEntity(GetEntity(in reader, in output));
            }

            throw new InvalidOperationException($"Invalid union tag value: [{tag}]");
        }
    }
}