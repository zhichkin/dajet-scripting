using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Data
{
    public sealed class MsQueryProcessor
    {
        private readonly static byte[] TRUE = [0x01];
        private readonly static byte[] FALSE = [0x00];
        private readonly string _connectionString;
        private readonly SqlStatement _statement;
        private readonly int _yearOffset;
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

                    SetParameters(in command);

                    using (SqlDataReader reader = command.ExecuteReader())
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
        private void SetParameters(in SqlCommand command) // input
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
        private void MapDataToObject(in SqlDataReader reader, in DataObject record) // output
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
    }
}