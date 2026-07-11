using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Host
{
    public sealed class AsyncExecutor : IDisposable, IAsyncDisposable
    {
        private static readonly TaskFactory _factory = System.Threading.Tasks.Task.Factory;

        private int _state;
        private const int STATE_IS_READY = 0;
        private const int STATE_IS_RUNNING = 1;
        private const int STATE_DISPOSING = 2;
        private void SetReady() { _ = Interlocked.Exchange(ref _state, STATE_IS_READY); }
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_RUNNING, STATE_IS_READY) == STATE_IS_READY; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_RUNNING) == STATE_IS_RUNNING; } }

        private int _taskId;
        private Task<object> _task;
        private CancellationToken _token;
        private CancellationTokenSource _cts;
        private CancellationTokenRegistration _ctr;

        private readonly Script _script;
        private readonly Interpreter _interpreter;
        public AsyncExecutor(in Script script)
        {
            ArgumentNullException.ThrowIfNull(script, nameof(script));

            _script = script;

            _interpreter = new Interpreter(in _script);
        }
        public Script Script { get { return _script; } }
        public Task<object> Task { get { return _task; } }
        public RunningTaskInfo Descriptor
        {
            get
            {
                return new RunningTaskInfo(_taskId, _script.Name, _script.SingletonKey);
            }
        }

        private void Configure(in DataObject parameters = null)
        {
            _cts = new CancellationTokenSource();

            _token = _cts.Token;

            _ctr = _token.Register(CancellationHandler);

            _interpreter.Configure(_token);

            if (parameters is not null)
            {
                _interpreter.SetParameters(in parameters);
            }
        }
        public Task<object> ExecuteAsync()
        {
            if (!CanExecute)
            {
                return null;
            }

            Configure();

            _task = _factory.StartNew(_interpreter.Execute, _token);

            _taskId = _task.Id;

            return _task;
        }
        public Task<object> ExecuteAsync(in DataObject parameters)
        {
            if (!CanExecute)
            {
                return null;
            }

            Configure(in parameters);

            _task = _factory.StartNew(_interpreter.Execute, _token);

            _taskId = _task.Id;

            return _task;
        }
        public Task<object> ExecuteAsync(TaskCreationOptions options)
        {
            if (!CanExecute)
            {
                return null;
            }

            Configure();

            _task = _factory.StartNew(_interpreter.Execute, _token, options, TaskScheduler.Default);

            _taskId = _task.Id;

            return _task;
        }
        public Task<object> ExecuteAsync(in DataObject parameters, TaskCreationOptions options)
        {
            if (!CanExecute)
            {
                return null;
            }

            Configure(in parameters);

            _task = _factory.StartNew(_interpreter.Execute, _token, options, TaskScheduler.Default);

            _taskId = _task.Id;

            return _task;
        }

        public void Cancel() { Dispose(); }
        private void CancellationHandler()
        {
            _interpreter.Cancel();
        }
        public void Dispose()
        {
            if (CanDispose)
            {
                try
                {
                    _task = null;
                    _cts.Cancel();
                }
                finally
                {
                    _ctr.Dispose(); // unregister handler from cancellation token
                    _cts.Dispose(); // dispose executor cancellation token source
                }

                _taskId = 0;

                SetReady(); // instances of this class can be reused
            }
        }
        void IDisposable.Dispose() { Dispose(); }
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            try
            {
                Dispose();

                return ValueTask.CompletedTask;
            }
            catch (Exception error)
            {
                return ValueTask.FromException(error);
            }
        }

        public override string ToString() { return Descriptor.ToString(); }
    }
}