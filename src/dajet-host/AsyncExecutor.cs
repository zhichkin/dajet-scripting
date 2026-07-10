using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Host
{
    public sealed class AsyncExecutor : IDisposable, IAsyncDisposable
    {
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
        
        private readonly string _display;
        private readonly Interpreter _interpreter;
        public AsyncExecutor(in Script script)
        {
            _interpreter = new Interpreter(in script);

            if (!string.IsNullOrWhiteSpace(script.Display))
            {
                _display = script.Display;
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

            _task = Task.Factory.StartNew(_interpreter.Execute, _token);

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

            _task = Task.Factory.StartNew(_interpreter.Execute, _token);

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

            _task = Task.Factory.StartNew(_interpreter.Execute, _token, options, TaskScheduler.Default);

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

            _task = Task.Factory.StartNew(_interpreter.Execute, _token, options, TaskScheduler.Default);

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

        public override string ToString()
        {
            return string.Format("[{0}] {1}", _taskId, _display is not null ? _display : "noname");
        }
    }
}