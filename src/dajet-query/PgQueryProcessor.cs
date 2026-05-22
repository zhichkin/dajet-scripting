using DaJet.Data.PostgreSql;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Npgsql;
using NpgsqlTypes;
using System.Buffers;
using System.Text;

namespace DaJet.Data
{
    public sealed class PgQueryProcessor
    {
        private readonly string _connectionString;
        private readonly SqlStatement _statement;
        private readonly int _yearOffset;
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

                    SetParameters(in command);

                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DataObject record = new(_schema.Properties.Count);

                            MapDataToObject(in reader, in record);

                            table.Add(record);
                        }

                        reader.Close();
                    }
                }
            }

            return table;
        }
        private void SetParameters(in NpgsqlCommand command) // input
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
        private void MapDataToObject(in NpgsqlDataReader reader, in DataObject record) // output
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
    }
}