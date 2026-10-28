using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;

namespace DaJet.Scripting
{
    public sealed class Interpreter : ScriptContext
    {
        private CancellationToken _cancellationToken;
        private CancellationTokenSource _cancellationSource;
        private ManualResetEventSlim _cancellationAwaiter;

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

            _data.Add("@@ERROR_MESSAGE", string.Empty);

            _context = new ExpressionInterpreter(in _data);

            _cancellationSource = new CancellationTokenSource();
            _cancellationToken = _cancellationSource.Token;
        }
        public void Cancel()
        {
            if (_cancellationSource is null)
            {
                return;
            }
            
            _cancellationAwaiter = new ManualResetEventSlim(false);
            
            try
            {
                _cancellationSource.Cancel();

                _ = _cancellationAwaiter.Wait(TimeSpan.FromSeconds(30)); //TODO: timeout setting
            }
            catch
            {
                /* ignore errors */
            }
            finally
            {
                _cancellationAwaiter.Dispose();
                _cancellationAwaiter = null;
            }
        }
        public override CancellationToken GetCancellationToken() { return _cancellationToken; }
        private void ThrowIfCancellationRequested() { _cancellationToken.ThrowIfCancellationRequested(); }
        public bool IsCancellationRequested { get { return _cancellationToken.IsCancellationRequested; } }
        private void Dispose()
        {
            _cancellationSource.Dispose();
            _cancellationSource = null;

            _ = Parallel.ForEach(_processors.Values, static processor =>
            {
                processor.Dispose(); // Might be awaiting the completion of processing.
            });
            
            _data.Clear();
            _sources.Clear();
            _processors.Clear();
            _parameters.Clear();
            _returnValue = null;
            
            _cancellationAwaiter?.Set();
        }

