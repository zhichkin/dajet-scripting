using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Web;
using RmqConstants = RabbitMQ.Client.Constants;

namespace DaJet.RabbitMQ
{
    public sealed class Producer : ProcessorBase
    {
        #region "CONSTANTS"
        private const string ERROR_STATE_IS_BROKEN = "Broken state";
        private const string WARNING_FLOW_CONTROL = "Flow control: {0}";
        private const string ERROR_CHANNEL_SHUTDOWN = "Channel shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_SHUTDOWN = "Connection shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_IS_BLOCKED = "Connection blocked: {0}";
        private const string ERROR_FAILED_TO_ACK = "Failed to confirm delivery tag: {0} ({1})";
        private const string ERROR_WAIT_FOR_CONFIRMS = "Wait for confirms timed out";
        private const string ERROR_PUBLISHER_CONFIRMS = "Publisher confirms nacked";
        private const string HEADER_CC = "CC";
        private const string HEADER_BCC = "BCC";
        #endregion

        private bool _disposed;
        private readonly ScriptContext _context;
        private readonly DataSourceScope _scope;
        private readonly ProduceStatement _statement;
        private readonly Dictionary<string, SyntaxNode> _select = new();

        private byte[] _buffer;
        private IChannel _channel;
        private IConnection _connection;
        private BasicProperties _properties;
        private TaskCompletionSource _state;
        private readonly Lock _confirmLock = new();
        private readonly CountdownEvent _confirmCountdown = new(0);
        private readonly ConcurrentDictionary<ulong, bool> _published = new(2, 1000);
        public Producer(in ScriptContext context, in ProduceStatement statement)
        {
            _context = context;
            _statement = statement;
            
            foreach (ColumnExpression column in _statement.Columns)
            {
                _select.Add(column.Alias, column.Expression);
            }

            ConfigureConnectionSettings();

            PublisherConfirmsTimeout = GetPublisherConfirmsTimeout();
        }
        public override ExitCode Process()
        {
            if (_context.IsCancellationRequested)
            {
                return ExitCode.Cancel;
            }

            ExitCode code = ExitCode.Success;

            if (_state is null)
            {
                _state = new TaskCompletionSource();

                if (_context.GetDataSource() is DataSourceScope scope)
                {
                    //_scope = scope;
                    scope.OnCommit += SynchronizeCommit;
                    scope.OnCancel += SynchronizeCancel;
                    scope.OnDispose += SynchronizeDispose;
                }
            }
            
            try
            {
                ThrowIfStateIsBroken();

                EnsureProcessorIsActive();

                PublishMessageOrThrow();
            }
            catch
            {
                ResetState(); throw;
            }

            return code;
        }
        private void ResetState()
        {
            if (_state is null)
            {
                return;
            }

            _state = null;
            _properties = null;
            _published.Clear();
            _confirmCountdown.Reset(0);

            IChannel channel = _channel;

            if (channel is not null)
            {
                channel.BasicAcksAsync -= BasicAcksHandler;
                channel.BasicNacksAsync -= BasicNacksHandler;
                channel.BasicReturnAsync -= BasicReturnHandler;
                channel.ChannelShutdownAsync -= ChannelShutdownHandler;

                try { channel.Dispose(); }
                catch { /* do nothing */ }
                finally { _channel = null; }
            }

            IConnection connection = _connection;

            if (connection is not null)
            {
                connection.ConnectionBlockedAsync -= HandleConnectionBlocked;
                connection.ConnectionUnblockedAsync -= HandleConnectionUnblocked;
                connection.ConnectionShutdownAsync -= ConnectionShutdownHandler;

                try { connection.Dispose(); }
                catch { /* do nothing */ }
                finally { _connection = null; }
            }

            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer, true);

