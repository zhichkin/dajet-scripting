using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Npgsql;
using NpgsqlTypes;
using System.Buffers.Binary;
using System.Collections;

namespace DaJet.Scripting
{
    public sealed class PgBulkInsertMapper : IEnumerable<DataObject>, IEnumerator<DataObject>
    {
        private int _current;
        private bool _skipped;
        private DataObject _record;
        private List<DataObject> _buffer;
        private readonly int _batchSize;
        private readonly int _yearOffset;
        private readonly ScriptContext _context;
        private readonly string _bufferItem;
        private readonly EntityDefinition _table;
        private readonly Dictionary<string, ColumnExpression> _map = new();
        private readonly Dictionary<ColumnDefinition, Action<NpgsqlBinaryImporter, DataType, object>> _converters = new();
        private static void ConvertTag(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            if (value is null)
            {
                importer.Write(Constants.TAG_UNDEFINED, NpgsqlDbType.Bytea);
            }
            else if (value is Union union)
            {
                switch (union.Tag)
                {
                    case UnionTag.Boolean: importer.Write(Constants.TAG_BOOLEAN, NpgsqlDbType.Bytea); break;
                    case UnionTag.Decimal: importer.Write(Constants.TAG_NUMERIC, NpgsqlDbType.Bytea); break;
                    case UnionTag.DateTime: importer.Write(Constants.TAG_DATETIME, NpgsqlDbType.Bytea); break;
                    case UnionTag.String: importer.Write(Constants.TAG_STRING, NpgsqlDbType.Bytea); break;
                    case UnionTag.Entity: importer.Write(Constants.TAG_ENTITY, NpgsqlDbType.Bytea); break;
                    default: importer.Write(Constants.TAG_UNDEFINED, NpgsqlDbType.Bytea); break;
                }
            }
            else
            {
                Type source = value.GetType();

                if (source == typeof(bool)) { importer.Write(Constants.TAG_BOOLEAN, NpgsqlDbType.Bytea); }
                else if (source == typeof(decimal)) { importer.Write(Constants.TAG_NUMERIC, NpgsqlDbType.Bytea); }
                else if (source == typeof(DateTime)) { importer.Write(Constants.TAG_DATETIME, NpgsqlDbType.Bytea); }
                else if (source == typeof(string)) { importer.Write(Constants.TAG_STRING, NpgsqlDbType.Bytea); }
                else if (source == typeof(Entity)) { importer.Write(Constants.TAG_ENTITY, NpgsqlDbType.Bytea); }
                else if (source == typeof(int)) { importer.Write(Constants.TAG_NUMERIC, NpgsqlDbType.Bytea); }
                else if (source == typeof(long)) { importer.Write(Constants.TAG_NUMERIC, NpgsqlDbType.Bytea); }
                else { throw new InvalidCastException($"[DATA MAPPER] Unsupported union data type {source}"); }
            }
        }
        private static void ConvertBoolean(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            if (value is bool boolean)
            {
                importer.Write(boolean, NpgsqlDbType.Boolean);
            }
            else if (value is Union union && union.Tag == UnionTag.Boolean)
            {
                importer.Write(union.GetBoolean(), NpgsqlDbType.Boolean);
            }
            else
            {
                importer.Write(false, NpgsqlDbType.Boolean);
            }
        }
        private static void ConvertNumeric(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            if (value is decimal numeric)
            {
                importer.Write(numeric, NpgsqlDbType.Numeric);
            }
            else if (value is int integer)
            {
                importer.Write(new decimal(integer), NpgsqlDbType.Numeric);
            }
            else if (value is long int64)
            {
                importer.Write(new decimal(int64), NpgsqlDbType.Numeric);
            }
            else if (value is Union union && union.Tag == UnionTag.Decimal)
            {
                importer.Write(union.GetDecimal(), NpgsqlDbType.Numeric);
            }
            else
            {
                importer.Write(0M, NpgsqlDbType.Numeric);
            }
        }
        private void ConvertDateTime(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            DateTime timestamp;

            if (value is DateTime datetime)
            {
                timestamp = datetime.AddYears(_yearOffset);
            }
            else if (value is Union union && union.Tag == UnionTag.DateTime)
            {
                timestamp = union.GetDateTime().AddYears(_yearOffset);
            }
            else
            {
                timestamp = DateTime.MinValue.AddYears(_yearOffset);
            }

            timestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, timestamp.Minute, timestamp.Second, DateTimeKind.Unspecified);

