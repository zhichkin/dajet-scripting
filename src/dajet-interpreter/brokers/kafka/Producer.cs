using Confluent.Kafka;
using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.Utilities;
using System.Text;
using Error = Confluent.Kafka.Error;

namespace DaJet.Kafka
{
    public sealed class Producer : ProcessorBase
    {
        private readonly ScriptContext _context;
        private readonly ProduceStatement _statement;
        private readonly Dictionary<string, SyntaxNode> _options = new();
        private readonly Dictionary<string, SyntaxNode> _payload = new();
        private string _error;
        private int _produced;
        private int _persisted;
        private readonly ProducerConfig _config;
        private IProducer<byte[], byte[]> _producer;
        private readonly Action<IProducer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IProducer<byte[], byte[]>, LogMessage> _logHandler;
        private readonly Action<DeliveryReport<byte[], byte[]>> _deliveryHandler;
        public Producer(in ScriptContext context, in ProduceStatement statement)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentNullException.ThrowIfNull(statement, nameof(statement));

            _context = context;
            _statement = statement;

            foreach (ColumnExpression column in _statement.Options)
            {
                _options.Add(column.Alias, column.Expression);
            }

            foreach (ColumnExpression column in _statement.Columns)
            {
                _payload.Add(column.Alias, column.Expression);
            }

            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
            _deliveryHandler = DeliveryHandler;

            _config = CreateProducerConfig();
        }
        private ProducerConfig CreateProducerConfig()
        {
            Dictionary<string, string> config = new();

            foreach (KeyValuePair<string, SyntaxNode> option in _options)
            {
                SyntaxNode expression = option.Value;

                object value = _context.Evaluate(in expression);

                config.Add(option.Key, value.ToString());
            }

            return new ProducerConfig(config);
        }
        private byte[] GetKey()
        {
            if (_payload.TryGetValue("Key", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is null)
                {
                    throw new InvalidOperationException("Null value is not allowed for the \"Key\" property of a record.");
                }

                if (value is byte[] binary)
                {
                    if (binary.Length == 0)
                    {
                        throw new InvalidOperationException("Empty byte array is not allowed for the \"Key\" property of a record.");
                    }

                    return binary; //TODO: protobuf
                }
                
                if (value is string text)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        throw new InvalidOperationException("Empty string value is not allowed for the \"Key\" property of a record.");
                    }

                    return Encoding.UTF8.GetBytes(text);
                }
                
