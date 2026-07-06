using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Npgsql;
using System.Collections;

namespace DaJet.Scripting
{
    public sealed class PgSelectProcessor : ProcessorBase
    {
        private readonly PgDataMapper _mapper;
        private readonly ScriptContext _context;
        private readonly PgDataSourceScope _dataSource;
        
        private readonly DataType _outputType;
        private readonly string _outputVariable;
        public PgSelectProcessor(in ScriptContext context, in SelectStatement statement)
        {
            if (context.GetDataSource() is not PgDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _context = context;

            _mapper = new PgDataMapper(in _context, statement);

            if (statement.GetIntoClause() is IntoClause into)
            {
                _outputVariable = into.Value?.Identifier;

                if (into.Value is VariableReference variable)
                {
                    if (variable.Binding is DeclareStatement declare)
                    {
                        _outputType = declare.Type;
                    }
                }
            }
        }
        public override void Process()
        {
            List<DataObject> table = new();

            int outputCount = _mapper.OutputSchema.Properties.Count;

            using (NpgsqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _mapper.CommandText;

                _mapper.ProcessInput(in command);

                //if (_outputType.IsUndefined)
                //{
                //    int recordsAffected = command.ExecuteNonQuery(); // SELECT INTO #<temporary table>
                //}

                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (_outputType.IsArray)
                    {
                        while (reader.Read()) // select all rows
                        {
                            DataObject record = new(outputCount);

                            _mapper.ProcessOutput(in reader, in record);

                            table.Add(record);
                        }
                    }
                    else
                    {
                        if (reader.Read()) // select single row
                        {
                            DataObject record = new(outputCount);

                            _mapper.ProcessOutput(in reader, in record);

                            table.Add(record);
                        }
                    }

                    reader.Close();
                }
            }

            SetOutputValue(in table);
        }
        private void SetOutputValue(in List<DataObject> table)
        {
            if (_outputType.IsUndefined)
            {
                return;
            }

            object value;

            if (_outputType.IsArray)
            {
                if (_outputType.IsObject)
                {
                    value = table;
                }
                else // array of simple type
                {
                    value = _outputType.DefaultValue();

                    if (value is IList array)
                    {
                        DataObject record;

                        for (int i = 0; i < table.Count; i++)
                        {
                            record = table[i];

                            array.Add(record.GetFirstValue());
                        }
                    }
                }
            }
            else if (_outputType.IsObject)
            {
                value = table.Count > 0 ? table[0] : new DataObject();
            }
            else // scalar value
            {
                DataObject record = null;

                if (table.Count > 0)
                {
                    record = table[0];
                }

                if (record is null)
                {
                    value = _outputType.DefaultValue();
                }
                else
                {
                    value = record.GetFirstValue();
                }
            }

            _context.SetValue(in _outputVariable, in value);
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}