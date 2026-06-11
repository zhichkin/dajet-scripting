using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;
using System.Data;

namespace DaJet.Scripting
{
    public sealed class MsSelectProcessor : ProcessorBase
    {
        private readonly static byte[] TRUE = [0x01];
        private readonly static byte[] FALSE = [0x00];

        private readonly MsDataSourceScope _dataSource;
        private readonly ExpressionInterpreter _context;

        private readonly byte[] _buffer = new byte[16];
        private readonly int _yearOffset;
        private readonly string _commandText;
        private readonly List<SyntaxNode> _input;
        private readonly bool _outputIsObject;
        private readonly string _outputVariable;
        private readonly EntityDefinition _outputSchema;
        private readonly Dictionary<ColumnDefinition, int> _ordinals = new();
        public MsSelectProcessor(in SelectStatement statement, in Stack<DataSourceScope> sources, in ExpressionInterpreter context)
        {
            if (sources.Peek() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _context = context;
            _input = statement.Input;
            _yearOffset = statement.YearOffset;
            _commandText = statement.Sql;
            _outputSchema = statement.InferEntity();

            PrepareOutputColumnOrdinals();

            if (statement.GetIntoClause() is IntoClause into)
            {
                _outputVariable = into.Value?.Identifier;

                if (into.Value is VariableReference variable)
                {
                    if (variable.Binding is DeclareStatement declare)
                    {
                        if (declare.Type.IsObject)
                        {
                            _outputIsObject = true;
                        }
                        else if (declare.Type.IsArray)
                        {
                            _outputIsObject = false;
                        }
                        else
                        {
                            //TODO: scalar values
                        }
                    }
                }
            }
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
        public override void Process()
        {
            List<Dictionary<string, object>> table = new();

            int outputCount = _outputSchema.Properties.Count;

            using (SqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _commandText;

                ProcessInput(in command);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Dictionary<string, object> record = new(outputCount);

                        ProcessOutput(in reader, in record);

                        table.Add(record);
                    }

                    reader.Close();
                }
            }

            SetOutputValue(in table);
        }
        private void SetOutputValue(in List<Dictionary<string, object>> table)
        {
            if (_outputVariable is not null)
            {
                object value;

                if (_outputIsObject)
                {
                    value = table.Count > 0 ? table[0] : null;
                }
                else
                {
                    value = table;
                }

                if (!_context.Data.TryAdd(_outputVariable, value))
                {
                    _context.Data[_outputVariable] = value;
                }
            }
        }

        private void ProcessInput(in SqlCommand command)
        {
            command.Parameters.Clear();

            foreach (SyntaxNode input in _input)
            {
                string name = string.Format("@p{0}", _input.IndexOf(input));

                object value = _context.Evaluate(in input);

                if (value is null)
                {
                    command.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (value is bool boolean)
                {
                    command.Parameters.AddWithValue(name, boolean ? TRUE : FALSE);
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
                else if (value is DateTime dateTime)
                {
                    dateTime = dateTime.AddYears(_yearOffset);
                    command.Parameters.AddWithValue(name, dateTime).SqlDbType = SqlDbType.DateTime2;
                }
                else if (value is Guid uuid)
                {
                    command.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else if (value is Entity entity)
                {
                    command.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                }
                else // decimal, string, byte[]
                {
                    command.Parameters.AddWithValue(name, value);
                }
            }
        }

        private void ProcessOutput(in SqlDataReader reader, in Dictionary<string, object> record)
        {
            foreach (PropertyDefinition property in _outputSchema.Properties)
            {
                DataType type = property.Type;

                if (type.IsUnion) { record.Add(property.Name, GetUnion(in reader, in property)); }
                else if (type.IsBoolean) { record.Add(property.Name, GetBoolean(in reader, in property)); }
                else if (type.IsDecimal) { record.Add(property.Name, GetDecimal(in reader, in property)); }
                else if (type.IsDateTime) { record.Add(property.Name, GetDateTime(in reader, in property)); }
                else if (type.IsString) { record.Add(property.Name, GetString(in reader, in property)); }
                else if (type.IsBinary) { record.Add(property.Name, GetBinary(in reader, in property)); }
                else if (type.IsUuid) { record.Add(property.Name, GetUuid(in reader, in property)); }
                else if (type.IsEntity)
                {
                    record.Add(property.Name, GetEntity(in reader, in property));
                }
                else if (type.IsInteger)
                {
                    if (type.Size == 4)
                    {
                        record.Add(property.Name, GetInt32(in reader, in property));
                    }
                    else
                    {
                        record.Add(property.Name, GetInt64(in reader, in property));
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