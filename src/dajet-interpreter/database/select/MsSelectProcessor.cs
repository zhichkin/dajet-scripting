using DaJet.Data;
using DaJet.Scripting.Host;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;

namespace DaJet.Scripting
{
    public sealed class MsSelectProcessor : ProcessorBase
    {
        private readonly MsDataMapper _mapper;
        private readonly ScriptContext _context;
        private readonly MsDataSourceScope _dataSource;

        private readonly DataType _outputType;
        private readonly string _outputVariable;
        public MsSelectProcessor(in ScriptContext context, in SelectStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _context = context;

            _mapper = new MsDataMapper(in _context, statement);

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
            List<Dictionary<string, object>> table = new();

            int outputCount = _mapper.OutputSchema.Properties.Count;

            using (SqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _mapper.CommandText;

                _mapper.ProcessInput(in command);

                //if (_outputType.IsUndefined)
                //{
                //    int recordsAffected = command.ExecuteNonQuery(); // SELECT INTO #<temporary table>
                //}

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (_outputType.IsArray)
                    {
                        while (reader.Read()) // select all rows
                        {
                            Dictionary<string, object> record = new(outputCount);

                            _mapper.ProcessOutput(in reader, in record);

                            table.Add(record);
                        }
                    }
                    else
                    {
                        if (reader.Read()) // select single row
                        {
                            Dictionary<string, object> record = new(outputCount);

                            _mapper.ProcessOutput(in reader, in record);

                            table.Add(record);
                        }
                    }
                    
                    reader.Close();
                }
            }

            SetOutputValue(in table);
        }
        private void SetOutputValue(in List<Dictionary<string, object>> table)
        {
            if (_outputType.IsUndefined)
            {
                return;
            }

            object value;

            if (_outputType.IsArray)
            {
                value = table;

            }
            else if (_outputType.IsObject)
            {
                value = table.Count > 0 ? table[0] : [];
            }
            else // scalar value
            {
                Dictionary<string, object> record = null;

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
                    value = record.Count > 0
                        ? record.First().Value
                        : _outputType.DefaultValue();
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