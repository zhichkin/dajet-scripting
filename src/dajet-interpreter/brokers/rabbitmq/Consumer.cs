using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Timers;
using System.Web;
using RmqConstants = RabbitMQ.Client.Constants;

namespace DaJet.RabbitMQ
{
    public sealed class Consumer : ProcessorBase
    {
        private readonly ScriptContext _context;
        private readonly ConsumeStatement _statement;
        private readonly Dictionary<string, SyntaxNode> _options = new();

        private int _state = STATE_IDLE;
        private const int STATE_IDLE = 0;
        private const int STATE_RUNNING = 1;
        private const int STATE_AUTORESET = 2;
        private const int STATE_DISPOSING = 3;
        private System.Timers.Timer _heartbeat;
        private ManualResetEventSlim _cancellation;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_RUNNING, STATE_IDLE) == STATE_IDLE; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_RUNNING) == STATE_RUNNING; } }
        private bool CanAutoReset { get { return Interlocked.CompareExchange(ref _state, STATE_AUTORESET, STATE_RUNNING) == STATE_RUNNING; } }

        private IChannel _channel;
        private IConnection _connection;
        private AsyncEventingBasicConsumer _consumer;
        private int _consumed = 0;
        private string _consumerTag;
        private readonly string _output;
        private ulong _deliveryTag;
        private List<DataObject> _batch;
        private SemaphoreSlim _batchLock;
        private System.Timers.Timer _batchTimer;
        public Consumer(in ScriptContext context, in ConsumeStatement statement)
        {
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

            if (_statement.IsStream)
            {
                DataObject message = new(9);
                _context.SetValue(_output, message);
            }
            else
            {
                _batch = new List<DataObject>(100);
                _context.SetValue(_output, _batch);
            }

            ConfigureConsumerSettings();

            ConfigureConnectionSettings();
        }
        public override ExitCode Process()
        {
            if (CanExecute)
            {
                System.Timers.Timer timer = new();

                if (Interlocked.CompareExchange(ref _heartbeat, timer, null) is not null)
                {
                    timer.Dispose();
                }
                else
                {
                    _heartbeat.AutoReset = true;
                    _heartbeat.Elapsed += EnsureConsumerIsActive;
                    _heartbeat.Interval = TimeSpan.FromSeconds(Heartbeat).TotalMilliseconds;
                }

                ManualResetEventSlim cancellation = new(false, 0);

                if (Interlocked.CompareExchange(ref _cancellation, cancellation, null) is not null)
                {
                    cancellation.Dispose();
                }

                ConfigureBatchProcessing();

                EnsureConsumerIsActive(this, null);

                _heartbeat.Start();

                try
                {
                    _cancellation.Wait(_context.GetCancellationToken()); // wait for the script to be cancelled
                }
                catch (OperationCanceledException)
                {
                    FileLogger.Default.Write("[INFO][RabbitMQ.Consumer] Script is cancelled."); throw;
                }
            }

            return ExitCode.Success;
        }
        private void ConfigureBatchProcessing()
        {
            if (_statement.IsStream)
            {
                return;
            }

            _batchLock = new SemaphoreSlim(1, 1);

            System.Timers.Timer timer = new();

            if (Interlocked.CompareExchange(ref _batchTimer, timer, null) is not null)
            {
                timer.Dispose();
            }
            else
            {
                _batchTimer.AutoReset = false;
                _batchTimer.Elapsed += ProcessBatchTimeout;
                _batchTimer.Interval = TimeSpan.FromSeconds(BatchTimeout).TotalMilliseconds;
            }
        }

        #region "CONFIGURATION AND PROCESSOR OPTIONS"
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        private string QueueName { get; set; } = string.Empty;
        private int Heartbeat { get; set; } = 60; // consumer health check (seconds)
        private uint PrefetchSize { get; set; } = 0; // size of the client buffer in bytes
        private ushort PrefetchCount { get; set; } = 1; // allowed messages on the fly without ack
        private int BatchSize { get; set; } = 1048576; // bytes
        private int BatchCount { get; set; } = 100; // message count
        private int BatchTimeout { get; set; } = 1; // seconds
        private string GetQueueName()
        {
            if (!_options.TryGetValue("QueueName", out SyntaxNode expression))
            {
                throw new InvalidOperationException("QueueName is not defined.");
            }

            object value = _context.Evaluate(in expression);

            if (value is not string queue)
            {
                throw new InvalidOperationException("QueueName must be a string.");
            }

            return queue;
        }
        private int GetHeartbeat()
        {
            if (_options.TryGetValue("Heartbeat", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int heartbeat)
                {
                    return heartbeat > 0 ? heartbeat : 60;
                }
            }

            return 60; // seconds
        }
        private uint GetPrefetchSize()
        {
            if (_options.TryGetValue("PrefetchSize", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int size)
                {
                    if (size > 0)
                    {
                        return (uint)size;
                    }
                }
            }

            return 0U; // size of the client buffer in bytes
        }
        private ushort GetPrefetchCount()
        {
            if (_options.TryGetValue("PrefetchCount", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int count)
                {
                    if (count > 0 && count <= ushort.MaxValue)
                    {
                        return (ushort)count;
                    }
                }
            }

            return 1; // allowed messages on the fly without ack
        }
        private void ConfigureConsumerSettings()
        {
            QueueName = GetQueueName();
            Heartbeat = GetHeartbeat();
            PrefetchSize = GetPrefetchSize();
            PrefetchCount = GetPrefetchCount();
        }
        private void ConfigureConnectionSettings()
        {
            Uri uri = _context.GetUri(_statement.Target);

            if (uri.Scheme != "amqp")
            {
                throw new InvalidOperationException($"[URI] amqp scheme expected");
            }

            HostName = uri.Host;
            HostPort = uri.Port;

            string[] userpass = uri.UserInfo.Split(':');

            if (userpass is not null && userpass.Length == 2)
            {
                UserName = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
                Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
            }

            if (uri.Segments is not null && uri.Segments.Length > 1)
            {
                VirtualHost = HttpUtility.UrlDecode(uri.Segments[1].TrimEnd('/'), Encoding.UTF8);
            }
        }
        #endregion
        
        private void ReportConsumerStatus()
        {
            int consumed = Interlocked.Exchange(ref _consumed, 0);

            int rate = consumed / Heartbeat;

            FileLogger.Default.Write($"[{QueueName}] Consumed {consumed} / {Heartbeat} = {rate} msg/sec");
        }
        private async Task ActivateConsumerAsync()
        {
            ConnectionFactory factory = new()
            {
                HostName = HostName,
                Port = HostPort,
                VirtualHost = VirtualHost,
                UserName = UserName,
                Password = Password
            };

            _connection = await factory.CreateConnectionAsync().ConfigureAwait(false);

            _channel = await _connection.CreateChannelAsync().ConfigureAwait(false);

            await _channel.BasicQosAsync(PrefetchSize, PrefetchCount, false).ConfigureAwait(false);

            _consumer = new AsyncEventingBasicConsumer(_channel);
            
            _consumer.ReceivedAsync += ProcessMessageAsync;

            _consumerTag = await _channel.BasicConsumeAsync(QueueName, false, _consumer, _context.GetCancellationToken()).ConfigureAwait(false);
        }
        private bool ConsumerIsHealthy()
        {
            AsyncEventingBasicConsumer consumer = _consumer;

            if (consumer is null || !consumer.IsRunning)
            {
                return false;
            }

            IChannel channel = consumer.Channel;

            return channel is not null && channel.IsOpen;
        }
        private void EnsureConsumerIsActive(object sender, ElapsedEventArgs args)
        {
            ReportConsumerStatus();

            if (CanAutoReset)
            {
                try
                {
                    if (!ConsumerIsHealthy())
                    {
                        DisposeConsumer();

                        Task activator = ActivateConsumerAsync();

                        if (!activator.IsCompleted)
                        {
                            activator.GetAwaiter().GetResult();
                        }
                    }
                }
                catch (Exception error)
                {
                    DisposeConsumer();

                    FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
                }
                finally
                {
                    // to be able to auto-reset upon the next status check or to be disposed
                    _ = Interlocked.Exchange(ref _state, STATE_RUNNING);
                }
            }
        }
        
        private async Task ProcessMessageAsync(object sender, BasicDeliverEventArgs args)
        {
            _deliveryTag = args.DeliveryTag;

            object value = _context.GetValue(_output);

            if (value is not DataObject message)
            {
                message = new DataObject(9);

                if (_statement.IsStream)
                {
                    _context.SetValue(_output, message);
                }
            }

            ProcessHeaders(in message, in args);
            message.SetValue("Body", DecodeMessageBody(args.Body));
            message.SetValue(nameof(IBasicProperties.AppId), args.BasicProperties.AppId ?? string.Empty);
            message.SetValue(nameof(IBasicProperties.ReplyTo), args.BasicProperties.ReplyTo ?? string.Empty);
            message.SetValue(nameof(IBasicProperties.MessageId), args.BasicProperties.MessageId ?? string.Empty);
            message.SetValue(nameof(IBasicProperties.CorrelationId), args.BasicProperties.CorrelationId ?? string.Empty);
            message.SetValue(nameof(IBasicProperties.Type), args.BasicProperties.Type ?? string.Empty);
            message.SetValue(nameof(IBasicProperties.ContentType), args.BasicProperties.ContentType ?? "application/json");
            message.SetValue(nameof(IBasicProperties.ContentEncoding), args.BasicProperties.ContentEncoding ?? "UTF-8");

            //NOTE: Все ошибки, возникающие в процессе обработки события Received,
            //NOTE: EventingBasicConsumer перехватывает, "проглатывает" и не падает.

            if (_statement.IsStream)
            {
                await ProcessStreamHandler();
            }
            else
            {
                await ProcessBatchHandler(message);
            }
        }
        private async Task ProcessStreamHandler()
        {
            IChannel channel = _channel;

            if (channel is null) { return; }

            ulong deliveryTag = _deliveryTag;

            if (deliveryTag == 0UL) { return; }

            bool batchMode = !_statement.IsStream;

            int consumed = batchMode ? _batch.Count : 1;

            bool success;

            try
            {
                ExitCode code = _context.Callback(_statement.Statements);

                success = (code == ExitCode.Success);
            }
            catch (Exception error)
            {
                success = false;

                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
            finally
            {
                if (batchMode)
                {
                    _batch.Clear();
                }
            }

            if (success)
            {
                await channel.BasicAckAsync(deliveryTag, batchMode).ConfigureAwait(false);

                _consumed += consumed;
            }
            else
            {
                await NackMessage(channel, deliveryTag, batchMode).ConfigureAwait(false);
            }
        }
        private async Task ProcessBatchHandler(DataObject message)
        {
            _batchTimer.Stop();
            
            _batchLock.Wait();

            _batch.Add(message);

            try
            {
                if (_batch.Count == PrefetchCount)
                {
                    await ProcessStreamHandler();
                }
            }
            finally
            {
                _batchLock.Release();
            }

            _batchTimer.Start();
        }
        private void ProcessBatchTimeout(object sender, ElapsedEventArgs args)
        {
            if (_state != STATE_RUNNING)
            {
                return;
            }

            _batchLock.Wait();

            if (_batch.Count == 0)
            {
                _batchLock.Release(); return;
            }

            try
            {
                Task task = ProcessStreamHandler();

                if (!task.IsCompleted)
                {
                    task.GetAwaiter().GetResult();
                }
            }
            finally
            {
                _batchLock.Release();
            }
        }
        private static void ProcessHeaders(in DataObject message, in BasicDeliverEventArgs args)
        {
            IDictionary<string, object> message_headers = args?.BasicProperties?.Headers;

            if (message_headers is null || message_headers.Count == 0)
            {
                message.SetValue(nameof(IBasicProperties.Headers), null); return;
            }

            DataObject headers = new(message_headers.Count);

            foreach (var header in message_headers)
            {
                if (header.Key == "CC" || header.Key == RmqConstants.PublishSequenceNumberHeader)
                {
                    continue; // Игнорируем стандартный заголовок
                }

                if (header.Value is string text)
                {
                    headers.SetValue(header.Key, HeaderSerializer.Deserialize(in text));
                }
                else if (header.Value is byte[] bytes) // this might be whatever ?
                {
                    string value = string.Empty;

                    try
                    {
                        value = Encoding.UTF8.GetString(bytes);
                    }
                    finally
                    {
                        headers.SetValue(header.Key, HeaderSerializer.Deserialize(in value));
                    }
                }
                else
                {
                    headers.SetValue(header.Key, string.Empty); // unsupported or unknown data type
                }
            }

            if (headers.Count == 0)
            {
                message.SetValue(nameof(IBasicProperties.Headers), null);
            }
            else
            {
                message.SetValue(nameof(IBasicProperties.Headers), headers);
            }
        }
        private static string DecodeMessageBody(in ReadOnlyMemory<byte> message)
        {
            return Encoding.UTF8.GetString(message.Span);
        }
        private async Task NackMessage(IChannel channel, ulong deliveryTag, bool multiple)
        {
            ManualResetEventSlim cancellation = _cancellation;

            if (cancellation is null)
            {
                return; // Consumer is highly likely already disposed
            }

            //NOTE: Требуется задержка, иначе может возникать зацикливание одного и того же сообщения:
            //NOTE: сервер Send > консюмер Nack > сервер Send > консюмер Nack ... и так далее.

            bool signaled = cancellation.Wait(TimeSpan.FromSeconds(Heartbeat), _context.GetCancellationToken());

            if (!signaled) // Consumer is still active
            {
                await channel.BasicNackAsync(deliveryTag, multiple, true).ConfigureAwait(false);
            }
        }

        public override void Dispose()
        {
            if (CanDispose)
            {
                DisposeHeartbeat();

                DisposeConsumer();

                DisposeBatchMode();

                SignalCancellation();

                _ = Interlocked.Exchange(ref _state, STATE_IDLE);
            }
            else if (_state == STATE_AUTORESET)
            {
                Thread.Sleep(1); Dispose(); //FIXME: wait on event !?
            }
        }
        private void DisposeConsumer()
        {
            try
            {
                _consumer?.ReceivedAsync -= ProcessMessageAsync;
                _ = _channel?.BasicCancelAsync(_consumerTag);
            }
            catch { /* IGNORE */ }
            finally { _consumer = null; }

            try { _channel?.Dispose(); }
            catch { /* IGNORE */ }
            finally { _channel = null; }

            try { _connection?.Dispose(); }
            catch { /* IGNORE */ }
            finally { _connection = null; }

            _consumerTag = null;
        }
        private void DisposeBatchMode()
        {
            if (_statement.IsStream)
            {
                return;
            }

            try
            {
                _batchTimer?.Stop();
                _batchTimer?.Dispose();
            }
            finally
            {
                _batchTimer = null;
            }

            try { _batchLock?.Dispose(); }
            finally { _batchLock = null; }
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
        private void SignalCancellation()
        {
            try
            {
                _cancellation?.Set();
                _cancellation?.Dispose();
            }
            finally
            {
                _cancellation = null;
            }
        }
    }
}