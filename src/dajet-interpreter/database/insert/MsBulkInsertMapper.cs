using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient.Server;
using System.Collections;
using System.Data;

namespace DaJet.Scripting
{
    public sealed class MsBulkInsertMapper : IEnumerator<SqlDataRecord>
    {
        private int _current;
        private bool _locked;
        private int _batchSize;

        private List<DataObject> _buffer;
        private readonly SqlDataRecord _record;
        private readonly ScriptContext _context;
        private readonly int _yearOffset;
        private readonly Func<object, object> _convertDateTime;
        private readonly Dictionary<string, ColumnExpression> _map = new();
        private readonly Dictionary<ColumnDefinition, int> _ordinals = new();
        private readonly Dictionary<ColumnDefinition, Func<object, object>> _converters = new();
        public MsBulkInsertMapper(in ScriptContext context, in InsertStatement statement, int batchSize)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentNullException.ThrowIfNull(statement, nameof(statement));

            _context = context;

            _batchSize = batchSize > 0 ? batchSize : 100;

            _yearOffset = statement.YearOffset;

            _convertDateTime = ConvertDateTimeWithOffset;
            
            if (statement.Target is not TableReference target || target.Binding is not EntityDefinition table)
            {
                throw new InvalidOperationException();
            }

            foreach (ColumnExpression map in statement.Values)
            {
                if (map.Expression is FunctionExpression function && function.Token == Token.VECTOR)
                {
                    continue; // column value is set by SEQUENCE
                }

                _map.Add(map.Alias, map);
            }

            if (statement.Source is not VariableReference variable
                || variable.Binding is not DeclareStatement declare
                || !(declare.Type.IsArray && declare.Type.IsObject))
            {
                throw new InvalidOperationException();
            }

            //_bufferName = variable.Identifier;
            //_bufferItem = string.Format("{0}{1}", _bufferName, "_Item");

            //_context.CreateVariable(in _bufferItem);

            //foreach (ColumnExpression map in _statement.Values)
            //{
            //    if (map.Expression is MemberAccessExpression member && member.GetVariableName() == _bufferName)
            //    {
            //        member.Identifier = member.Identifier.Replace(_bufferName, _bufferItem);
            //    }
            //}

            SqlMetaData[] columns = PrepareTableTypeMetadata(in table);

            _record = new SqlDataRecord(columns);
        }
        object IEnumerator.Current { get { return Current; } }
        public IEnumerator<SqlDataRecord> Enumerate(in List<DataObject> buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));

            _buffer = buffer; Reset(); return this;
        }
        public void Reset() { _current = 0; _locked = false; }
        public SqlDataRecord Current
        {
            get
            {
                DataObject current;

                if (_current < _buffer.Count)
                {
                    current = _buffer[_current];
                }
                else
                {
                    return null;
                }

                //TODO: convert object to record

                _context.SetValue(in _bufferItem, current);

                SetDataRecordValues(in _record);
                
                return _record;
            }
        }
        public bool HasRecords()
        {
            return (_current < _buffer.Count);
        }
        public bool MoveNext()
        {
            if (!HasRecords())
            {
                return false;
            }

            if (_current == 0)
            {
                return true;
            }

            if (_locked)
            {
                _current++; _locked = false; return true;
            }

            int next = _current + 1;

            if (!(next < _buffer.Count))
            {
                _current++; return false;
            }

            if (next % _batchSize == 0)
            {
                _locked = true; return false;
            }
            
            _current++;
            
            return true;
        }
        public void Dispose()
        {
            _buffer = null;

            if (_record is not null)
            {
                for (int i = 0; i < _record.FieldCount; i++)
                {
                    _record.SetValue(i, null);
                }
            }
        }

        private object ConvertDateTimeWithOffset(object value)
        {
            return MsDataMapper.ConvertDateTime(value, _yearOffset);
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

        private void SetDataRecordValues(in SqlDataRecord record)
        {
            record.SetInt32(0, _nextRecord);

            int ordinal;
            object value;
            Func<object, object> converter;

            foreach (PropertyDefinition property in table.Properties)
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
    }
}