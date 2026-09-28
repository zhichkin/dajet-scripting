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
using RmqConstants = RabbitMQ.Client.Constants;

namespace DaJet.RabbitMQ
{
    public sealed class Producer : ProcessorBase
    {
        #region "CONSTANTS"
        private const string ERROR_STATE_IS_BROKEN = "Broken state";
        private const string ERROR_CHANNEL_SHUTDOWN = "Channel shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_SHUTDOWN = "Connection shutdown: [{0}] {1}";
        private const string ERROR_CONNECTION_IS_BLOCKED = "Connection blocked: {0}";
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
        private TaskCompletionSource _state;
        private readonly ConcurrentDictionary<ulong, bool> _published = new();
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

            PublisherConfirmsTimeout = GetPublisherConfirmsTimeout();
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

                Task<ulong> publisher = PublishMessageAsync();

                if (!publisher.IsCompleted)
                {
                    publisher.GetAwaiter().GetResult();
                }

                ulong deliveryTag = publisher.Result;

                if (!_published.TryAdd(publisher.Result, false))
                {
                    throw new InvalidOperationException($"Failed to track the publisher confirmation for sequence number '{deliveryTag}' because it already exists.");
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
        private TimeSpan PublisherConfirmsTimeout { get; set; } = TimeSpan.FromSeconds(60);
        private TimeSpan GetPublisherConfirmsTimeout()
        {
            //if (StreamFactory.TryGetOption(in _scope, "PublisherConfirmsTimeout", out object value))
            //{
            //    if (value is int seconds)
            //    {
            //        return TimeSpan.FromSeconds(seconds);
            //    }
            //}

            return TimeSpan.FromSeconds(60);
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
            CreateChannelOptions options = new(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: false);

            _channel = await _connection.CreateChannelAsync(options).ConfigureAwait(false);

            _channel.BasicAcksAsync += BasicAcksHandler;
            _channel.BasicNacksAsync += BasicNacksHandler;
            _channel.BasicReturnAsync += BasicReturnHandler;
            _channel.FlowControlAsync += FlowControlHandler;
            _channel.ChannelShutdownAsync += ChannelShutdownHandler;

            _properties = new BasicProperties()
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "UTF-8"
            };
        }
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
        private Task FlowControlHandler(object sender, FlowControlEventArgs args)
        {
            //SetSessionToBrokenState(string.Format(ERROR_CHANNEL_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText));

            return Task.CompletedTask;
        }
        private Task ChannelShutdownHandler(object sender, ShutdownEventArgs args)
        {
            string message = string.Format(ERROR_CHANNEL_SHUTDOWN, args.ReplyCode.ToString(), args.ReplyText);

            _ = _state?.TrySetException(new Exception(message));

            FileLogger.Default.Write(message);

            return Task.CompletedTask;
        }
        private Task HandlePublisherConfirm(ulong deliveryTag, bool multiple)
        {
            if (multiple)
            {
                foreach (KeyValuePair<ulong, bool> pair in _published.ToArray())
                {
                    if (pair.Key <= deliveryTag)
                    {
                        if (_published.TryRemove(pair.Key, out _))
                        {
                            //tcs.SetResult(true);
                        }
                    }
                }
            }
            else
            {
                if (_published.TryRemove(deliveryTag, out _))
                {
                    //tcs.SetResult(true);
                }
            }

            if (_published.IsEmpty)
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

        #region "PUBLISH MESSAGE ASYNCHRONOUSLY"
        private async Task<ulong> PublishMessageAsync()
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

            _state = null;

            _published.Clear();
        }
        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_scope is not null)
            {
                _scope.OnCommit -= SynchronizeCommit;
                _scope.OnCancel -= SynchronizeCancel;
                _scope.OnDispose -= SynchronizeDispose;
            }

            _disposed = true;
        }
    }
}