                return Encoding.UTF8.GetBytes(value.ToString()); //THINK: integer, decimal, uuid - binary format ?
            }
            
            return null; // optional value
        }
        private byte[] GetValue()
        {
            if (!_payload.TryGetValue("Value", out SyntaxNode expression))
            {
                throw new InvalidOperationException("The \"Value\" property of the record is not defined.");
            }

            object value = _context.Evaluate(in expression);

            if (value is null)
            {
                throw new InvalidOperationException("Null value is not allowed for the \"Value\" property of a record.");
            }

            if (value is byte[] binary)
            {
                if (binary.Length == 0)
                {
                    throw new InvalidOperationException("Empty byte array is not allowed for the \"Value\" property of a record.");
                }

                return binary; //TODO: protobuf
            }

            if (value is string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Empty string value is not allowed for the \"Value\" property of a record.");
                }

                return Encoding.UTF8.GetBytes(text);
            }

            throw new InvalidOperationException("The \"Value\" property of a record must be a string or binary value.");
        }
        private string GetTopic()
        {
            if (!_payload.TryGetValue("Topic", out SyntaxNode expression))
            {
                throw new InvalidOperationException("Topic is not defined.");
            }

            object value = _context.Evaluate(in expression);

            if (value is not string topic)
            {
                throw new InvalidOperationException("Topic must be a string.");
            }

            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new InvalidOperationException("Topic value is empty.");
            }

            return topic;
        }
        public override ExitCode Process()
        {
            ExitCode code = ExitCode.Success;

            try
            {
                ThrowIfStateIsBroken();

                EnsureProcessorIsInitialized();

                var message = new Message<byte[], byte[]>()
                {
                    Key = GetKey(),
                    Value = GetValue()
                    //TODO: Headers !?
                };

                _producer.Produce(GetTopic(), message, _deliveryHandler); // async inside - returns immediately

                _ = Interlocked.Increment(ref _produced);
            }
            catch (ProduceException<byte[], byte[]> error)
            {
                WriteToLogProduceError(in error);

                ResetState(); throw;
            }
            catch
            {
                ResetState(); throw;
            }

            return code;
        }
        private void EnsureProcessorIsInitialized()
        {
            if (_producer is null) // uninitialized processor state
            {
                DataSourceScope scope = _context.GetDataSource();

                if (scope is not null)
                {
                    // enter database scope if present
                    scope.OnCommit += SynchronizeCommit;
                    scope.OnDispose += SynchronizeDispose;
                }

                try
                {
                    _producer = new ProducerBuilder<byte[], byte[]>(_config)
                        .SetLogHandler(_logHandler)
                        .SetErrorHandler(_errorHandler)
                        .Build();
                }
                catch (Exception error)
                {
                    if (scope is not null)
                    {
                        // exit database scope on failure
                        scope.OnCommit -= SynchronizeCommit;
                        scope.OnDispose -= SynchronizeDispose;
                    }

                    WriteToLogFailedToBuildProducer(in error); throw;
                }
            }
        }
        private void LogHandler(IProducer<byte[], byte[]> _, LogMessage message)
        {
            FileLogger.Default.Write($"[INFO][DaJet.Kafka.Producer][{_config.ClientId}] {message.Name}: {message.Message}");
        }
        private void ErrorHandler(IProducer<byte[], byte[]> _, Error error)
        {
            _error = error.Reason; // set processor state to broken

            string message = $"[ERROR][DaJet.Kafka.Producer][{_config.ClientId}] {error.Reason}";
            
            FileLogger.Default.Write(message);
        }
        private void DeliveryHandler(DeliveryReport<byte[], byte[]> report)
        {
            if (report.Status == PersistenceStatus.Persisted)
            {
                _ = Interlocked.Increment(ref _persisted); return; // successful delivery
            }

            if (report.Error is not null && report.Error.Code != ErrorCode.NoError)
            {
                _error ??= report.Error.Reason; // set processor state to broken

                string error = $"[ERROR][DaJet.Kafka.Producer][{_config.ClientId}][{report.Topic}] {report.Status} {{{report.Error.Code}}} {report.Error.Reason}";
                
                FileLogger.Default.Write(error);
            }
        }
        private void SynchronizeCommit(object sender, EventArgs args)
        {
            // Данный метод должен либо завершиться успешно, либо вернуть ошибку!
            // Исключительным критерием успешности является равенство _produced и _persisted.
            // Это критически важно с точки зрения обеспечения целостности данных,
            // выполняемой в данный момент времени транзакции базы данных.

            try
            {
                ThrowIfStateIsBroken();

                // Wait until all outstanding produce requests and delivery report callbacks are completed.
                // The related configuration parameter is "message.timeout.ms",
                // which determines the maximum length of time librdkafka attempts to deliver a message
                // and so also affects the maximum time a call to "Flush" method may block.

                _producer.Flush(_context.GetCancellationToken());

                if (_produced == _persisted)
                {
                    WriteToLogSuccessfulStory(); return; // The only successful path!
                }
                
                // Throw an exception to trigger a database transaction rollback.
                
                ThrowFailedToConfirmDeliveries();
            }
            catch (OperationCanceledException)
            {
                WriteToLogWarningOnCancellation(); throw;
            }
            finally // Cleanup resources and restore the initial state.
            {
                if (sender is DataSourceScope scope)
                {
                    // exit database scope entered earlier
                    scope.OnCommit -= SynchronizeCommit;
                    scope.OnDispose -= SynchronizeDispose;
                }

                ResetState();
            }
        }
        private void SynchronizeDispose(object sender, EventArgs args)
        {
            if (_producer is null)
            {
                return; // The processor has already been disposed or it was never initialized.
            }

            if (sender is DataSourceScope scope)
            {
                // exit database scope entered earlier
                scope.OnCommit -= SynchronizeCommit;
                scope.OnDispose -= SynchronizeDispose;
            }
            
            ResetState();
        }
        private void ResetState()
        {
            _error = null;
            _produced = 0;
            _persisted = 0;

            try { _producer?.Dispose(); }
            catch { /* ignore errors */ }
            finally { _producer = null; }
        }
        public override void Dispose()
        {
            // This method is called by the interpreter
            // either when script execution is cancelled
            // or upon its successful completion.

            // Commit and Cancel methods will never be called
            // if processor is not bound to a data source (scoped)
        }

        private void ThrowIfStateIsBroken()
        {
            string reason = _error;

            if (reason is not null)
            {
                string message = $"[ERROR][DaJet.Kafka.Producer][{_config.ClientId}] Broken state detected: persisted {_persisted} of {_produced} messages. Reason: {reason}";

                FileLogger.Default.Write(message);

                throw new InvalidOperationException(message);
            }
        }
        private void WriteToLogSuccessfulStory()
        {
            FileLogger.Default.Write($"[SUCCESS][DaJet.Kafka.Producer][{_config.ClientId}] Produced {_persisted} messages.");
        }
        private void WriteToLogFailedToBuildProducer(in Exception error)
        {
            string message = $"[ERROR][DaJet.Kafka.Producer][{_config.ClientId}] Failed to build producer: {ExceptionHelper.GetErrorMessage(error)}";

            FileLogger.Default.Write(message);
        }
        private void WriteToLogProduceError(in ProduceException<byte[], byte[]> error)
        {
            string topic = error.DeliveryResult?.Topic;

            ErrorCode code = error.Error is null ? ErrorCode.Unknown : error.Error.Code;

            string reason = error.Error is null ? "Unknown delivery error." : error.Error.Reason;

            string message = $"[ERROR][DaJet.Kafka.Producer][{_config.ClientId}][{topic}] Persisted {_persisted} of {_produced} messages. {{{code}}} {reason}";

            FileLogger.Default.Write(message);
        }
        private void ThrowFailedToConfirmDeliveries()
        {
            string error = $"[ERROR][DaJet.Kafka.Producer][{_config.ClientId}] Failed to confirm {_produced - _persisted} of {_produced} messages.";

            string reason = _error;

            if (reason is not null)
            {
                error += " Reason: " + reason;
            }

            FileLogger.Default.Write(error);

            throw new InvalidOperationException(error);
        }
        private void WriteToLogWarningOnCancellation()
        {
            FileLogger.Default.Write($"[WARNING][DaJet.Kafka.Producer][{_config.ClientId}] Persisted {_persisted} of {_produced} messages.");
        }
    }
}