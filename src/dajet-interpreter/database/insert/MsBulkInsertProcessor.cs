using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using System.Data;

namespace DaJet.Scripting
{
    public sealed class MsBulkInsertProcessor : ProcessorBase
    {
        private readonly ScriptContext _context;
        private readonly InsertStatement _statement;
        private readonly EntityDefinition _target;
        private readonly int _yearOffset;
        private readonly Func<object, object> _convertDateTime;
        private readonly string _bufferName;
        private readonly string _bufferItem;
        private readonly string _insertTable;
        private readonly bool _useSequence;
        private readonly string _vectorProperty;
        private readonly SqlDataRecord _record;
        private readonly SqlMetaData[] _columns;
        private readonly Dictionary<ColumnDefinition, int> _ordinals = new();
        private readonly Dictionary<ColumnDefinition, Func<object, object>> _converters = new();
        public MsBulkInsertProcessor(in ScriptContext context, in InsertStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope)
            {
                throw new InvalidOperationException();
            }

            _context = context;
            _statement = statement;
            _yearOffset = statement.YearOffset;

            if (_statement.Target is not TableReference table || table.Binding is not EntityDefinition target)
            {
                throw new InvalidOperationException();
            }

            if (_statement.Source is not VariableReference variable
                || variable.Binding is not DeclareStatement declare
                || !(declare.Type.IsArray && declare.Type.IsObject))
            {
                throw new InvalidOperationException();
            }

            _target = target;

            _bufferName = variable.Identifier;
            _bufferItem = string.Format("{0}{1}", _bufferName, "_Item");

            _context.CreateVariable(in _bufferItem);

            foreach (ColumnExpression map in _statement.Values)
            {
                if (map.Expression is MemberAccessExpression member && member.GetVariableName() == _bufferName)
                {
                    member.Identifier = member.Identifier.Replace(_bufferName, _bufferItem);
                }
            }

            _convertDateTime = ConvertDateTimeWithOffset;
            
            _useSequence = MsBulkInsertTranspiler.GetSequenceProperty(in _statement, out _vectorProperty, out string sequenceName);

            _insertTable = MsBulkInsertTranspiler.BulkInsertStatement(in _target, in _vectorProperty, in sequenceName);

            _columns = PrepareTableTypeMetadata();

            _record = new SqlDataRecord(_columns);
        }
        private object ConvertDateTimeWithOffset(object value)
        {
            return MsDataMapper.ConvertDateTime(value, _yearOffset);
        }

        private SqlMetaData[] PrepareTableTypeMetadata()
        {
            List<SqlMetaData> columns = new();

            columns.Add(new SqlMetaData("order_column", SqlDbType.Int));
            
            foreach (PropertyDefinition property in _target.Properties)
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

        public override ExitCode Process()
        {
            if (_context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            if (_context.GetValue(in _bufferName) is not List<DataObject> buffer)
            {
                throw new InvalidOperationException();
            }

            if (buffer.Count == 0)
            {
                return ExitCode.Success;
            }

            int timeout = _statement.Timeout;

            using (SqlCommand command = use.CreateCommand())
            {
                command.CommandText = _insertTable;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = timeout; // seconds
                SqlParameter tvp = new()
                {
                    SqlDbType = SqlDbType.Structured,
                    TypeName = MsBulkInsertTranspiler.GetTableTypeName(in _target),
                    ParameterName = MsBulkInsertTranspiler.TableVariableName,
                    Value = Stream(buffer)
                };
                command.Parameters.Add(tvp);

                _nextRecord = 0;

                while (_nextRecord < buffer.Count)
                {
                    command.ExecuteNonQuery();
                }
            }

            _context.SetValue(in _bufferItem, null);

            for (int i = 0; i < _record.FieldCount; i++)
            {
                _record.SetValue(i, null);
            }

            return ExitCode.Success;
        }

        private int _nextRecord;
        private IEnumerable<SqlDataRecord> Stream(List<DataObject> buffer)
        {
            int batchSize = _statement.BatchSize;

            while (_nextRecord < buffer.Count)
            {
                DataObject source = buffer[_nextRecord];

                _context.SetValue(in _bufferItem, source);

                SetDataRecordValues(in _record);

                _nextRecord++;
                
                yield return _record;

                if (_nextRecord % batchSize == 0)
                {
                    break;
                }
            }
        }
        private void SetDataRecordValues(in SqlDataRecord record)
        {
            record.SetInt32(0, _nextRecord);

            int ordinal;
            object value;
            Func<object, object> converter;

            foreach (PropertyDefinition property in _target.Properties)
            {
                value = null; // default value

                if (_statement.TryGetMapping(property.Name, out ColumnExpression map))
                {
                    if (property.Name != _vectorProperty)
                    {
                        value = _context.Evaluate(map.Expression);
                    }
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
        public override void Dispose()
        {
            // do nothing
        }
    }
}