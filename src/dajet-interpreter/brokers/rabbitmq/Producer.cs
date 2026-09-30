using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
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

        private byte[] _buffer;
        private IChannel _channel;
        private IConnection _connection;
        private BasicProperties _properties;
        private TaskCompletionSource _state; // batch state
        private ulong _deliveryTag; // current tag published
        private ulong _trackingTag; // the last tag to wait ack for
        private readonly ConcurrentDictionary<ulong, bool> _published = new(2, 1000);
        public Producer(in ScriptContext context, in ProduceStatement statement)
        {
            _context = context;
            _statement = statement;

            if (context.GetDataSource() is DataSourceScope scope)
            {
                _scope = scope;
                _scope.OnCommit += SynchronizeCommit;
                _scope.OnCancel += SynchronizeCancel;
                _scope.OnDispose += SynchronizeDispose;
            }

            //InitializeUri();

            //PublisherConfirmsTimeout = GetPublisherConfirmsTimeout();
        }
        public override ExitCode Process()
        {
            if (_context.IsCancellationRequested)
            {
                return ExitCode.Cancel;
            }

            ExitCode code = ExitCode.Success;

            _state ??= new TaskCompletionSource();

            try
            {
                ThrowIfStateIsBroken();

                EnsureProcessorIsActive();

                ValueTask<ulong> publisher = PublishMessageAsync();

                if (publisher.IsCompleted)
                {
                    _deliveryTag = publisher.Result;
                }
                else
                {
                    _deliveryTag = publisher.GetAwaiter().GetResult();
                }

                if (!_published.TryAdd(_deliveryTag, false))
                {
                    throw new InvalidOperationException($"Failed to track the publisher confirmation for sequence number '{_deliveryTag}' because it already exists.");
                }
            }
            catch
            {
                ResetState(); throw;
            }

            return code;
        }

        //private void InitializeUri()
        //{
        //    Uri uri = _scope.GetUri(_options.Target);

        //    if (uri.Scheme != "amqp")
        //    {
        //        throw new InvalidOperationException($"[URI] amqp scheme expected");
        //    }

        //    HostName = uri.Host;
        //    HostPort = uri.Port;

        //    string[] userpass = uri.UserInfo.Split(':');

        //    if (userpass is not null && userpass.Length == 2)
        //    {
        //        UserName = HttpUtility.UrlDecode(userpass[0], Encoding.UTF8);
        //        Password = HttpUtility.UrlDecode(userpass[1], Encoding.UTF8);
        //    }

        //    if (uri.Segments is not null && uri.Segments.Length > 1)
        //    {
        //        VirtualHost = HttpUtility.UrlDecode(uri.Segments[1].TrimEnd('/'), Encoding.UTF8);
        //    }
        //}

        #region "CONFIGURATION OPTIONS"
        private string HostName { get; set; } = "localhost";
        private int HostPort { get; set; } = 5672;
        private string VirtualHost { get; set; } = "/";
        private string UserName { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        private TimeSpan PublisherConfirmsTimeout { get; set; } = TimeSpan.FromSeconds(10);
        private TimeSpan GetPublisherConfirmsTimeout()
        {
            //if (StreamFactory.TryGetOption(in _scope, "PublisherConfirmsTimeout", out object value))
            //{
            //    if (value is int seconds)
            //    {
            //        return TimeSpan.FromSeconds(seconds);
            //    }
            //}

            return TimeSpan.FromSeconds(10);
        }
        private TimeSpan GetRequestedHeartbeat()
        {
            //if (StreamFactory.TryGetOption(in _scope, "RequestedHeartbeat", out object value))
            //{
            //    if (value is int seconds)
            //    {
            //        return TimeSpan.FromSeconds(seconds);
            //    }
            //}

            return TimeSpan.FromSeconds(60);
        }
        private bool GetAutomaticRecoveryEnabled()
        {
            //if (StreamFactory.TryGetOption(in _scope, "AutomaticRecoveryEnabled", out object value))
            //{
            //    if (value is bool boolean)
            //    {
            //        return boolean;
            //    }
            //}

            return true;
        }
        private TimeSpan GetNetworkRecoveryInterval()
        {
            //if (StreamFactory.TryGetOption(in _scope, "NetworkRecoveryInterval", out object value))
            //{
            //    if (value is int seconds)
            //    {
            //        return TimeSpan.FromSeconds(seconds);
            //    }
            //}

            return TimeSpan.FromSeconds(5);
        }
        private TimeSpan GetContinuationTimeout()
        {
            //if (StreamFactory.TryGetOption(in _scope, "ContinuationTimeout", out object value))
            //{
            //    if (value is int seconds)
            //    {
            //        return TimeSpan.FromSeconds(seconds);
            //    }
            //}

            return TimeSpan.FromSeconds(30);
        }
        private TimeSpan GetRequestedConnectionTimeout()
        {
            //if (StreamFactory.TryGetOption(in _scope, "RequestedConnectionTimeout", out object value))
            //{
            //    if (value is int seconds)
            //    {
            //        return TimeSpan.FromSeconds(seconds);
            //    }
            //}

            return TimeSpan.FromSeconds(30);
        }
        #endregion

        #region "MESSAGE OPTIONS AND VALUES"
        private string GetExchange()
        {
            //if (StreamFactory.TryGetOption(in _scope, "Exchange", out object value))
            //{
            //    return value.ToString();
            //}

            return string.Empty;
        }
        private string GetRoutingKey()
        {
            //if (StreamFactory.TryGetOption(in _scope, "RoutingKey", out object value))
            //{
            //    return value.ToString();
            //}

            return string.Empty;
        }
        private bool GetMandatory()
        {
            //if (StreamFactory.TryGetOption(in _scope, "Mandatory", out object value))
            //{
            //    if (value is bool boolean)
            //    {
            //        return boolean;
            //    }
            //}

            return false;
        }
        private string GetMessageBody()
        {
            //if (StreamFactory.TryGetOption(in _scope, "Body", out object value))
            //{
            //    return value.ToString();
            //}

            return string.Empty;
        }
        private DataObject GetHeaders()
        {
            //if (StreamFactory.TryGetOption(in _scope, "Headers", out object value) && value is DataObject record)
            //{
            //    return record;
            //}

            return null;
        }
        private string[] GetBlindCopy()
        {
            //if (StreamFactory.TryGetOption(in _scope, "BlindCopy", out object value))
            //{
            //    if (value is List<DataObject> list && list.Count > 0)
            //    {
            //        string[] array = new string[list.Count];

            //        for (int i = 0; i < list.Count; i++)
            //        {
            //            array[i] = list[i].GetValue(0).ToString();
            //        }

            //        return array;
            //    }
            //    else
            //    {
            //        return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            //    }
            //}

            return null;
        }
        private string[] GetCarbonCopy()
        {
            //if (StreamFactory.TryGetOption(in _scope, "CarbonCopy", out object value))
            //{
            //    if (value is List<DataObject> list && list.Count > 0)
            //    {
            //        string[] array = new string[list.Count];

            //        for (int i = 0; i < list.Count; i++)
            //        {
            //            array[i] = list[i].GetValue(0).ToString();
            //        }

            //        return array;
            //    }
            //    else
            //    {
            //        return value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            //    }
            //}

            return null;
        }
        private string GetAppId()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.AppId), out object value))
            //{
            //    return value.ToString();
            //}

            return null;
        }
        private string GetMessageId()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.MessageId), out object value))
            //{
            //    return value.ToString();
            //}

            return null;
        }
        private string GetMessageType()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.Type), out object value))
            //{
            //    return value.ToString();
            //}

            return null;
        }
        private string GetCorrelationId()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.CorrelationId), out object value))
            //{
            //    return value.ToString();
            //}

            return null;
        }
        private byte GetPriority()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.Priority), out object value))
            //{
            //    if (value is not null && byte.TryParse(value.ToString(), out byte priority))
            //    {
            //        return priority;
            //    }
            //}

            return 0;
        }
        private DeliveryModes GetDeliveryMode()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.DeliveryMode), out object value))
            //{
            //    if (value is not null && byte.TryParse(value.ToString(), out byte mode))
            //    {
            //        return mode;
            //    }
            //}

            return DeliveryModes.Persistent;
        }
        private string GetContentType()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.ContentType), out object value))
            //{
            //    return value.ToString();
            //}

            return "application/json";
        }
        private string GetContentEncoding()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.ContentEncoding), out object value))
            //{
            //    return value.ToString();
            //}

            return "UTF-8";
        }
        private string GetReplyTo()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.ReplyTo), out object value))
            //{
            //    return value.ToString();
            //}

            return null;
        }
        private string GetExpiration()
        {
            //if (StreamFactory.TryGetOption(in _scope, nameof(IBasicProperties.Expiration), out object value))
            //{
            //    return value.ToString();
            //}

            return null;
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

        #region "PUBLISH MESSAGE ASYNCHRONOUSLY"
        private async ValueTask<ulong> PublishMessageAsync()
        {
            ulong deliveryTag = await _channel.GetNextPublishSequenceNumberAsync().ConfigureAwait(false);

            ConfigureMessageHeaders(deliveryTag);

            ConfigureMessageProperties();

            ReadOnlyMemory<byte> payload = EncodeMessageBody(GetMessageBody());

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

            return deliveryTag;
        }
        private void ConfigureMessageHeaders(ulong deliveryTag)
        {
            _properties.Headers?.Clear();

            DataObject headers = GetHeaders();
            string[] BlindCopy = GetBlindCopy();
            string[] CarbonCopy = GetCarbonCopy();

            if (headers is null && BlindCopy is null && CarbonCopy is null)
            {
                return;
            }

            _properties.Headers ??= new Dictionary<string, object>();

            _properties.Headers.Add(RmqConstants.PublishSequenceNumberHeader, (long)deliveryTag);

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

        #region "MESSAGE DELIVERY HANDLERS"
        private Task BasicAcksHandler(object sender, BasicAckEventArgs args)
        {
            return HandlePublisherConfirm(args.DeliveryTag, args.Multiple);
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

            //ulong deliveryTag = 0;

            //IDictionary<string, object> headers = args.BasicProperties?.Headers;

            //if (headers is not null)
            //{
            //    object value = headers[RmqConstants.PublishSequenceNumberHeader];

            //    if (value is long int64)
            //    {
            //        deliveryTag = (ulong)int64;
            //    }
            //}

            //return HandlePublisherConfirm(deliveryTag, false);
        }
        private Task HandlePublisherConfirm(ulong deliveryTag, bool multiple)
        {
            if (_trackingTag == deliveryTag && multiple)
            {
                _ = _state?.TrySetResult();

                return Task.CompletedTask;
            }

            if (multiple)
            {
                foreach (KeyValuePair<ulong, bool> pending in _published.ToArray())
                {
                    if (pending.Key <= deliveryTag)
                    {
                        if (!_published.TryRemove(pending.Key, out _))
                        {
                            FileLogger.Default.Write(string.Format(ERROR_FAILED_TO_ACK, deliveryTag, "multiple"));
                        }
                    }
                }
            }
            else
            {
                if (!_published.TryRemove(deliveryTag, out _))
                {
                    FileLogger.Default.Write(string.Format(ERROR_FAILED_TO_ACK, deliveryTag, "single"));
                }
            }

            if (_trackingTag == deliveryTag && _published.IsEmpty)
            {
                _ = _state?.TrySetResult();
            }

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

            _trackingTag = _deliveryTag; // the last delivery tag awaiting confirmation from server

            bool timedout = state.Task.Wait(PublisherConfirmsTimeout, _context.Cancellation);

            if (timedout)
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
        private void ResetState()
        {
            if (_state is null)
            {
                return;
            }

            _state = null;
            _deliveryTag = 0UL;
            _trackingTag = 0UL;
            _published.Clear();
            _properties = null;

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
        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DataSourceScope scope = _scope;

            if (scope is not null)
            {
                scope.OnCommit -= SynchronizeCommit;
                scope.OnCancel -= SynchronizeCancel;
                scope.OnDispose -= SynchronizeDispose;
            }

            _disposed = true;
        }



        private bool _onlyAcksReceived = true;
        private readonly object _confirmLock = new object();
        private readonly LinkedList<ulong> _pendingDeliveryTags = new();
        private readonly CountdownEvent _deliveryTagsCountdown = new(0);
        public ShutdownEventArgs CloseReason { get; private set; }
        public bool IsOpen
        {
            get { return CloseReason == null; }
        }
        public ulong NextPublishSeqNo { get; private set; }
        public void ConfirmSelect()
        {
            if (NextPublishSeqNo == 0UL)
            {
                NextPublishSeqNo = 1;
            }

            //_Private_ConfirmSelect(false);
        }
        public void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
        {
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            if (basicProperties == null)
            {
                //basicProperties = _emptyBasicProperties;
            }

            if (NextPublishSeqNo > 0)
            {
                lock (_confirmLock)
                {
                    if (_deliveryTagsCountdown.IsSet)
                    {
                        _deliveryTagsCountdown.Reset(1);
                    }
                    else
                    {
                        _deliveryTagsCountdown.AddCount();
                    }

                    _pendingDeliveryTags.AddLast(NextPublishSeqNo++);
                }
            }

            try
            {
                //_Private_BasicPublish(exchange,
                //    routingKey,
                //    mandatory,
                //    basicProperties,
                //    body);
            }
            catch
            {
                if (NextPublishSeqNo > 0)
                {
                    lock (_confirmLock)
                    {
                        NextPublishSeqNo--;

                        _pendingDeliveryTags.RemoveLast();

                        _deliveryTagsCountdown.Reset(_pendingDeliveryTags.Count);
                    }
                }

                throw;
            }
        }
        private void OnModelShutdown(ShutdownEventArgs reason)
        {
            //_continuationQueue.HandleModelShutdown(reason);
            //EventHandler<ShutdownEventArgs> handler;
            //lock (_shutdownLock)
            //{
            //    handler = _modelShutdown;
            //    _modelShutdown = null;
            //}
            //if (handler != null)
            //{
            //    foreach (EventHandler<ShutdownEventArgs> h in handler.GetInvocationList())
            //    {
            //        try
            //        {
            //            h(this, reason);
            //        }
            //        catch (Exception e)
            //        {
            //            OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnModelShutdown"));
            //        }
            //    }
            //}

            _deliveryTagsCountdown.Reset(0);
            //_flowControlBlock.Set();
        }
        private void OnBasicReturn(BasicReturnEventArgs args)
        {
            //foreach (EventHandler<BasicReturnEventArgs> h in BasicReturn?.GetInvocationList() ?? Array.Empty<Delegate>())
            //{
            //    try
            //    {
            //        h(this, args);
            //    }
            //    catch (Exception e)
            //    {
            //        OnCallbackException(CallbackExceptionEventArgs.Build(e, "OnBasicReturn"));
            //    }
            //}
        }
        private void HandleAckNack(ulong deliveryTag, bool multiple, bool isNack)
        {
            // No need to do this if publisher confirms have never been enabled.
            if (NextPublishSeqNo > 0)
            {
                // let's take a lock so we can assume that deliveryTags are unique, never duplicated and always sorted
                lock (_confirmLock)
                {
                    // No need to do anything if there are no delivery tags in the list
                    if (_pendingDeliveryTags.Count > 0)
                    {
                        if (multiple)
                        {
                            int count = 0;
                            while (_pendingDeliveryTags.First.Value < deliveryTag)
                            {
                                _pendingDeliveryTags.RemoveFirst(); count++;
                            }

                            if (_pendingDeliveryTags.First.Value == deliveryTag)
                            {
                                _pendingDeliveryTags.RemoveFirst(); count++;
                            }

                            if (count > 0)
                            {
                                _deliveryTagsCountdown.Signal(count);
                            }
                        }
                        else
                        {
                            if (_pendingDeliveryTags.Remove(deliveryTag))
                            {
                                _deliveryTagsCountdown.Signal();
                            }
                        }
                    }

                    _onlyAcksReceived = _onlyAcksReceived && !isNack;
                }
            }
        }
        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            if (NextPublishSeqNo == 0UL)
            {
                throw new InvalidOperationException("Confirms not selected");
            }
            bool isWaitInfinite = timeout.TotalMilliseconds == Timeout.Infinite;

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                if (!IsOpen)
                {
                    throw new AlreadyClosedException(CloseReason);
                }

                if (_deliveryTagsCountdown.IsSet)
                {
                    bool aux = _onlyAcksReceived;
                    
                    _onlyAcksReceived = true;
                    
                    timedOut = false;

                    return aux;
                }

                if (isWaitInfinite)
                {
                    _deliveryTagsCountdown.Wait();
                }
                else
                {
                    TimeSpan elapsed = stopwatch.Elapsed;

                    if (elapsed > timeout || !_deliveryTagsCountdown.Wait(timeout - elapsed))
                    {
                        timedOut = true;

                        return _onlyAcksReceived;
                    }
                }
            }
        }
    }
}