            importer.Write(timestamp, NpgsqlDbType.Timestamp);
        }
        private static void ConvertString(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            NpgsqlDbType dbType = target.IsFixed ? NpgsqlDbType.Char : NpgsqlDbType.Varchar;

            if (value is string text)
            {
                if (target.Size > 0 && text.Length > target.Size)
                {
                    throw new InvalidCastException($"[DATA MAPPER] String data would be truncated (length {text.Length}, max {target.Size}).");
                }

                importer.Write(text, dbType);
            }
            else if (value is Union union && union.Tag == UnionTag.String)
            {
                text = union.GetString();

                if (text is null)
                {
                    importer.Write(string.Empty, dbType);
                }
                else
                {
                    if (target.Size > 0 && text.Length > target.Size)
                    {
                        throw new InvalidCastException($"[DATA MAPPER] String data would be truncated (length {text.Length}, max {target.Size}).");
                    }

                    importer.Write(text, dbType);
                }
            }
            else
            {
                importer.Write(string.Empty, dbType);
            }
        }
        private static void ConvertBinary(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            if (value is byte[] binary)
            {
                importer.Write(binary, NpgsqlDbType.Bytea);
            }
            else
            {
                importer.Write(Constants.VALUE_STORAGE, NpgsqlDbType.Bytea);
            }
        }
        private static void ConvertUuid(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            if (value is Guid uuid)
            {
                importer.Write(uuid.ToByteArray(), NpgsqlDbType.Bytea);
            }
            else
            {
                importer.Write(Constants.EMPTY_UUID, NpgsqlDbType.Bytea);
            }
        }
        private static void ConvertTypeCode(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            int code = 0;

            if (value is Entity entity)
            {
                code = entity.TypeCode;
            }
            else if (value is Union union && union.Tag == UnionTag.Entity)
            {
                code = union.GetEntity().TypeCode;
            }

            if (code > 0)
            {
                Span<byte> buffer = stackalloc byte[4];

                BinaryPrimitives.WriteInt32BigEndian(buffer, code);

                importer.Write(buffer.ToArray(), NpgsqlDbType.Bytea);
            }
            else
            {
                importer.Write(Constants.EMPTY_TYPE_CODE, NpgsqlDbType.Bytea);
            }
        }
        private static void ConvertIdentity(NpgsqlBinaryImporter importer, DataType target, object value)
        {
            if (value is Entity entity)
            {
                importer.Write(entity.Identity.ToByteArray(), NpgsqlDbType.Bytea);
            }
            else if (value is Union union && union.Tag == UnionTag.Entity)
            {
                importer.Write(union.GetEntity().Identity.ToByteArray(), NpgsqlDbType.Bytea);
            }
            else
            {
                importer.Write(Constants.EMPTY_UUID, NpgsqlDbType.Bytea);
            }
        }
        public PgBulkInsertMapper(in ScriptContext context, in InsertStatement statement, in string bufferItem)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentNullException.ThrowIfNull(statement, nameof(statement));
            ArgumentNullException.ThrowIfNullOrEmpty(bufferItem, nameof(bufferItem));

            _context = context;
            _batchSize = statement.BatchSize;
            _yearOffset = statement.YearOffset;
            _bufferItem = bufferItem;

            foreach (ColumnExpression map in statement.Values)
            {
                if (map.Expression is FunctionExpression function && function.Token == Token.VECTOR)
                {
                    continue; // column value is set by SEQUENCE
                }

                _map.Add(map.Alias, map);
            }

