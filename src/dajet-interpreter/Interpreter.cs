using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class Interpreter
    {
        private Script _script;
        private ExpressionInterpreter _expression;
        private Dictionary<SyntaxNode, SqlStatement> _statements = new();

        private Dictionary<string, object> _parameters = new(); // input parameters

        private object _returnValue = null;
        private readonly Dictionary<string, object> _data = new();
        private readonly Stack<DataSourceScope> _sources = new();
        private readonly List<ProcessorBase> _processors = new();
        public Interpreter(in string source)
        {
            try
            {
                Prepare(in source);
            }
            catch
            {
                Dispose(); throw;
            }
        }
        private void Prepare(in string source)
        {
            _expression = new ExpressionInterpreter(in _data);

            Parser parser = new();

            if (!parser.TryParse(in source, out _script, out string error))
            {
                throw new InvalidOperationException(error);
            }

            Binder binder = new();
            CacheableSchemaProvider schema = new();

            if (!binder.TryBind(in _script, schema, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            Transpiler transpiler = new();

            if (!transpiler.TryTranspile(in _script, out List<SqlStatement> statements, out errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            foreach (SqlStatement statement in statements)
            {
                _statements.Add(statement.Node, statement);
            }
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
        }
        
        private ExitCode Execute(in SyntaxNode node)
        {
            if (node is StatementBlock block) { return Execute(in block); }
            else if (node is DeclareStatement declare) { return Execute(in declare); }
            else if (node is PrintStatement print) { return Execute(in print); }
            else if (node is UseStatement use) { return Execute(in use); }
            else if (node is SelectStatement select) { return Execute(in select); }
            else if (node is ReturnStatement _return) { return Execute(in _return); }
            else if (node is AssignmentOperator assign) { return Execute(in assign); }
            
            return ExitCode.Success;
        }
        private ExitCode Execute(in StatementBlock block)
        {
            foreach (SyntaxNode node in block.Statements)
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

            string parameterName = name.TrimStart('@');

            if (!_parameters.TryGetValue(parameterName, out value))
            {
                value = _expression.Evaluate(statement.Initializer);
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
            object value = _expression.Evaluate(statement.Expression);

            if (value is not null)
            {
                Console.WriteLine(value.ToString());
            }

            return ExitCode.Success;
        }
        private ExitCode Execute(in ReturnStatement statement)
        {
            _returnValue = _expression.Evaluate(statement.Expression);

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

            if (!_statements.TryGetValue(statement, out SqlStatement sql))
            {
                throw new InvalidOperationException();
            }

            ProcessorBase processor; //TODO: use _processors collection

            if (use.Type == DataSourceType.SqlServer)
            {
                processor = new MsSelectProcessor(in _sources, in sql, in _expression, in _data);
            }
            else
            {
                processor = new PgSelectProcessor(in _sources, in sql, in _expression, in _data);
            }

            processor.Process();

            return ExitCode.Success;
        }
        private ExitCode Execute(in AssignmentOperator statement)
        {
            //TODO:

            return ExitCode.Success;
        }
    }
}