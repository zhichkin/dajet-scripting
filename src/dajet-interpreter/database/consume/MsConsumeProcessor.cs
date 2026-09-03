using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Collections;

namespace DaJet.Scripting
{
    public sealed class MsConsumeProcessor : ProcessorBase
    {
        private readonly MsDataMapper _mapper;
        private readonly ScriptContext _context;
        private readonly ConsumeStatement _statement;
        
        private readonly DataType _outputType;
        private readonly string _outputVariable;
        public MsConsumeProcessor(in ScriptContext context, in ConsumeStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope)
            {
                throw new InvalidOperationException();
            }

            _context = context;
            _statement = statement;

            _mapper = new MsDataMapper(in _context, statement);

            if (statement.Into is IntoClause into)
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
        public override ExitCode Process()
        {
            ExitCode code = ExitCode.Success;

            if (_statement.IsStream)
            {
                code = Stream();
            }
            else
            {
                Select();
            }

            return code;
        }
        private void SetOutputValue(in List<DataObject> table)
        {
            if (!_outputType.IsArray)
            {
                throw new InvalidOperationException($"[MsConsumeProcessor] Unsupported output variable type: array expected.");
            }

            object value;

            if (_outputType.IsObject) // array of records
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

            _context.SetValue(in _outputVariable, in value);
        }
        private void Select()
        {
            if (_context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            List<DataObject> table = new();

            int outputCount = _mapper.OutputSchema.Properties.Count;

            using (SqlCommand command = use.CreateCommand())
            {
                _mapper.ProcessInput(in command);

                command.CommandText = _mapper.CommandText;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DataObject record = new(outputCount);

                        _mapper.ProcessOutput(in reader, in record);

                        table.Add(record);
                    }

                    reader.Close();
                }
            }

            SetOutputValue(in table);
        }
        private ExitCode Stream()
        {
            ExitCode code = ExitCode.Success;

            if (_context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            int outputCount = _mapper.OutputSchema.Properties.Count;

            DataObject record = new(outputCount);

            using (SqlCommand command = use.CreateCommand())
            {
                _mapper.ProcessInput(in command);

                command.CommandText = _mapper.CommandText;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _mapper.ProcessOutput(in reader, in record);

                        _context.SetValue(in _outputVariable, record);

                        if (_statement.Statements is not null)
                        {
                            code = _context.Callback(_statement.Statements);

                            if (code != ExitCode.Success)
                            {
                                break;
                            }
                        }
                    }

                    reader.Close();
                }
            }

            _context.SetValue(in _outputVariable, new DataObject());

            return code;
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}