            if (statement.Target is not TableReference target || target.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException();
            }

            _table = table;
            
            PrepareConverters(in _table);
        }
        object IEnumerator.Current { get { return Current; } }
        public IEnumerator<DataObject> GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public IEnumerator<DataObject> Enumerate(in List<DataObject> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

            _buffer = buffer; Reset(); return this;
        }
        public void Reset() { _current = -1; _skipped = false; }
        public DataObject Current { get { return _record; } }
        public bool CanRead()
        {
            return (_skipped || (_buffer is not null && _current + 1 < _buffer.Count));
        }
        public bool MoveNext()
        {
            if (_skipped)
            {
                _skipped = false; return true;
            }

            if (++_current >= _buffer.Count)
            {
                return false;
            }

            DataObject current = _buffer[_current];

            _context.SetValue(in _bufferItem, current);

            _record = current;

            if (_current > 0 && _current % _batchSize == 0)
            {
                _skipped = true; return false;
            }
            
            return true;
        }
        public void Dispose()
        {
            if (_buffer is null)
            {
                return; // disposed
            }

            if (_current < _buffer.Count)
            {
                return; // skipped
            }

            _buffer = null;
            _record = null;
        }

        private void PrepareConverters(in EntityDefinition table)
        {
            foreach (PropertyDefinition property in table.Properties)
            {
                DataType input = property.Type;

                foreach (ColumnDefinition column in property.Columns)
                {
                    if (column.IsGenerated)
                    {
                        continue; // database auto-generated column
                    }

                    Action<NpgsqlBinaryImporter, DataType, object> converter = null;

                    if (column.Purpose == ColumnPurpose.Value)
                    {
                        if (input.IsBoolean) { converter = ConvertBoolean; }
                        else if (input.IsDecimal) { converter = ConvertNumeric; }
                        else if (input.IsInteger) { converter = ConvertNumeric; }
                        else if (input.IsDateTime) { converter = ConvertDateTime; }
                        else if (input.IsString) { converter = ConvertString; }
                        else if (input.IsBinary) { converter = ConvertBinary; }
                        else if (input.IsUuid) { converter = ConvertUuid; }
                        else if (input.IsEntity) { converter = ConvertIdentity; }
                    }
                    else if (column.Purpose == ColumnPurpose.Tag) { converter = ConvertTag; }
                    else if (column.Purpose == ColumnPurpose.Boolean) { converter = ConvertBoolean; }
                    else if (column.Purpose == ColumnPurpose.Numeric) { converter = ConvertNumeric; }
                    else if (column.Purpose == ColumnPurpose.DateTime) { converter = ConvertDateTime; }
                    else if (column.Purpose == ColumnPurpose.String) { converter = ConvertString; }
                    else if (column.Purpose == ColumnPurpose.TypeCode) { converter = ConvertTypeCode; }
                    else if (column.Purpose == ColumnPurpose.Identity) { converter = ConvertIdentity; }
                    
                    _converters.Add(column, converter);
                }
            }
        }
        public void WriteRowValues(in NpgsqlBinaryImporter importer)
        {
            DataObject record = Current;

            importer.StartRow();
            importer.Write(_current, NpgsqlDbType.Integer); // order_column
            
            object value;
            Action<NpgsqlBinaryImporter, DataType, object> converter;

            foreach (PropertyDefinition property in _table.Properties)
            {
                value = null; // default value

                if (_map.TryGetValue(property.Name, out ColumnExpression map))
                {
                    value = _context.Evaluate(map.Expression);
                }

                foreach (ColumnDefinition column in property.Columns)
                {
                    if (column.IsGenerated)
                    {
                        continue; // database auto-generated column
                    }

                    converter = _converters[column];

                    try
                    {
                        converter(importer, column.Type, value);
                    }
                    catch (InvalidCastException error)
                    {
                        throw new InvalidCastException($"[BULK INSERT] {error.Message} Column: {column.Name} [record {_current + 1}]", error);
                    }
                }
            }
        }
    }
}