                _buffer = null;
            }
        }

        #region "CONFIGURATION OPTIONS"
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
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
        private TimeSpan PublisherConfirmsTimeout { get; set; } = TimeSpan.FromSeconds(10);
        private TimeSpan GetPublisherConfirmsTimeout()
        {
            if (_select.TryGetValue("PublisherConfirmsTimeout", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int seconds)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }

            return TimeSpan.FromSeconds(10);
        }
        private TimeSpan GetRequestedHeartbeat()
        {
            if (_select.TryGetValue("RequestedHeartbeat", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int seconds)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }

            return TimeSpan.FromSeconds(60);
        }
        private bool GetAutomaticRecoveryEnabled()
        {
            if (_select.TryGetValue("AutomaticRecoveryEnabled", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is bool boolean)
                {
                    return boolean;
                }
            }

            return true;
        }
        private TimeSpan GetNetworkRecoveryInterval()
        {
            if (_select.TryGetValue("NetworkRecoveryInterval", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int seconds)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }

            return TimeSpan.FromSeconds(5);
        }
        private TimeSpan GetContinuationTimeout()
        {
            if (_select.TryGetValue("ContinuationTimeout", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int seconds)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }

            return TimeSpan.FromSeconds(30);
        }
        private TimeSpan GetRequestedConnectionTimeout()
        {
            if (_select.TryGetValue("RequestedConnectionTimeout", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is int seconds)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }

            return TimeSpan.FromSeconds(30);
        }
        #endregion

        #region "MESSAGE OPTIONS AND VALUES"
        private bool GetMandatory()
        {
            if (_select.TryGetValue("Mandatory", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is bool boolean)
                {
                    return boolean;
                }
            }

            return false;
        }
        private string GetExchange()
        {
            if (_select.TryGetValue("Exchange", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return string.Empty;
        }
        private string GetRoutingKey()
        {
            if (_select.TryGetValue("RoutingKey", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return string.Empty;
        }
        private string GetMessageBody()
        {
            if (_select.TryGetValue("Body", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return string.Empty;
        }
        private DataObject GetHeaders()
        {
            if (_select.TryGetValue("Headers", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is DataObject headers)
                {
                    return headers;
                }
            }

            return null;
        }
        private string[] GetBlindCopy()
        {
            if (_select.TryGetValue("BlindCopy", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is List<DataObject> list && list.Count > 0)
                {
                    string[] array = new string[list.Count];

                    for (int i = 0; i < list.Count; i++)
                    {
                        array[i] = list[i].GetValue(0).ToString();
                    }

                    return array;
                }
                else
                {
                    return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
            }
            
            return null;
        }
        private string[] GetCarbonCopy()
        {
            if (_select.TryGetValue("CarbonCopy", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is List<DataObject> list && list.Count > 0)
                {
                    string[] array = new string[list.Count];

                    for (int i = 0; i < list.Count; i++)
                    {
                        array[i] = list[i].GetValue(0).ToString();
                    }

                    return array;
                }
                else
                {
                    return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
            }

            return null;
        }
        private string GetAppId()
        {
            if (_select.TryGetValue("AppId", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return null;
        }
        private string GetMessageId()
        {
            if (_select.TryGetValue("MessageId", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return null;
        }
        private string GetMessageType()
        {
            if (_select.TryGetValue("Type", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return null;
        }
        private string GetCorrelationId()
        {
            if (_select.TryGetValue("CorrelationId", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return null;
        }
        private byte GetPriority()
        {
            if (_select.TryGetValue("Priority", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is byte priority)
                {
                    return priority;
                }
            }

            return (byte)0;
        }
        private string GetContentType()
        {
            if (_select.TryGetValue("ContentType", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return "application/json";
        }
        private string GetContentEncoding()
        {
            if (_select.TryGetValue("ContentEncoding", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return "UTF-8";
        }
        private string GetReplyTo()
        {
            if (_select.TryGetValue("ReplyTo", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return null;
        }
        private string GetExpiration()
        {
            if (_select.TryGetValue("Expiration", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is string text)
                {
                    return text;
                }
            }

            return null;
        }
        private DeliveryModes GetDeliveryMode()
        {
            if (_select.TryGetValue("DeliveryMode", out SyntaxNode expression))
            {
                object value = _context.Evaluate(in expression);

                if (value is byte mode)
                {
                    return mode == 1 ? DeliveryModes.Transient : DeliveryModes.Persistent;
                }
            }

            return DeliveryModes.Persistent;
        }
        #endregion

        #region "CONNECTION AND CHANNEL MANAGEMENT"
        private void ThrowIfStateIsBroken()
        {
            TaskCompletionSource state = _state;

            if (state is null || state.Task.IsFaulted)
            {
                throw new InvalidOperationException(ERROR_STATE_IS_BROKEN);
            }
        }
        private void EnsureProcessorIsActive()
        {
            IChannel channel = _channel;

            if (channel is not null)
            {
                if (channel.IsOpen)
                {
                    return;
                }

                throw new InvalidOperationException(ERROR_STATE_IS_BROKEN);
            }

            Task activator = ActivateProcessorAsync();

            if (!activator.IsCompleted)
            {
                activator.GetAwaiter().GetResult();
            }
        }
        private async Task ActivateProcessorAsync()
        {
            await InitializeConnection().ConfigureAwait(false);

            await InitializeChannel().ConfigureAwait(false);
        }
        private async Task InitializeConnection()
        {
            ConnectionFactory factory = new()
            {
                HostName = HostName,
                Port = HostPort,
                VirtualHost = VirtualHost,
                UserName = UserName,
                Password = Password,
                RequestedHeartbeat = GetRequestedHeartbeat(),
                ContinuationTimeout = GetContinuationTimeout(),
                RequestedConnectionTimeout = GetRequestedConnectionTimeout(),
                AutomaticRecoveryEnabled = GetAutomaticRecoveryEnabled(),
                NetworkRecoveryInterval = GetNetworkRecoveryInterval()
            };

            _connection = await factory.CreateConnectionAsync().ConfigureAwait(false);

            _connection.ConnectionBlockedAsync += HandleConnectionBlocked;
            _connection.ConnectionUnblockedAsync += HandleConnectionUnblocked;
            _connection.ConnectionShutdownAsync += ConnectionShutdownHandler;
        }
        private Task HandleConnectionBlocked(object sender, ConnectionBlockedEventArgs args)
        {
            string message = string.Format(ERROR_CONNECTION_IS_BLOCKED, args.Reason);

            _ = _state?.TrySetException(new Exception(message));

            FileLogger.Default.Write(message);

            return Task.CompletedTask;
        }
        private Task HandleConnectionUnblocked(object sender, AsyncEventArgs args)
        {
            FileLogger.Default.Write("Connection unblocked");

            return Task.CompletedTask;
        }
        private Task ConnectionShutdownHandler(object sender, ShutdownEventArgs args)
        {
            string message = string.Format(ERROR_CONNECTION_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText);

            _ = _state?.TrySetException(new Exception(message));

            FileLogger.Default.Write(message);

            return Task.CompletedTask;
        }
        private async Task InitializeChannel()
        {
            _properties = new BasicProperties()
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "UTF-8"
            };

            CreateChannelOptions options = new(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: false);

            _channel = await _connection.CreateChannelAsync(options).ConfigureAwait(false);

            _channel.BasicAcksAsync += BasicAcksHandler;
            _channel.BasicNacksAsync += BasicNacksHandler;
            _channel.BasicReturnAsync += BasicReturnHandler;
            _channel.FlowControlAsync += FlowControlHandler;
            _channel.ChannelShutdownAsync += ChannelShutdownHandler;
        }
        private Task FlowControlHandler(object sender, FlowControlEventArgs args)
        {
            string message = string.Format(WARNING_FLOW_CONTROL, args.Active);

            FileLogger.Default.Write(message);

            return Task.CompletedTask;
        }
        private Task ChannelShutdownHandler(object sender, ShutdownEventArgs args)
        {
            string message = string.Format(ERROR_CHANNEL_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText);

            _ = _state?.TrySetException(new Exception(message));

            FileLogger.Default.Write(message);

            return Task.CompletedTask;
        }
        #endregion

        #region "PUBLISH MESSAGE SYNC-OVER-ASYNC"
        private void PublishMessageOrThrow()
        {
            Task publisher = PublishMessageAsync();

            if (!publisher.IsCompleted)
            {
                publisher.GetAwaiter().GetResult();
            }

            if (_confirmCountdown.IsSet)
            {
                _confirmCountdown.Reset(1);
            }
            else
            {
                _confirmCountdown.AddCount();
            }
        }
        private async Task PublishMessageAsync()
        {
            ulong deliveryTag = await _channel.GetNextPublishSequenceNumberAsync().ConfigureAwait(false);
            
            ConfigureMessageProperties();

            ConfigureMessageHeaders(deliveryTag);

            ReadOnlyMemory<byte> payload = EncodeMessageBody(GetMessageBody());

            try
            {
                if (!_published.TryAdd(deliveryTag, false))
                {
                    throw new InvalidOperationException($"Failed to track the publisher confirmation for sequence number '{deliveryTag}' because it already exists.");
                }

                if (string.IsNullOrWhiteSpace(GetExchange()))
                {
                    // clear CC and BCC headers if present
                    _ = _properties?.Headers?.Remove(HEADER_CC); // carbon copy
                    _ = _properties?.Headers?.Remove(HEADER_BCC); // blind carbon copy

                    // send message directly to the specified queue (default exchange)
                    await _channel.BasicPublishAsync(string.Empty, GetRoutingKey(), GetMandatory(), _properties, payload).ConfigureAwait(false);
                }
                else if (string.IsNullOrWhiteSpace(GetRoutingKey()))
                {
                    // send message to the specified exchange without routing key
                    await _channel.BasicPublishAsync(GetExchange(), string.Empty, GetMandatory(), _properties, payload).ConfigureAwait(false);
                }
                else
                {
                    // send message to the specified exchange using provided routing key
                    await _channel.BasicPublishAsync(GetExchange(), GetRoutingKey(), GetMandatory(), _properties, payload).ConfigureAwait(false);
                }
            }
            catch
            {
                _ = _published.TryRemove(deliveryTag, out _);

                throw;
            }
        }
        private void ConfigureMessageHeaders(ulong deliveryTag)
        {
            _properties.Headers?.Clear();

            _properties.Headers ??= new Dictionary<string, object>();

            _properties.Headers.Add(RmqConstants.PublishSequenceNumberHeader, (long)deliveryTag);

            DataObject headers = GetHeaders();
            string[] BlindCopy = GetBlindCopy();
            string[] CarbonCopy = GetCarbonCopy();

            if (headers is null && BlindCopy is null && CarbonCopy is null)
            {
                return;
            }
            
            if (BlindCopy is not null)
            {
                _ = _properties.Headers.TryAdd(HEADER_BCC, BlindCopy);
            }

            if (CarbonCopy is not null)
            {
                _ = _properties.Headers.TryAdd(HEADER_CC, CarbonCopy);
            }

            if (headers is not null)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    string key = headers.GetName(i);
                    object value = headers.GetValue(i);

                    string text = HeaderSerializer.Serialize(in value);
                    byte[] utf8 = Encoding.UTF8.GetBytes(text);
                    _ = _properties.Headers.TryAdd(key, utf8);
                }
            }
        }
        private void ConfigureMessageProperties()
        {
            _properties.AppId = GetAppId();
            _properties.MessageId = GetMessageId();
            _properties.Type = GetMessageType();
            _properties.CorrelationId = GetCorrelationId();
            _properties.Priority = GetPriority();
            _properties.DeliveryMode = GetDeliveryMode();
            _properties.ContentType = GetContentType();
            _properties.ContentEncoding = GetContentEncoding();
            _properties.ReplyTo = GetReplyTo();
            _properties.Expiration = GetExpiration();
        }
        private ReadOnlyMemory<byte> EncodeMessageBody(in string message)
        {
            int bufferSize = message.Length * 2; // char == 2 bytes

            if (_buffer is null)
            {
                _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            }
            else if (_buffer.Length < bufferSize)
            {
                ArrayPool<byte>.Shared.Return(_buffer);

                _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            }

            int encoded = Encoding.UTF8.GetBytes(message, 0, message.Length, _buffer, 0);

            ReadOnlyMemory<byte> payload = new(_buffer, 0, encoded);

            return payload;
        }
        #endregion

        #region "MESSAGE DELIVERY TRACKING"
        private Task BasicAcksHandler(object sender, BasicAckEventArgs args)
        {
            bool multiple = args.Multiple;
            ulong deliveryTag = args.DeliveryTag;

            lock (_confirmLock) // !?
            {
                if (multiple)
                {
                    int count = 0;

                    foreach (KeyValuePair<ulong, bool> pending in _published.ToArray())
                    {
                        if (pending.Key <= deliveryTag)
                        {
                            if (_published.TryRemove(pending.Key, out _))
                            {
                                count++;
                            }
                            else
                            {
                                FileLogger.Default.Write(string.Format(ERROR_FAILED_TO_ACK, deliveryTag, "multiple"));
                            }
                        }
                    }

                    _ = _confirmCountdown.Signal(count);
                }
                else
                {
                    if (_published.TryRemove(deliveryTag, out _))
                    {
                        _ = _confirmCountdown.Signal();
                    }
                    else
                    {
                        FileLogger.Default.Write(string.Format(ERROR_FAILED_TO_ACK, deliveryTag, "single"));
                    }
                }
            }

            return Task.CompletedTask;
        }
        private Task BasicNacksHandler(object sender, BasicNackEventArgs args)
        {
            _ = _state?.TrySetException(new Exception(ERROR_PUBLISHER_CONFIRMS));

            return Task.CompletedTask;
        }
        private static string GetReturnReason(in BasicReturnEventArgs args)
        {
            return "Message return (" + args.ReplyCode.ToString() + "): " +
                (string.IsNullOrWhiteSpace(args.ReplyText) ? "(empty)" : args.ReplyText) + ". " +
                "Exchange: " + (string.IsNullOrWhiteSpace(args.Exchange) ? "(empty)" : args.Exchange) + ". " +
                "RoutingKey: " + (string.IsNullOrWhiteSpace(args.RoutingKey) ? "(empty)" : args.RoutingKey) + ".";
        }
        private Task BasicReturnHandler(object sender, BasicReturnEventArgs args)
        {
            string message = GetReturnReason(in args);

            _ = _state?.TrySetException(new Exception(message));

            FileLogger.Default.Write(message);

            return Task.CompletedTask;
        }
        private void WaitForPublisherConfirms()
        {
            TaskCompletionSource state = _state;

            if (state is null)
            {
                throw new OperationCanceledException(ERROR_STATE_IS_BROKEN);
            }

            if (state.Task.IsCompletedSuccessfully)
            {
                return;
            }

            TimeSpan timeout = PublisherConfirmsTimeout;

            //bool timedout = state.Task.Wait(timeout, _context.Cancellation);

            bool success = _confirmCountdown.Wait(timeout, _context.Cancellation);

            if (success)
            {
                _ = state.TrySetResult();
            }
            else
            {
                throw new OperationCanceledException(ERROR_WAIT_FOR_CONFIRMS);
            }

            if (!state.Task.IsCompletedSuccessfully)
            {
                throw new OperationCanceledException(ERROR_PUBLISHER_CONFIRMS);
            }
        }
        #endregion

        private void SynchronizeCommit(object sender, EventArgs args)
        {
            if (_context.IsCancellationRequested)
            {
                return;
            }

            try
            {
                ThrowIfStateIsBroken();

                WaitForPublisherConfirms();
            }
            finally
            {
                ResetState();
            }
        }
        private void SynchronizeCancel(object sender, EventArgs args)
        {
            if (_context.IsCancellationRequested)
            {
                return;
            }

            ResetState();
        }
        private void SynchronizeDispose(object sender, EventArgs args)
        {
            ResetState();
        }
        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            //DataSourceScope scope = _scope;

            //if (scope is not null)
            //{
            //    scope.OnCommit -= SynchronizeCommit;
            //    scope.OnCancel -= SynchronizeCancel;
            //    scope.OnDispose -= SynchronizeDispose;
            //}

            _disposed = true;
        }
    }
}