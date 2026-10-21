using Confluent.Kafka;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using System.Text;
using System.Timers;
using Error = Confluent.Kafka.Error;

namespace DaJet.Kafka
{
    public sealed class Consumer : ProcessorBase
    {
        private readonly ScriptContext _context;
        private readonly ConsumeStatement _statement;
        private readonly string _output;

        private int _consumed = 0;
        private System.Timers.Timer _heartbeat;

        private int _state;
        private const int STATE_IDLE = 0;
        private const int STATE_ACTIVE = 1;
        private const int STATE_DISPOSING = 2;
        private CancellationTokenSource _consumeLoop;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_IDLE) == STATE_IDLE; } }
        private bool IsActive { get { return Interlocked.CompareExchange(ref _state, STATE_ACTIVE, STATE_ACTIVE) == STATE_ACTIVE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_ACTIVE) == STATE_ACTIVE; } }

        private readonly string _topic;
        private readonly string _proto_key;
        private readonly string _proto_value;
        private ConsumerConfig _config;
        private readonly Dictionary<string, SyntaxNode> _options = new();
        private readonly Action<IConsumer<byte[], byte[]>, Error> _errorHandler;
        private readonly Action<IConsumer<byte[], byte[]>, LogMessage> _logHandler;
        private readonly Action<IConsumer<byte[], byte[]>, List<TopicPartition>> _partitionsAssignedHandler;
        private readonly Action<IConsumer<byte[], byte[]>, List<TopicPartitionOffset>> _partitionsRevokedHandler;
        public Consumer(in ScriptContext context, in ConsumeStatement statement)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentNullException.ThrowIfNull(statement, nameof(statement));

            _context = context;
            _statement = statement;

            foreach (ColumnExpression column in _statement.Options)
            {
                _options.Add(column.Alias, column.Expression);
            }

            if (_statement.Into.Value is VariableReference variable)
            {
                _output = variable.Identifier;
            }

            _topic = GetTopic();
            _proto_key = GetProtoKey();
            _proto_value = GetProtoValue();
            _logHandler = LogHandler;
            _errorHandler = ErrorHandler;
            _partitionsRevokedHandler = PartitionsRevokedHandler;
            _partitionsAssignedHandler = PartitionsAssignedHandler;

            _config = CreateConsumerConfig();
        }
        private ConsumerConfig CreateConsumerConfig()
        {
            Dictionary<string, string> config = new();

            foreach (KeyValuePair<string, SyntaxNode> option in _options)
            {
                string key = option.Key;

                if (key == "topic" || key == "proto.key" || key == "proto.value")
                {
                    continue;
                }

                SyntaxNode expression = option.Value;

                object value = _context.Evaluate(in expression);

                config.Add(option.Key, value.ToString());
            }

            return new ConsumerConfig(config);
        }
        private string GetTopic()
        {
            if (!_options.TryGetValue("topic", out SyntaxNode expression))
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
        private string GetProtoKey()
        {
            if (_options.TryGetValue("proto.key", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is not string text)
                {
                    throw new InvalidOperationException("Option \"proto.key\" must be a string.");
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Option \"proto.key\" value is empty.");
                }

                return text;
            }
            
            return string.Empty;
        }
        private string GetProtoValue()
        {
            if (_options.TryGetValue("proto.value", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is not string text)
                {
                    throw new InvalidOperationException("Option \"proto.value\" must be a string.");
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Option \"proto.value\" value is empty.");
                }

                return text;
            }

            return string.Empty;
        }
        
        public override ExitCode Process()
        {
            if (CanExecute) // STATE_IDLE -> STATE_ACTIVE
            {
                StartHeartbeat();

                while (IsActive) // STATE_ACTIVE
                {
                    _consumeLoop = new CancellationTokenSource();

                    CancellationToken cancellationToken = _consumeLoop.Token;

                    RunConsumeLoop(cancellationToken); // Kafka consumer infinite poll loop

                    if (cancellationToken.IsCancellationRequested) // Dispose is called during poll loop
                    {
                        break; // exit processor working loop
                    }

                    try
                    {
                        FileLogger.Default.Write($"[{_topic}] Sleep 60 seconds ...");

                        Task.Delay(TimeSpan.FromSeconds(60)).Wait(cancellationToken);
                    }
                    catch // (OperationCanceledException)
                    {
                        // do nothing - cancellation requested
                    }

                    if (cancellationToken.IsCancellationRequested) // Dispose is called during sleep
                    {
                        break; // exit processor working loop
                    }

                    try { _consumeLoop.Dispose(); }
                    finally { _consumeLoop = null; }
                }

                // STATE_DISPOSING

                DisposeHeartbeat();

                try { _consumeLoop?.Dispose(); }
                finally { _consumeLoop = null; }
                
                _ = Interlocked.Exchange(ref _state, STATE_IDLE); // STATE_DISPOSING -> STATE_IDLE
            }

            return ExitCode.Success;
        }

        private void StartHeartbeat()
        {
            _heartbeat = new System.Timers.Timer
            {
                AutoReset = true
            };
            _heartbeat.Elapsed += ReportConsumerStatus;
            _heartbeat.Interval = TimeSpan.FromSeconds(60).TotalMilliseconds;
            _heartbeat.Start();
        }
        private void ReportConsumerStatus(object sender, ElapsedEventArgs args)
        {
            int consumed = Interlocked.Exchange(ref _consumed, 0);

            FileLogger.Default.Write($"[{_topic}] Consumed {consumed} messages");
        }
        private void DisposeHeartbeat()
        {
            try
            {
                _heartbeat?.Stop();
                _heartbeat?.Dispose();
            }
            finally
            {
                _heartbeat = null;
            }
        }

        private void LogHandler(IConsumer<byte[], byte[]> _, LogMessage log)
        {
            FileLogger.Default.Write($"[{_topic}][{log.Name}]: {log.Message}");
        }
        private void ErrorHandler(IConsumer<byte[], byte[]> consumer, Error error)
        {
            if (error.IsFatal)
            {
                //TODO: !?
            }

            FileLogger.Default.Write($"[{_topic}][{consumer.Name}][{string.Concat(consumer.Subscription)}]: {error.Reason}");
        }
        private void PartitionsAssignedHandler(IConsumer<byte[], byte[]> consumer, List<TopicPartition> partitions)
        {
            StringBuilder text = new();

            if (partitions is null || partitions.Count == 0)
            {
                text.Append("none");
            }
            else
            {
                foreach (TopicPartition partition in partitions)
                {
                    text.Append('[').Append(partition.Partition.Value).Append(']');
                }
            }

            FileLogger.Default.Write($"[{_topic}][{consumer.Name}] partitions assigned: {text}");
        }
        private void PartitionsRevokedHandler(IConsumer<byte[], byte[]> consumer, List<TopicPartitionOffset> partitions)
        {
            StringBuilder text = new();

            if (partitions is null || partitions.Count == 0)
            {
                text.Append("none");
            }
            else
            {
                foreach (TopicPartitionOffset partition in partitions)
                {
                    string offset = partition.Offset.Value < 0 ? "-" : partition.Offset.Value.ToString();

                    text.Append($"[{partition.Partition.Value}:{offset}]");
                }
            }

            FileLogger.Default.Write($"[{_topic}][{consumer.Name}] partitions revoked: {text}");
        }

        private void RunConsumeLoop(CancellationToken cancellationToken)
        {
            ConsumeResult<byte[], byte[]> result;

            using (IConsumer<byte[], byte[]> consumer = new ConsumerBuilder<byte[], byte[]>(_config)
                .SetLogHandler(_logHandler)
                .SetErrorHandler(_errorHandler)
                .SetPartitionsRevokedHandler(_partitionsRevokedHandler)
                .SetPartitionsAssignedHandler(_partitionsAssignedHandler)
                .Build())
            {
                try
                {
                    consumer.Subscribe(_topic);
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write($"[{_topic}] Failed to subscribe: {ExceptionHelper.GetErrorMessage(error)}");

                    return; // exit consume loop
                }

                string offsetInfo = "-";

                while (!cancellationToken.IsCancellationRequested) // Kafka consumer infinite poll loop
                {
                    try
                    {
                        result = consumer.Consume(cancellationToken);

                        if (result is null || result.Message is null)
                        {
                            break; // no more messages to process - exit consume loop
                        }
                        else
                        {
                            offsetInfo = $"{result.Partition.Value}:{result.Offset.Value}";
                        }
                    }
                    catch (ConsumeException error)
                    {
                        ConsumeResult<byte[], byte[]> record = error.ConsumerRecord;

                        if (record is not null)
                        {
                            if (record.Offset.Value < 0)
                            {
                                offsetInfo = $"{record.Partition.Value}:-";
                            }
                            else
                            {
                                offsetInfo = $"{record.Partition.Value}:{record.Offset.Value}";
                            }
                        }

                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Consume error: [{error.Error.Code}] {error.Error.Reason}");

                        break; // exit consume loop
                    }
                    catch (OperationCanceledException)
                    {
                        FileLogger.Default.Write($"[{_topic}] Consumer shutdown requested");

                        break; // exit consume loop
                    }
                    catch (Exception exception)
                    {
                        FileLogger.Default.Write($"[{_topic}] Unexpected consume error: {ExceptionHelper.GetErrorMessage(exception)}");

                        break; // exit consume loop
                    }

                    try
                    {
                        ProcessMessage(in result);
                    }
                    catch (Exception error)
                    {
                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Process message error: {ExceptionHelper.GetErrorMessage(error)}");

                        // ? Manual offset management: _consumer.Seek(_result.TopicPartitionOffset);

                        break; // exit consume loop
                    }
                    
                    try
                    {
                        ExitCode code = _context.Callback(_statement.Statements);

                        if (code != ExitCode.Success)
                        {
                            FileLogger.Default.Write($"[{_topic}][{offsetInfo}] CONSUME message processing code returned exit code: {code}");

                            break; // exit consume loop
                        }
                    }
                    catch (Exception error)
                    {
                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Failed to process message: {ExceptionHelper.GetErrorMessage(error)}");

                        break; // exit consume loop
                    }

                    try
                    {
                        consumer.Commit(result); //THINK: Use EnableAutoOffsetStore = false !?
                    }
                    catch (Exception error)
                    {
                        //THINK: _consumer.Seek(_result.TopicPartitionOffset);

                        FileLogger.Default.Write($"[{_topic}][{offsetInfo}] Commit message error: {ExceptionHelper.GetErrorMessage(error)}");

                        break; // exit consume loop
                    }

                    _ = Interlocked.Increment(ref _consumed);
                }

                try
                {
                    consumer.Close(); // leave consumer group
                }
                catch (KafkaException error)
                {
                    FileLogger.Default.Write($"[{_topic}] Close consumer error: [{error.Error.Code}] {error.Error.Reason}");
                }
            }
        }
        private void ProcessMessage(in ConsumeResult<byte[], byte[]> result)
        {
            object target = _context.GetValue(_output);

            if (target is DataObject message)
            {
                if (string.IsNullOrEmpty(result.Topic))
                {
                    message.SetValue("Topic", _topic);
                }
                else
                {
                    message.SetValue("Topic", result.Topic);
                }

                if (result.Message.Key is null)
                {
                    message.SetValue("Key", string.Empty);
                }
                else
                {
                    string key = string.Empty;

                    if (string.IsNullOrEmpty(_proto_key))
                    {
                        key = Encoding.UTF8.GetString(result.Message.Key);
                    }
                    else
                    {
                        //key = ProtobufConverter.ConvertProtobufToJson(in _proto_key, result.Message.Key);
                    }

                    message.SetValue("Key", key);
                }

                if (result.Message.Value is null)
                {
                    message.SetValue("Value", string.Empty);
                }
                else
                {
                    string value = string.Empty;

                    if (string.IsNullOrEmpty(_proto_value))
                    {
                        value = Encoding.UTF8.GetString(result.Message.Value);
                    }
                    else
                    {
                        //value = ProtobufConverter.ConvertProtobufToJson(in _proto_value, result.Message.Value);
                    }

                    message.SetValue("Value", value);
                }
            }
        }

        public override void Dispose()
        {
            if (CanDispose) // STATE_ACTIVE -> STATE_DISPOSING
            {
                try
                {
                    _consumeLoop?.Cancel(); // request consume loop cancellation
                }
                catch // (ObjectDisposedException)
                {
                    // do nothing
                }

                try // wait to exit consume loop and dispose resources
                {
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                }
                finally //TODO: use ManualResetEvent ???
                {
                    // do nothing
                }
            }
        }
    }
}