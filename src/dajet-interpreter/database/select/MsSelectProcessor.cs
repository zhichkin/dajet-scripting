using DaJet.Data;
using DaJet.Scripting.Host;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;

namespace DaJet.Scripting
{
    public sealed class MsSelectProcessor : ProcessorBase
    {
        private readonly MsDataMapper _mapper;
        private readonly ScriptContext _context;
        private readonly MsDataSourceScope _dataSource;

        private readonly bool _outputIsObject;
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

            int outputCount = _mapper.OutputSchema.Properties.Count;

            //THINK: DataSourceScope scope = _context.GetDataSource();

            using (SqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _mapper.CommandText;

                _mapper.ProcessInput(in command);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Dictionary<string, object> record = new(outputCount);

                        _mapper.ProcessOutput(in reader, in record);

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

                _context.SetValue(in _outputVariable, in value);
            }
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}