using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient.Server;
using Npgsql;
using NpgsqlTypes;
using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace DaJet.Scripting
{
    public sealed class PgBulkInsertMapper : IEnumerable<SqlDataRecord>, IEnumerator<SqlDataRecord>, IDataReader
    {
        private int _current;
        private bool _skipped;
        private List<DataObject> _buffer;
        private readonly int _batchSize;
        private readonly int _yearOffset;
        private readonly SqlDataRecord _record;
        private readonly ScriptContext _context;
        private readonly string _bufferItem;
        private readonly EntityDefinition _table;
        private readonly Func<object, object> _convertDateTime;
        private readonly Dictionary<string, ColumnExpression> _map = new();
        private readonly Dictionary<ColumnDefinition, int> _ordinals = new();
        private readonly Dictionary<ColumnDefinition, Func<object, object>> _converters = new();
        public PgBulkInsertMapper(in ScriptContext context, in InsertStatement statement, in string bufferItem)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentNullException.ThrowIfNull(statement, nameof(statement));
            ArgumentNullException.ThrowIfNullOrEmpty(bufferItem, nameof(bufferItem));

            _context = context;
            _batchSize = statement.BatchSize;
            _yearOffset = statement.YearOffset;
            _bufferItem = bufferItem;
            _convertDateTime = ConvertDateTimeWithOffset;
            
            if (statement.Target is not TableReference target || target.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException();
            }

            _table = table;

            foreach (ColumnExpression map in statement.Values)
            {
                if (map.Expression is FunctionExpression function && function.Token == Token.VECTOR)
                {
                    continue; // column value is set by SEQUENCE
                }

                _map.Add(map.Alias, map);
            }

            SqlMetaData[] columns = PrepareTableTypeMetadata(in table);

            _record = new SqlDataRecord(columns);
        }
        object IEnumerator.Current { get { return Current; } }
        public IEnumerator<SqlDataRecord> GetEnumerator() { return this; }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public IEnumerator<SqlDataRecord> Enumerate(in List<DataObject> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

            _buffer = buffer; Reset(); return this;
        }
        public void Reset() { _current = -1; _skipped = false; }
        public SqlDataRecord Current { get { return _record; } }
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

            SetDataRecordValues(in _record, _current);

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
                return;
            }

            if (_current < _buffer.Count)
            {
                return;
            }

            _buffer = null;

            if (_record is not null)
            {
                for (int i = 0; i < _record.FieldCount; i++)
                {
                    _record.SetValue(i, null);
                }
            }
        }

        private SqlMetaData[] PrepareTableTypeMetadata(in EntityDefinition table)
        {
            List<SqlMetaData> columns = new();

            columns.Add(new SqlMetaData("order_column", SqlDbType.Int));

            foreach (PropertyDefinition property in table.Properties)
            {
                PrepareTableTypeColumns(in property, in columns);
            }

            return columns.ToArray();
        }
        private void PrepareTableTypeColumns(in PropertyDefinition property, in List<SqlMetaData> columns)
        {
            int ordinal = columns.Count;

            DataType input = property.Type;

            foreach (ColumnDefinition column in property.Columns)
            {
                if (column.IsGenerated)
                {
                    continue; // database auto-generated column
                }

                SqlDbType type = SqlDbType.Binary;
                Func<object, object> converter = null;

                if (column.Purpose == ColumnPurpose.Value)
                {
                    if (input.IsBoolean) { converter = MsDataMapper.ConvertBoolean; }
                    else if (input.IsDecimal) { converter = MsDataMapper.ConvertNumeric; type = SqlDbType.Decimal; }
                    else if (input.IsInteger) { converter = MsDataMapper.ConvertNumeric; type = SqlDbType.Decimal; }
                    else if (input.IsDateTime) { converter = _convertDateTime; type = SqlDbType.DateTime2; }
                    else if (input.IsString)
                    {
                        converter = MsDataMapper.ConvertString;

                        type = (column.Type.IsFixed) ? SqlDbType.NChar : SqlDbType.NVarChar;
                    }
                    else if (input.IsBinary) { converter = MsDataMapper.ConvertBinary; type = SqlDbType.VarBinary; }
                    else if (input.IsUuid) { converter = MsDataMapper.ConvertUuid; }
                    else if (input.IsEntity) { converter = MsDataMapper.ConvertIdentity; }
                }
                else if (column.Purpose == ColumnPurpose.Tag) { converter = MsDataMapper.ConvertTag; }
                else if (column.Purpose == ColumnPurpose.Boolean) { converter = MsDataMapper.ConvertBoolean; }
                else if (column.Purpose == ColumnPurpose.Numeric) { converter = MsDataMapper.ConvertNumeric; type = SqlDbType.Decimal; }
                else if (column.Purpose == ColumnPurpose.DateTime) { converter = _convertDateTime; type = type = SqlDbType.DateTime2; }
                else if (column.Purpose == ColumnPurpose.String)
                {
                    converter = MsDataMapper.ConvertString;

                    type = type = (column.Type.IsFixed) ? SqlDbType.NChar : SqlDbType.NVarChar;
                }
                else if (column.Purpose == ColumnPurpose.TypeCode) { converter = MsDataMapper.ConvertTypeCode; }
                else if (column.Purpose == ColumnPurpose.Identity) { converter = MsDataMapper.ConvertIdentity; }

                SqlMetaData metadata;

                if (type == SqlDbType.NChar)
                {
                    metadata = new SqlMetaData(column.Name, type, input.Size);
                }
                else if (type == SqlDbType.NVarChar)
                {
                    metadata = new SqlMetaData(column.Name, type, (input.Size == 0) ? -1 : input.Size);
                }
                else if (type == SqlDbType.Binary)
                {
                    metadata = new SqlMetaData(column.Name, type, input.Size);
                }
                else if (type == SqlDbType.VarBinary)
                {
                    metadata = new SqlMetaData(column.Name, type, -1);
                }
                else if (type == SqlDbType.Decimal)
                {
                    metadata = new SqlMetaData(column.Name, type, input.Precision, input.Scale);
                }
                else
                {
                    metadata = new SqlMetaData(column.Name, type);
                }

                columns.Add(metadata);

                _ordinals.Add(column, ordinal++);

                _converters.Add(column, converter);
            }
        }

        private object ConvertDateTimeWithOffset(object value)
        {
            return MsDataMapper.ConvertDateTime(value, _yearOffset);
        }
        public void WriteRowValues(in NpgsqlBinaryImporter importer)
        {
            DataObject record = null;

            importer.StartRow();
            importer.Write(_current, NpgsqlDbType.Integer); // order_column
            importer.Write("test", "mvarchar");

            int ordinal;
            object value;
            Func<object, object> converter;

            foreach (PropertyDefinition property in _table.Properties)
            {
                value = null; // default value

                if (_map.TryGetValue(property.Name, out ColumnExpression map))
                {
                    value = _context.Evaluate(map.Expression);
                }

                foreach (ColumnDefinition column in property.Columns)
                {
                    if (!_ordinals.TryGetValue(column, out ordinal))
                    {
                        continue; // database auto-generated column
                    }

                    converter = _converters[column];

                    value = converter(value);

                    //FIXME: column metadata is provided by DaJet as SQL Server data types
                    bool boolean = (column.Purpose == ColumnPurpose.Boolean) ||
                        (column.Purpose == ColumnPurpose.Value && property.Type.IsBoolean);

                    if (boolean)
                    {
                        importer.Write((bool)value, NpgsqlDbType.Boolean);
                    }
                    else
                    {
                        importer.Write("test", "mvarchar");
                    }
                }
            }
        }
        private void SetDataRecordValues(in SqlDataRecord record, int rowNumber)
        {
            record.SetInt32(0, rowNumber);

            int ordinal;
            object value;
            Func<object, object> converter;

            foreach (PropertyDefinition property in _table.Properties)
            {
                value = null; // default value

                if (_map.TryGetValue(property.Name, out ColumnExpression map))
                {
                    value = _context.Evaluate(map.Expression);
                }

                foreach (ColumnDefinition column in property.Columns)
                {
                    if (!_ordinals.TryGetValue(column, out ordinal))
                    {
                        continue; // database auto-generated column
                    }

                    converter = _converters[column];

                    value = converter(value);

                    record.SetValue(ordinal, value);
                }
            }
        }

        internal IDataReader GetDataReader(in List<DataObject> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

            _buffer = buffer; Reset(); return this;
        }
        public bool Read() { return MoveNext(); }
        public int FieldCount { get { return _record.FieldCount; } }
        public object GetValue(int ordinal) { return _record.GetValue(ordinal); }

        #region "Not implemented IDataReader interface"
        public int Depth => throw new NotImplementedException();
        public bool IsClosed => throw new NotImplementedException();
        public int RecordsAffected => throw new NotImplementedException();
        public object this[int ordinal] => throw new NotImplementedException();
        public object this[string name] => throw new NotImplementedException();
        public void Close() { throw new NotImplementedException(); }
        public bool NextResult() { throw new NotImplementedException(); }
        public bool GetBoolean(int i) { throw new NotImplementedException(); }
        public byte GetByte(int i) { throw new NotImplementedException(); }
        public char GetChar(int i) { throw new NotImplementedException(); }
        public string GetDataTypeName(int i) { throw new NotImplementedException(); }
        public DateTime GetDateTime(int i) { throw new NotImplementedException(); }
        public decimal GetDecimal(int i) { throw new NotImplementedException(); }
        public double GetDouble(int i) { throw new NotImplementedException(); }
        public float GetFloat(int i) { throw new NotImplementedException(); }
        public Guid GetGuid(int i) { throw new NotImplementedException(); }
        public short GetInt16(int i) { throw new NotImplementedException(); }
        public int GetInt32(int i) { throw new NotImplementedException(); }
        public long GetInt64(int i) { throw new NotImplementedException(); }
        public string GetName(int i) { throw new NotImplementedException(); }
        public int GetOrdinal(string name) { throw new NotImplementedException(); }
        public string GetString(int i) { throw new NotImplementedException(); }
        public int GetValues(object[] values) { throw new NotImplementedException(); }
        public bool IsDBNull(int i) { throw new NotImplementedException(); }
        public DataTable GetSchemaTable() { throw new NotImplementedException(); }
        public IDataReader GetData(int i) { throw new NotImplementedException(); }
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) { throw new NotImplementedException(); }
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) { throw new NotImplementedException(); }
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type GetFieldType(int i) { throw new NotImplementedException(); }
        #endregion
    }
}