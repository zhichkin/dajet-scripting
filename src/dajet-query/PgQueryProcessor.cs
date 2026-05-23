using DaJet.Data.PostgreSql;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Npgsql;
using NpgsqlTypes;
using System.Buffers;
using System.Data.Common;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgQueryProcessor
    {
        private readonly int _yearOffset;
        private readonly string _connectionString;
        private readonly SqlStatement _statement;
        private readonly EntityDefinition _schema;
        public PgQueryProcessor(in string connectionString, in SqlStatement statement)
        {
            _statement = statement;
            _yearOffset = statement.YearOffset;
            _connectionString = connectionString;

            if (statement.Node is not SelectStatement select)
            {
                throw new InvalidOperationException();
            }

            _schema = DataMapper.InferEntity(in select);
        }
        public List<DataObject> Execute()
        {
            List<DataObject> table = new();

            using (NpgsqlConnection connection = PgDataSourceFactory.CreateConnection(_connectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = _statement.Sql;

                    ProcessInput(in command);

                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DataObject record = new(_schema.Properties.Count);

                            ProcessOutput(in reader, in record);

                            table.Add(record);
                        }

                        reader.Close();
                    }
                }
            }

            return table;
        }
        
        private void ProcessInput(in NpgsqlCommand command)
        {
            command.Parameters.Clear();

            foreach (SyntaxNode input in _statement.Input)
            {
                string name = string.Format("${0}", _statement.Input.IndexOf(input) + 1);

                if (input is VariableReference variable)
                {
                    if (variable.Binding is DeclareStatement declare)
                    {
                        DataType type = declare.Type;

                        if (declare.Initializer is ScalarExpression scalar)
                        {
                            string literal = scalar.Literal;

                            if (type.IsBoolean)
                            {
                                command.Parameters.AddWithValue(name, literal == "TRUE");
                            }
                            else if (type.IsDecimal)
                            {
                                command.Parameters.AddWithValue(name, decimal.Parse(literal));
                            }
                            else if (type.IsDateTime)
                            {
                                command.Parameters.AddWithValue(name, DateTime.Parse(literal).AddYears(_yearOffset));
                            }
                            else if (type.IsString)
                            {
                                command.Parameters.Add(new NpgsqlParameter<string>()
                                {
                                    TypedValue = literal,
                                    NpgsqlDbType = NpgsqlDbType.Varchar
                                });
                            }
                            else if (type.IsUuid)
                            {
                                command.Parameters.AddWithValue(name, new Guid(literal));
                            }
                        }
                    }
                }
            }
        }
        public void ConfigureParameters(in DbCommand command, in Dictionary<string, object> parameters, int yearOffset)
        {
            if (command is not NpgsqlCommand cmd)
            {
                throw new InvalidOperationException($"{nameof(command)} is not type of {typeof(NpgsqlCommand)}");
            }

            cmd.Parameters.Clear();

            foreach (var parameter in parameters)
            {
                string name = parameter.Key.StartsWith('@') ? parameter.Key[1..] : parameter.Key;

                if (parameter.Value is null)
                {
                    cmd.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (parameter.Value is Entity entity)
                {
                    cmd.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    cmd.Parameters.AddWithValue(name, dateTime.AddYears(yearOffset));
                }
                else if (parameter.Value is Guid uuid)
                {
                    cmd.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else // bool, int, decimal, string, byte[]
                {
                    cmd.Parameters.AddWithValue(name, parameter.Value);
                }

                //TODO: user-defined type - table-valued parameter
                //else if (parameter.Value is List<DataObject> table)
                //{
                //    DeclareStatement declare = GetDeclareStatementByName(in model, parameter.Key);

                //    parameters[parameter.Key] = new TableValuedParameter()
                //    {
                //        Name = parameter.Key,
                //        Value = table,
                //        DbName = declare is null ? string.Empty : declare.Type.Identifier
                //    };
                //}

                //else if (parameter.Value is List<Dictionary<string, object>> table)
                //{
                //    DeclareStatement declare = GetDeclareStatementByName(in model, parameter.Key);

                //    parameters[parameter.Key] = new TableValuedParameter()
                //    {
                //        Name = parameter.Key,
                //        Value = table,
                //        DbName = declare is null ? string.Empty : declare.Type.Identifier
                //    };
                //}
            }
        }

        private void ProcessOutput(in NpgsqlDataReader reader, in DataObject record)
        {
            int ordinal = 0;

            foreach (PropertyDefinition property in _schema.Properties)
            {
                DataType type = property.Type;

                if (type.IsUnion)
                {

                }
                else if (type.IsReferenceOnlyUnion)
                {

                }
                else if (type.IsBoolean)
                {

                }
                else if (type.IsDecimal)
                {

                }
                else if (type.IsDateTime)
                {

                }
                else if (type.IsString)
                {
                    record.SetValue(property.Name, GetString(in reader, ordinal));
                }

                ordinal += property.Columns.Count;
            }
        }
        public static object GetValue(in NpgsqlDataReader reader)
        {
            if (Columns.Count == 0)
            {
                return null;
            }
            else if (Columns.Count == 1)
            {
                return GetSingleValue(in reader);
            }
            else
            {
                return GetMultipleValue(in reader);
            }
        }
        private static object GetSingleValue(in NpgsqlDataReader reader)
        {
            if (DataType.IsBoolean) { return GetBoolean(in reader); }
            else if (DataType.IsNumeric) { return GetDecimal(in reader); }
            else if (DataType.IsDateTime) { return GetDateTime(in reader); }
            else if (DataType.IsString) { return GetString(in reader); }
            else if (DataType.IsBinary) { return GetBinary(in reader); }
            else if (DataType.IsUuid) { return GetUuid(in reader); }
            else if (DataType.IsEntity) { return GetEntity(in reader); }
            else if (DataType.IsVersion) { return GetVersion(in reader); }
            else if (DataType.IsInteger) { return GetInteger(in reader); }

            throw new NotSupportedException($"Unsupported: {DataType}");
        }
        private static object GetMultipleValue(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Tag, out _);

            if (ordinal == -1) // Union value without _TYPE discriminator field
            {
                return GetEntity(in reader);
            }

            if (reader.IsDBNull(ordinal))
            {
                return Union.Undefined;
            }

            // _TYPE binary(1) - may be generated by query engine if value is not stored in the database
            // TAG value is generated by query engine in case data type addition operation takes place !
            byte tag = ((byte[])reader.GetValue(ordinal))[0];

            object value;

            if (tag == 1) // Неопределено
            {
                return Union.Undefined;
            }
            else if (tag == 2) // Булево
            {
                value = GetBoolean(in reader);
                return (value == null ? Union.Undefined : new Union.CaseBoolean((bool)value));
            }
            else if (tag == 3) // Число
            {
                value = GetDecimal(in reader);
                return (value == null ? Union.Undefined : new Union.CaseDecimal((decimal)value));
            }
            else if (tag == 4) // Дата
            {
                value = GetDateTime(in reader);
                return (value == null ? Union.Undefined : new Union.CaseDateTime((DateTime)value));
            }
            else if (tag == 5) // Строка
            {
                value = GetString(in reader);
                return (value == null ? Union.Undefined : new Union.CaseString((string)value));
            }
            else if (tag == 8) // Ссылка
            {
                value = GetEntity(in reader);
                return (value == null ? Union.Undefined : new Union.CaseEntity((Entity)value));
            }

            throw new InvalidOperationException($"Invalid union tag value: [{tag}]");
        }
        private static object GetBoolean(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Boolean, out ColumnMapper column);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            bool value;

            if (reader.GetFieldType(ordinal) == typeof(bool))
            {
                value = reader.GetBoolean(ordinal); // PostgreSql
            }
            else
            {
                value = (((byte[])reader.GetValue(ordinal))[0] == 1); // SqlServer
            }

            //TODO: убрать этот костыль в класс DataMapper _Folder
            if (column.Name == "_Folder" || column.Name == "_folder")
            {
                return !value; // invert - exceptional 1C case
            }
            else
            {
                return value;
            }
        }
        private static object GetDecimal(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Numeric, out ColumnMapper column);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            Type type = reader.GetFieldType(ordinal);

            if (type == typeof(int))
            {
                return new decimal(reader.GetInt32(ordinal));
            }
            else if (type == typeof(long))
            {
                return new decimal(reader.GetInt64(ordinal));
            }
            else if (type == typeof(byte))
            {
                return new decimal(reader.GetByte(ordinal));
            }
            else if (type == typeof(byte[])) // binary(4) TRef
            {
                byte[] value = (byte[])reader.GetValue(ordinal);
                return Convert.ToDecimal(DbUtilities.GetInt32(value));
            }
            else
            {
                return reader.GetDecimal(ordinal);
            }
        }
        private static object GetInt32(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Integer, out _);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            Type type = reader.GetFieldType(ordinal);

            if (type == typeof(byte[]))
            {
                //TODO: cast binary(4) to int at database level ?
                //NOTE: the value can be stored as unsigned big-endian !!!
                return DbUtilities.GetInt32((byte[])reader.GetValue(ordinal));
            }
            else if (type == typeof(long))
            {
                return reader.GetInt64(ordinal);
            }
            else
            {
                return reader.GetInt32(ordinal);
            }
        }
        private static object GetInt64(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Version, out _);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            if (reader.GetFieldType(ordinal) == typeof(byte[]))
            {
                //TODO: cast binary(8) to bigint at database level ?
                //NOTE: SQL Server rowversion is unsigned big-endian value !!!
                return DbUtilities.GetInt64((byte[])reader.GetValue(ordinal));
            }
            else
            {
                return reader.GetInt64(ordinal);
            }
        }
        private static object GetDateTime(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.DateTime, out _);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetDateTime(ordinal).AddYears(-_yearOffset);
        }
        private static string GetString(in NpgsqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) { return string.Empty; }

            string typeName = reader.GetPostgresType(ordinal).Name;

            if (typeName == "mchar" || typeName == "mvarchar")
            {

                int size = 1024;
                long length;
                long offset = 0;
                string text = string.Empty;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

                do
                {
                    length = reader.GetBytes(ordinal, offset, buffer, 0, size);

                    offset += length;

                    if (length > 0)
                    {
                        text += Encoding.Unicode.GetString(buffer, 0, (int)length);
                    }
                }
                while (length > 0);

                ArrayPool<byte>.Shared.Return(buffer);

                return text;
            }
            
            return reader.GetString(ordinal);
        }
        private static object GetBinary(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Binary, out _);

            if (reader.IsDBNull(ordinal)) { return null; }

            if (reader is not NpgsqlDataReader postgres)
            {
                return ((byte[])reader.GetValue(ordinal));
            }

            string typeName = postgres.GetPostgresType(ordinal).Name;

            if (typeName == "integer") //TODO: поле _version в PostgreSQL
            {
                return BitConverter.GetBytes(reader.GetInt32(ordinal));
            }
            else
            {
                return ((byte[])reader.GetValue(ordinal));
            }
        }
        private static object GetUuid(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Uuid, out _);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            Type type = reader.GetFieldType(ordinal);

            if (type == typeof(byte[]))
            {
                byte[] buffer = new byte[16];

                _ = reader.GetBytes(ordinal, 0, buffer, 0, 16);

                return new Guid(buffer);
            }
            else if (type == typeof(Guid))
            {
                return reader.GetGuid(ordinal);
            }

            throw new InvalidOperationException("Invalid UUID value");
        }
        private static object GetEntity(in NpgsqlDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.Entity, out _);

            if (ordinal == -1)
            {
                throw new InvalidOperationException("Entity column mapping is not found");
            }

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            Guid identity = new((byte[])reader.GetValue(ordinal)); // binary(16)

            if (Columns.Count == 1) // single reference type value - RRef
            {
                return new Entity(DataType.TypeCode, identity);
            }

            ordinal = GetOrdinal(in reader, UnionTag.TypeCode, out _);

            if (ordinal == -1) // union having single reference type
            {
                return new Entity(DataType.TypeCode, identity);
            }

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            int typeCode = DbUtilities.GetInt32((byte[])reader.GetValue(ordinal)); // binary(4)

            return new Entity(typeCode, identity);
        }
    }
}