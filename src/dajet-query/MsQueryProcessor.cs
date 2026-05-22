using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Buffers;
using System.Data;
using System.Data.Common;
using System.Text;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;

namespace DaJet.Data
{
    public sealed class MsQueryProcessor
    {
        private readonly static byte[] TRUE = [0x01];
        private readonly static byte[] FALSE = [0x00];
        private readonly int _yearOffset;
        private readonly string _connectionString;
        private readonly SqlStatement _statement;
        private readonly EntityDefinition _schema;
        public MsQueryProcessor(in string connectionString, in SqlStatement statement)
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
            
            using (SqlConnection connection = new(_connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = _statement.Sql;

                    ProcessInput(in command);

                    using (SqlDataReader reader = command.ExecuteReader())
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
        
        private void ProcessInput(in SqlCommand command)
        {
            command.Parameters.Clear();

            foreach (SyntaxNode input in _statement.Input)
            {
                string name = string.Format("@p{0}", _statement.Input.IndexOf(input));

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
                                command.Parameters.AddWithValue(name, literal == "TRUE" ? TRUE : FALSE);
                            }
                            else if (type.IsDecimal)
                            {
                                command.Parameters.AddWithValue(name, decimal.Parse(literal));
                            }
                            else if (type.IsDateTime)
                            {
                                DateTime value = DateTime.Parse(literal).AddYears(_yearOffset);

                                command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.DateTime2;
                            }
                            else if (type.IsString)
                            {
                                command.Parameters.AddWithValue(name, literal);
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
        public void ConfigureParameters(in SqlCommand command, in Dictionary<string, object> parameters)
        {
            command.Parameters.Clear();

            foreach (var parameter in parameters)
            {
                string name = parameter.Key.StartsWith('@') ? parameter.Key[1..] : parameter.Key;

                if (parameter.Value is null)
                {
                    command.Parameters.AddWithValue(name, DBNull.Value);
                }
                else if (parameter.Value is bool boolean)
                {
                    command.Parameters.AddWithValue(name, boolean ? TRUE : FALSE);
                }
                else if (parameter.Value is int integer)
                {
                    command.Parameters.AddWithValue(name, DbUtilities.GetByteArray(integer));
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    dateTime = dateTime.AddYears(_yearOffset);
                    command.Parameters.AddWithValue(name, dateTime).SqlDbType = SqlDbType.DateTime2;
                }
                else if (parameter.Value is Guid uuid)
                {
                    command.Parameters.AddWithValue(name, uuid.ToByteArray());
                }
                else if (parameter.Value is Entity entity)
                {
                    command.Parameters.AddWithValue(name, entity.Identity.ToByteArray());
                }
                else // decimal, string, byte[]
                {
                    command.Parameters.AddWithValue(name, parameter.Value);
                }
            }
        }

        private void ProcessOutput(in SqlDataReader reader, in DataObject record)
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
                    record.SetValue(property.Name, reader.GetString(ordinal));
                }

                ordinal += property.Columns.Count;
            }
        }
        public object GetValue(in SqlDataReader reader)
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
        private object GetSingleValue(in SqlDataReader reader)
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
        private object GetMultipleValue(in SqlDataReader reader)
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
        private object GetBoolean(in IDataReader reader)
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
        private object GetDecimal(in IDataReader reader)
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
        private object GetDateTime(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.DateTime, out _);

            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetDateTime(ordinal).AddYears(-_yearOffset);
        }
        private object GetString(in IDataReader reader)
        {
            int ordinal = GetOrdinal(in reader, UnionTag.String, out _);

            if (reader.IsDBNull(ordinal)) { return null; }

            if (reader is not NpgsqlDataReader postgres)
            {
                return reader.GetString(ordinal);
            }

            string typeName = postgres.GetPostgresType(ordinal).Name;

            if (typeName == "mchar" || typeName == "mvarchar")
            {

                int size = 1024;
                long length;
                long offset = 0;
                string text = string.Empty;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(size);

                do
                {
                    length = postgres.GetBytes(ordinal, offset, buffer, 0, size);

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
            else
            {
                return postgres.GetString(ordinal);
            }
        }
        private object GetBinary(in IDataReader reader)
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
        private object GetUuid(in IDataReader reader)
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
        private object GetEntity(in IDataReader reader)
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
        private object GetInt32(in IDataReader reader)
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
        private object GetInt64(in IDataReader reader)
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
    }
}