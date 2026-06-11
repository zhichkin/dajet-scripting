using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class Interpreter
    {
        private readonly Script _script;
        private readonly ExpressionInterpreter _context;
        private readonly Dictionary<string, object> _data = new();
        private readonly Stack<DataSourceScope> _sources = new();
        private readonly List<ProcessorBase> _processors = new();

        private object _returnValue = null; // output value
        private Dictionary<string, object> _parameters = new(); // input parameters
        public Interpreter(in Script script)
        {
            ArgumentNullException.ThrowIfNull(script, nameof(script));

            _script = script;

            _context = new ExpressionInterpreter(in _data);
        }
        public object Execute()
        {
            object value = null;

            try
            {
                foreach (SyntaxNode node in _script.Statements)
                {
                    ExitCode code = Execute(in node);

                    if (code == ExitCode.Return)
                    {
                        value = _returnValue; break;
                    }
                    else if (code == ExitCode.Cancel)
                    {
                        break;
                    }
                }
            }
            finally
            {
                Dispose();
            }

            return value;
        }
        public object Execute(in Dictionary<string, object> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters, nameof(parameters));

            _parameters = parameters;

            return Execute();
        }
        private void Dispose()
        {
            _returnValue = null;
            _parameters.Clear();

            _data.Clear();

            foreach (DataSourceScope source in _sources)
            {
                source.Dispose();
            }

            _sources.Clear();
        }
        
        private ExitCode Execute(in SyntaxNode node)
        {
            if (node is DeclareStatement declare) { return Execute(in declare); }
            else if (node is PrintStatement print) { return Execute(in print); }
            else if (node is UseStatement use) { return Execute(in use); }
            else if (node is SelectStatement select) { return Execute(in select); }
            else if (node is ReturnStatement _return) { return Execute(in _return); }
            else if (node is AssignmentOperator assign) { return Execute(in assign); }
            
            return ExitCode.Success;
        }
        private ExitCode Execute(in StatementBlock statements)
        {
            foreach (SyntaxNode node in statements)
            {
                ExitCode code = Execute(in node);

                if (code != ExitCode.Success)
                {
                    return code;
                }
            }

            return ExitCode.Success;
        }
        private ExitCode Execute(in DeclareStatement statement)
        {
            string name = statement.Identifier;
            
            object value = null;

            if (statement.IsPrivate)
            {
                value = _context.Evaluate(statement.Initializer);
            }
            else // apply parameter value if provided
            {
                string parameterName = name.TrimStart('@');

                if (!_parameters.TryGetValue(parameterName, out value))
                {
                    value = _context.Evaluate(statement.Initializer); // parameter is not provided
                }
            }

            if (value is null) // set default value
            {
                DataType type = statement.Type;

                if (type.IsBoolean) { value = false; }
                else if (type.IsDecimal) { value = 0M; }
                else if (type.IsInteger) { value = type.Size == 4 ? 0 : 0L; }
                else if (type.IsDateTime) { value = DateTime.MinValue; }
                else if (type.IsString) { value = string.Empty; }
                else if (type.IsBinary) { value = Array.Empty<byte>(); }
                else if (type.IsUuid) { value = Guid.Empty; }
                else if (type.IsEntity) { value = Entity.Undefined; }
                else if (type.IsUnion) { value = Union.Undefined; }
                else if (type.IsObject) { value = new Dictionary<string, object>(); }
                else if (type.IsArray) { value = new List<Dictionary<string, object>>(); }
            }

            _data.Add(name, value);

            return ExitCode.Success;
        }
        private ExitCode Execute(in PrintStatement statement)
        {
            object value = _context.Evaluate(statement.Expression);

            if (value is not null)
            {
                Console.WriteLine(value.ToString());
            }

            return ExitCode.Success;
        }
        private ExitCode Execute(in ReturnStatement statement)
        {
            _returnValue = _context.Evaluate(statement.Expression);

            return ExitCode.Return;
        }
        private ExitCode Execute(in UseStatement statement)
        {
            MetadataProvider provider = MetadataProvider.Get(statement.Source);

            string connectionString = provider.ConnectionString;

            DataSourceScope use;

            if (provider.DataSource == DataSourceType.SqlServer)
            {
                use = new MsDataSourceScope(connectionString, "READCOMMITTED");
            }
            else if (provider.DataSource == DataSourceType.PostgreSql)
            {
                use = new PgDataSourceScope(connectionString, "READCOMMITTED");
            }
            else
            {
                throw new InvalidOperationException($"Unsupported data source: {provider.DataSource}");
            }

            _sources.Push(use);

            ExitCode code = ExitCode.Success;

            try
            {
                code = Execute(statement.Statements);
            }
            finally
            {
                use.Dispose();
            }

            _ = _sources.Pop();

            return code;
        }
        private ExitCode Execute(in SelectStatement statement)
        {
            DataSourceScope use = _sources.Peek();

            ProcessorBase processor; //TODO: use _processors collection

            if (use.Type == DataSourceType.SqlServer)
            {
                processor = new MsSelectProcessor(in statement, in _sources, in _context);
            }
            else
            {
                processor = new PgSelectProcessor(in statement, in _sources, in _context);
            }

            processor.Process();

            return ExitCode.Success;
        }
        private ExitCode Execute(in AssignmentOperator statement)
        {
            object value = _context.Evaluate(statement.Initializer);

            if (statement.Target is VariableReference variable)
            {
                if (_context.Data.ContainsKey(variable.Identifier))
                {
                    _context.Data[variable.Identifier] = value;
                }
            }
            else if (statement.Target is MemberAccessExpression member)
            {
                List<string> members = member.GetAccessMembers();

                if (_context.Data.TryGetValue(members[0], out object target))
                {
                    if (target is Dictionary<string, object> _object)
                    {
                        if (!_object.TryAdd(members[1], value))
                        {
                            _object[members[1]] = value;
                        }
                    }
                }
            }

            return ExitCode.Success;
        }
    }
}