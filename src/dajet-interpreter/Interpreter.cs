using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class Interpreter : ScriptContext
    {
        private readonly Script _script;
        private readonly ExpressionInterpreter _context;
        private readonly Stack<DataSourceScope> _sources = new();
        private readonly Dictionary<string, object> _data = new();
        private readonly Dictionary<SyntaxNode, ProcessorBase> _processors = new();

        private object _returnValue = null; // output value
        private Dictionary<string, object> _parameters = new(); // input parameters
        public Interpreter(in Script script)
        {
            ArgumentNullException.ThrowIfNull(script, nameof(script));

            _script = script;

            _context = new ExpressionInterpreter(in _data);
        }
        public void Cancel() { Dispose(); }
        private void Dispose()
        {
            foreach (DataSourceScope source in _sources)
            {
                source.Dispose();
            }

            foreach (ProcessorBase processor in _processors.Values)
            {
                processor.Dispose();
            }

            _data.Clear();
            _sources.Clear();
            _parameters.Clear();
            _processors.Clear();
            _returnValue = null;
        }

        public override DataSourceScope GetDataSource()
        {
            return _sources.Peek();
        }
        public override object Evaluate(in SyntaxNode expression)
        {
            return _context.Evaluate(in expression);
        }
        public override object GetValue(in string name)
        {
            if (_data.TryGetValue(name, out object value))
            {
                return value;
            }

            return null;
        }
        public override void SetValue(in string name, in object value)
        {
            if (_data.TryGetValue(name, out object target))
            {
                if (target is not Union)
                {
                    _data[name] = value;
                }
                else
                {
                    if (value is null)
                    {
                        _data[name] = Union.Undefined;
                    }
                    else if (value is bool boolean)
                    {
                        _data[name] = new Union.CaseBoolean(boolean);
                    }
                    else if (value is decimal number)
                    {
                        _data[name] = new Union.CaseDecimal(number);
                    }
                    else if (value is DateTime datetime)
                    {
                        _data[name] = new Union.CaseDateTime(datetime);
                    }
                    else if (value is string text)
                    {
                        _data[name] = new Union.CaseString(text);
                    }
                    else if (value is Entity entity)
                    {
                        _data[name] = new Union.CaseEntity(entity);
                    }
                    else
                    {
                        _data[name] = value;
                    }
                }
            }
        }

        public void Configure(CancellationToken cancellation)
        {
            Cancellation = cancellation;
        }
        public void SetParameters(in DataObject parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters, nameof(parameters));

            _parameters = parameters;
        }
        public void SetParameters(in Dictionary<string, object> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters, nameof(parameters));

            _parameters = parameters;
        }

        public object Execute()
        {
            object value = null;

            ExitCode code;

            try
            {
                foreach (SyntaxNode node in _script.Statements)
                {
                    code = Execute(in node);

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
            catch (OperationCanceledException)
            {
                code = ExitCode.Cancel; throw;
            }
            catch
            {
                code = ExitCode.Faulted; throw;
            }
            finally
            {
                Dispose();
            }

            return value;
        }
        public object Execute(in DataObject parameters)
        {
            SetParameters(in parameters); return Execute();
        }
        public object Execute(in Dictionary<string, object> parameters)
        {
            SetParameters(in parameters); return Execute();
        }
        private ExitCode Execute(in SyntaxNode node)
        {
            if (node is DeclareStatement declare) { return Execute(in declare); }
            else if (node is PrintStatement print) { return Execute(in print); }
            else if (node is SleepStatement sleep) { return Execute(in sleep); }
            else if (node is IfStatement _if) { return Execute(in _if); }
            else if (node is ForStatement _for) { return Execute(in _for); }
            else if (node is WhileStatement _while) { return Execute(in _while); }
            else if (node is BreakStatement _break) { return Execute(in _break); }
            else if (node is ContinueStatement _continue) { return Execute(in _continue); }
            else if (node is TryStatement _try) { return Execute(in _try); }
            else if (node is ThrowStatement _throw) { return Execute(in _throw); }
            else if (node is ReturnStatement _return) { return Execute(in _return); }
            else if (node is AssignmentOperator assign) { return Execute(in assign); }
            else if (node is UseStatement use) { return Execute(in use); }
            else if (node is SelectStatement select) { return Execute(in select); }
            else if (node is InsertStatement insert) { return Execute(in insert); }
            else if (node is UpdateStatement update) { return Execute(in update); }
            else if (node is DeleteStatement delete) { return Execute(in delete); }
            else if (node is ConsumeStatement consume) { return Execute(in consume); }
            else if (node is SqlStatement statement) { return Execute(in statement); }

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

            object value;

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

            value ??= statement.Type.DefaultValue();

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
        private ExitCode Execute(in SleepStatement statement)
        {
            TimeSpan delay = TimeSpan.FromSeconds(statement.Timeout);

            Task.Delay(delay, Cancellation).Wait(Cancellation);

            return ExitCode.Success;
        }
        private ExitCode Execute(in IfStatement statement) { throw new NotImplementedException("Statement is not implemented: IF"); }
        private ExitCode Execute(in ForStatement statement) { throw new NotImplementedException("Statement is not implemented: FOR"); }
        private ExitCode Execute(in WhileStatement statement) { throw new NotImplementedException("Statement is not implemented: WHILE"); }
        private ExitCode Execute(in BreakStatement statement) { throw new NotImplementedException("Statement is not implemented: BREAK"); }
        private ExitCode Execute(in ContinueStatement statement) { throw new NotImplementedException("Statement is not implemented: CONTINUE"); }
        private ExitCode Execute(in TryStatement statement) { throw new NotImplementedException("Statement is not implemented: TRY"); }
        private ExitCode Execute(in ThrowStatement statement) { throw new NotImplementedException("Statement is not implemented: THROW"); }
        private ExitCode Execute(in ReturnStatement statement)
        {
            _returnValue = _context.Evaluate(statement.Expression);

            return ExitCode.Return;
        }
        private ExitCode Execute(in AssignmentOperator statement)
        {
            object value = _context.Evaluate(statement.Initializer);

            if (statement.Target is VariableReference variable)
            {
                SetValue(variable.Identifier, in value);
            }
            else if (statement.Target is MemberAccessExpression member)
            {
                List<string> members = member.GetAccessMembers();

                object target = GetValue(members[0]);

                if (target is DataObject _object)
                {
                    _object.SetValue(members[1], value);
                }
            }

            return ExitCode.Success;
        }

        private ExitCode Execute(in UseStatement statement)
        {
            MetadataProvider provider = MetadataProvider.Get(statement.Source);

            string connectionString = provider.ConnectionString;

            DataSourceScope use;

            if (provider.DataSource == DataSourceType.SqlServer)
            {
                use = new MsDataSourceScope(connectionString, statement.IsTransactional);
            }
            else if (provider.DataSource == DataSourceType.PostgreSql)
            {
                use = new PgDataSourceScope(connectionString, statement.IsTransactional);
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

                if (code == ExitCode.Success)
                {
                    use.Commit();
                }
                else
                {
                    use.Rollback();
                }
            }
            catch
            {
                use.Rollback(); throw;
            }
            finally
            {
                _ = _sources.Pop(); use.Dispose();
            }
            
            return code;
        }
        private ExitCode Execute(in SelectStatement statement)
        {
            DataSourceScope use = GetDataSource();
            
            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                if (use.Type == DataSourceType.SqlServer)
                {
                    processor = new MsSelectProcessor(this, in statement);
                }
                else
                {
                    processor = new PgSelectProcessor(this, in statement);
                }

                _processors.Add(statement, processor);
            }

            try
            {
                processor.Process();
            }
            finally
            {
                processor.Dispose();
            }

            return ExitCode.Success;
        }
        private ExitCode Execute(in InsertStatement statement)
        {
            DataSourceScope use = GetDataSource();

            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                if (use.Type == DataSourceType.SqlServer)
                {
                    processor = new MsInsertProcessor(this, in statement);
                }
                else
                {
                    processor = new PgInsertProcessor(this, in statement);
                }

                _processors.Add(statement, processor);
            }

            try
            {
                processor.Process();
            }
            finally
            {
                processor.Dispose();
            }

            return ExitCode.Success;
        }
        private ExitCode Execute(in UpdateStatement statement) { throw new NotImplementedException("Statement is not implemented: UPDATE"); }
        private ExitCode Execute(in DeleteStatement statement) { throw new NotImplementedException("Statement is not implemented: DELETE"); }
        private ExitCode Execute(in ConsumeStatement statement) { throw new NotImplementedException("Statement is not implemented: CONSUME"); }
        private ExitCode Execute(in SqlStatement statement)
        {
            DataSourceScope use = GetDataSource();

            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                if (use.Type == DataSourceType.SqlServer)
                {
                    processor = new MsNonQueryProcessor(this, in statement);
                }
                else
                {
                    processor = new PgNonQueryProcessor(this, in statement);
                }

                _processors.Add(statement, processor);
            }

            try
            {
                processor.Process();
            }
            finally
            {
                processor.Dispose();
            }

            return ExitCode.Success;
        }
    }
}