        public override object Evaluate(in SyntaxNode expression)
        {
            return _context.Evaluate(in expression);
        }
        public override bool CreateVariable(in string name)
        {
            return _data.TryAdd(name, null);
        }
        public override void RemoveVariable(in string name)
        {
            _ = _data.Remove(name);
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

        private void ThrowIfScriptIsFaulted()
        {
            if (_data.TryGetValue("@@ERROR_MESSAGE", out object error))
            {
                if (error is string message && !string.IsNullOrWhiteSpace(message))
                {
                    throw new Exception(message);
                }
            }
            
            throw new Exception();
        }

        public object Execute()
        {
            object value = null;

            ExitCode code;

            try
            {
                foreach (SyntaxNode node in _script.Statements)
                {
                    ThrowIfCancellationRequested();

                    code = Execute(in node);

                    if (code == ExitCode.Success)
                    {
                        continue;
                    }
                    
                    if (code == ExitCode.Return)
                    {
                        value = _returnValue; return value;
                    }

                    if (code == ExitCode.Faulted)
                    {
                        ThrowIfScriptIsFaulted();
                    }

                    if (code == ExitCode.Cancel)
                    {
                        ThrowIfCancellationRequested();

                        throw new OperationCanceledException("Script is canceled.");
                    }
                    
                    throw new InvalidOperationException($"Unexpected exit code: {code}.");
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
        public object Execute(object parameters)
        {
            if (parameters is null)
            {
                return Execute();
            }

            if (parameters is DataObject data)
            {
                return Execute(in data);
            }

            if (parameters is Dictionary<string, object> input)
            {
                return Execute(in input);
            }

            throw new ArgumentOutOfRangeException(nameof(parameters));
        }
        public object Execute(in DataObject parameters)
        {
            if (parameters is not null)
            {
                _parameters = parameters;
            }
            
            return Execute();
        }
        public object Execute(in Dictionary<string, object> parameters)
        {
            if (parameters is not null)
            {
                _parameters = parameters;
            }

            return Execute();
        }

        //TODO: OpenDatabaseScope() !?
        //TODO: CloseDatabaseScope() !?
        public override DataSourceScope GetDataSource()
        {
            if (_sources.TryPeek(out DataSourceScope scope))
            {
                return scope;
            }

            return null;
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
            else if (node is UpsertStatement upsert) { return Execute(in upsert); }
            else if (node is DeleteStatement delete) { return Execute(in delete); }
            else if (node is ConsumeStatement consume) { return Execute(in consume); }
            else if (node is SqlStatement statement) { return Execute(in statement); }

            else if (node is ProduceStatement produce) { return Execute(in produce); }

            else if (node is CommentStatement) { return ExitCode.Success; }

            throw new NotImplementedException($"Statement is not implemented: {node.GetType()}");
        }
        private ExitCode Execute(in StatementBlock statements)
        {
            foreach (SyntaxNode node in statements)
            {
                if (IsCancellationRequested)
                {
                    return ExitCode.Cancel;
                }

                ExitCode code = Execute(in node);

                if (code != ExitCode.Success)
                {
                    return code; // Faulted, Return, Break, Continue, Cancel
                }
            }

            return ExitCode.Success; // end of statement block
        }
        public override ExitCode Callback(in StatementBlock statements)
        {
            return Execute(in statements);
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

            Task.Delay(delay, _cancellationToken).Wait(_cancellationToken);

            return ExitCode.Success;
        }
        private ExitCode Execute(in IfStatement statement)
        {
            object value = _context.Evaluate(statement.IF);

            if (value is not bool condition)
            {
                throw new InvalidOperationException("[IF] Condition must evaluate to boolean value");
            }

            ExitCode code = ExitCode.Success;

            if (condition)
            {
                code = Execute(statement.THEN);
            }
            else if (statement.ELSE is not null)
            {
                code = Execute(statement.ELSE);
            }

            return code;
        }
        private ExitCode Execute(in ForStatement statement)
        {
            object value = _context.Evaluate(statement.Iterator);

            if (value is not List<DataObject> iterator)
            {
                throw new InvalidOperationException("[FOR] Condition must evaluate to boolean value");
            }

            ExitCode code = ExitCode.Success;

            if (iterator.Count == 0)
            {
                return code;
            }

            foreach (DataObject item in iterator)
            {
                SetValue(statement.Variable.Identifier, item);

                code = Execute(statement.Statements);

                if (code == ExitCode.Break)
                {
                    code = ExitCode.Success; break;
                }
                else if (code == ExitCode.Continue)
                {
                    code = ExitCode.Success;
                }
                else if (code != ExitCode.Success)
                {
                    return code;
                }
            }

            return code;
        }
        private ExitCode Execute(in WhileStatement statement)
        {
            object value = _context.Evaluate(statement.Condition);

            if (value is not bool condition)
            {
                throw new InvalidOperationException("[WHILE] Condition must evaluate to boolean value");
            }

            ExitCode code = ExitCode.Success;

            while (condition)
            {
                code = Execute(statement.Statements);

                if (code == ExitCode.Break)
                {
                    code = ExitCode.Success; break;
                }
                else if (code == ExitCode.Continue)
                {
                    code = ExitCode.Success;
                }
                else if (code != ExitCode.Success)
                {
                    return code;
                }

                condition = (bool)_context.Evaluate(statement.Condition);
            }

            return code;
        }
        private ExitCode Execute(in BreakStatement _) { return ExitCode.Break; }
        private ExitCode Execute(in ContinueStatement _) { return ExitCode.Continue; }
        private ExitCode Execute(in TryStatement statement)
        {
            ExitCode code = ExitCode.Success;

            try
            {
                return Execute(statement.TRY);
            }
            catch (Exception error)
            {
                _data["@@ERROR_MESSAGE"] = ExceptionHelper.GetErrorMessage(error);

                if (statement.CATCH is not null)
                {
                    code = Execute(statement.CATCH);
                }
            }
            finally
            {
                _data["@@ERROR_MESSAGE"] = string.Empty;

                if (statement.FINALLY is not null)
                {
                    code = Execute(statement.FINALLY);
                }
            }

            return code;
        }
        private ExitCode Execute(in ThrowStatement statement)
        {
            object value = _context.Evaluate(statement.Expression);

            if (value is not string message)
            {
                throw new InvalidOperationException("[THROW] Expression must evaluate to string value");
            }

            throw new Exception(message);
        }
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

            _sources.Push(use); // Open a new database connection context.

            ExitCode code = ExitCode.Success;

            //TODO: Требуется определиться являются ли методы Cancel и Dispose класса DataSourceScope
            //TODO: семантически аналогичными ? В текущей реализации это скорее всего именно так ...
            //TODO: Должны ли подписчики на события OnCancel или OnDispose класса DataSourceScope
            //TODO: понимать или как-то дифференцированно использовать эту семантичекую разницу ?

            try
            {
                code = Execute(statement.Statements);

                if (code == ExitCode.Success)
                {
                    use.Commit(); // commit database transaction
                }
            }
            finally
            {
                use.Dispose(); // This method does not throw any exceptions.
                
                _ = _sources.TryPop(out _); // Close the current database connection context.
            }
            
            return code;
        }
        private ExitCode Execute(in SelectStatement statement)
        {
            if (GetDataSource() is not DataSourceScope use)
            {
                throw new InvalidOperationException("[SELECT] USE scope is missing");
            }

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

            return processor.Process();
        }
        private ExitCode Execute(in ConsumeStatement statement)
        {
            if (statement.IsDatabaseSource)
            {
                return ExecuteDatabaseConsumer(in statement);
            }
            
            if (statement.Target.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteRabbitMQConsumer(in statement);
            }

            if (statement.Target.StartsWith("kafka", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteApacheKafkaConsumer(in statement);
            }

            throw new InvalidOperationException($"Unsupported consumer: {statement.Target}");
        }
        private ExitCode ExecuteDatabaseConsumer(in ConsumeStatement statement)
        {
            if (GetDataSource() is not DataSourceScope use)
            {
                throw new InvalidOperationException("[CONSUME] USE scope is missing");
            }

            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                if (use.Type == DataSourceType.SqlServer)
                {
                    processor = new MsConsumeProcessor(this, in statement);
                }
                else
                {
                    processor = new PgConsumeProcessor(this, in statement);
                }

                _processors.Add(statement, processor);
            }

            return processor.Process();
        }
        private ExitCode ExecuteRabbitMQConsumer(in ConsumeStatement statement)
        {
            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                processor = new RabbitMQ.Consumer(this, in statement);

                _processors.Add(statement, processor);
            }

            ExitCode code = ExitCode.Success;

            try
            {
                code = processor.Process();
            }
            finally
            {
                processor.Dispose();
            }

            return code;
        }
        private ExitCode ExecuteApacheKafkaConsumer(in ConsumeStatement statement)
        {
            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                processor = new Kafka.Consumer(this, in statement);

                _processors.Add(statement, processor);
            }

            ExitCode code = ExitCode.Success;

            try
            {
                code = processor.Process();
            }
            finally
            {
                processor.Dispose();
            }

            return code;
        }

        private ExitCode Execute(in InsertStatement statement)
        {
            if (GetDataSource() is not DataSourceScope use)
            {
                throw new InvalidOperationException("[INSERT] USE scope is missing");
            }

            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                if (use.Type == DataSourceType.SqlServer)
                {
                    if (statement.Source is null)
                    {
                        processor = new MsInsertProcessor(this, in statement);
                    }
                    else
                    {
                        processor = new MsBulkInsertProcessor(this, in statement);
                    }
                }
                else
                {
                    if (statement.Source is null)
                    {
                        processor = new PgInsertProcessor(this, in statement);
                    }
                    else
                    {
                        processor = new PgBulkInsertProcessor(this, in statement);
                    }
                }

                _processors.Add(statement, processor);
            }

            return processor.Process();
        }
        private ExitCode Execute(in UpdateStatement statement) { throw new NotImplementedException("Statement is not implemented: UPDATE"); }
        private ExitCode Execute(in UpsertStatement statement) { throw new NotImplementedException("Statement is not implemented: UPSERT"); }
        private ExitCode Execute(in DeleteStatement statement) { throw new NotImplementedException("Statement is not implemented: DELETE"); }
        private ExitCode Execute(in SqlStatement statement)
        {
            if (GetDataSource() is not DataSourceScope use)
            {
                throw new InvalidOperationException("[SQL] USE scope is missing");
            }

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

            return processor.Process();
        }

        private ExitCode Execute(in ProduceStatement statement)
        {
            if (!_processors.TryGetValue(statement, out ProcessorBase processor))
            {
                if (statement.Target == "kafka")
                {
                    processor = new Kafka.Producer(this, in statement);
                }
                else if (statement.Target.StartsWith("amqp"))
                {
                    processor = new RabbitMQ.Producer(this, in statement);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported producer: {statement.Target}");
                }
                
                _processors.Add(statement, processor);
            }

            ExitCode code = processor.Process();

            return code;
        }
    }
}