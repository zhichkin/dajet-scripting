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
        private readonly Dictionary<string, object> _data;

        private readonly int _yearOffset;
        private readonly string _commandText;
        private readonly List<SyntaxNode> _input;
        private readonly bool _outputIsObject;
        private readonly string _outputVariable;
        private readonly EntityDefinition _outputSchema;
        private readonly ExpressionInterpreter _expression;
        public MsSelectProcessor(in Stack<DataSourceScope> sources, in SqlStatement statement, in ExpressionInterpreter expression, in Dictionary<string, object> data)
        {
            if (statement.Node is not SelectStatement select)
            {
                throw new InvalidOperationException();
            }

            if (sources.Peek() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _data = data;
            _dataSource = use;
            _expression = expression;
            _input = statement.Input;
            _yearOffset = statement.YearOffset;
            _commandText = statement.Sql;
            _outputSchema = DataMapper.InferEntity(in select);

            if (select.GetIntoClause() is IntoClause into)
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

                if (_data.ContainsKey(_outputVariable))
                {
                    _data[_outputVariable] = value;
                }
                else
                {
                    _data.Add(_outputVariable, value);
                }
            }
        }

        private void ProcessInput(in SqlCommand command)
        {
            command.Parameters.Clear();

            foreach (SyntaxNode input in _input)
            {
                string name = string.Format("@p{0}", _input.IndexOf(input));

                object value = _expression.Evaluate(in input);

                if (value is null)
                {
                    command.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (value is bool boolean)
                {
                    command.Parameters.AddWithValue(name, boolean ? TRUE : FALSE);
                }
                else if (value is int integer)
                {
                    command.Parameters.AddWithValue(name, DbUtilities.GetByteArray(integer));
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
            int ordinal = 0;
            int columns = 0;

            foreach (PropertyDefinition property in _outputSchema.Properties)
            {
                columns = property.Columns.Count;

                DataType type = property.Type;

                if (type.IsUnion) { record.Add(property.Name, GetUnion(in reader, ordinal, in property, _yearOffset)); }
                else if (type.IsBoolean) { record.Add(property.Name, GetBoolean(in reader, ordinal)); }
                else if (type.IsDecimal) { record.Add(property.Name, GetDecimal(in reader, ordinal)); }
                else if (type.IsDateTime) { record.Add(property.Name, GetDateTime(in reader, ordinal, _yearOffset)); }
                else if (type.IsString) { record.Add(property.Name, GetString(in reader, ordinal)); }
                else if (type.IsBinary) { record.Add(property.Name, GetBinary(in reader, ordinal)); }
                else if (type.IsUuid) { record.Add(property.Name, GetUuid(in reader, ordinal)); }
                else if (type.IsEntity)
                {
                    record.Add(property.Name, GetEntity(in reader, ordinal, type.TypeCode));
                }
                else if (type.IsInteger)
                {
                    if (type.Size == 4)
                    {
                        record.Add(property.Name, GetInt32(in reader, ordinal));
                    }
                    else
                    {
                        record.Add(property.Name, GetInt64(in reader, ordinal));
                    }
                }
                else
                {
                    record.Add(property.Name, null);
                }

                ordinal += columns;
            }
        }
        private static bool GetBoolean(in SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return false;
            }

            bool value = (((byte[])reader.GetValue(ordinal))[0] == 1);

            return value;

            //TODO:
            //if (column.Name == "_Folder" || column.Name == "_folder")
            //{
            //    return !value; // invert - exceptional 1C case
            //}
            //else
            //{
            //    return value;
            //}
        }
        private static decimal GetDecimal(in SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? 0M : reader.GetDecimal(ordinal);
        }
        private static int GetInt32(in SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }
        private static long GetInt64(in SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? 0L : reader.GetInt64(ordinal);
        }
        private static DateTime GetDateTime(in SqlDataReader reader, int ordinal, int yearOffset)
        {
            return reader.IsDBNull(0) ? DateTime.MinValue : reader.GetDateTime(ordinal).AddYears(-yearOffset);
        }
        private static string GetString(in SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }
        private static byte[] GetBinary(in SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? Array.Empty<byte>() : (byte[])reader.GetValue(ordinal);
        }
        private static Guid GetUuid(in SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return Guid.Empty;
            }

            byte[] value = new byte[16];

            _ = reader.GetBytes(ordinal, 0L, value, 0, 16);

            return new Guid(value);
        }
        private static Entity GetEntity(in SqlDataReader reader, int ordinal, int typeCode)
        {
            if (reader.IsDBNull(ordinal))
            {
                return Entity.Undefined;
            }

            byte[] value = new byte[16];

            _ = reader.GetBytes(ordinal, 0L, value, 0, 16);

            Guid identity = new(value);

            return new Entity(typeCode, identity);
        }
        private static object GetUnion(in SqlDataReader reader, int ordinal, in PropertyDefinition property, int yearOffset)
        {
            int typeCode = 0;

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Tag);

            if (column is null) // IsReferenceOnlyUnion
            {
                if (reader.IsDBNull(ordinal))
                {
                    return Entity.Undefined;
                }

                column = property.GetColumnByPurpose(ColumnPurpose.TypeCode);

                if (column is null)
                {
                    return GetEntity(in reader, ordinal, property.Type.TypeCode);
                }

                byte[] buffer = new byte[4];

                _ = reader.GetBytes(ordinal, 0L, buffer, 0, 4);
                
                typeCode = BinaryPrimitives.ReadInt32BigEndian(buffer);

                return GetEntity(in reader, ++ordinal, typeCode);
            }

            // _TYPE binary(1) - may be generated by query engine if value is not stored in the database
            // TAG value is generated by query engine in case data type addition operation takes place !
            if (reader.IsDBNull(ordinal))
            {
                return Union.Undefined;
            }

            byte tag = ((byte[])reader.GetValue(ordinal))[0];

            if (tag == 1) // Неопределено
            {
                return Union.Undefined;
            }
            else if (tag == 2) // Булево
            {
                ordinal += GetOrdinal(in property, ColumnPurpose.Boolean);
                return new Union.CaseBoolean(GetBoolean(in reader, ordinal));
            }
            else if (tag == 3) // Число
            {
                ordinal += GetOrdinal(in property, ColumnPurpose.Numeric);
                return new Union.CaseDecimal(GetDecimal(in reader, ordinal));
            }
            else if (tag == 4) // Дата
            {
                ordinal += GetOrdinal(in property, ColumnPurpose.DateTime);
                return new Union.CaseDateTime(GetDateTime(in reader, ordinal, yearOffset));
            }
            else if (tag == 5) // Строка
            {
                ordinal += GetOrdinal(in property, ColumnPurpose.String);
                return new Union.CaseString(GetString(in reader, ordinal));
            }
            else if (tag == 8) // Ссылка
            {
                column = property.GetColumnByPurpose(ColumnPurpose.TypeCode);

                if (column is null)
                {
                    ordinal += GetOrdinal(in property, ColumnPurpose.Identity);
                    return new Union.CaseEntity(GetEntity(in reader, ordinal, property.Type.TypeCode));
                }

                ordinal += GetOrdinal(in property, ColumnPurpose.TypeCode);

                byte[] buffer = new byte[4];
                _ = reader.GetBytes(ordinal, 0L, buffer, 0, 4);
                typeCode = BinaryPrimitives.ReadInt32BigEndian(buffer);

                return new Union.CaseEntity(GetEntity(in reader, ++ordinal, typeCode));
            }
            
            throw new InvalidOperationException($"Invalid union tag value: [{tag}]");
        }

        private static int GetOrdinal(in PropertyDefinition property, in ColumnPurpose purpose)
        {
            List<ColumnDefinition> columns = property.Columns;

            if (columns is null || columns.Count == 0)
            {
                return -1;
            }

            for (int ordinal = 0; ordinal < columns.Count; ordinal++)
            {
                ColumnDefinition columnDefinition = columns[ordinal];

                if (columnDefinition.Purpose == purpose)
                {
                    return ordinal;
                }
            }

            return -1;
        }
    }
}