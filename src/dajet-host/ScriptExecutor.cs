using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Host
{
    public sealed class ScriptExecutor : IDisposable, IAsyncDisposable
    {
        private int _state;
        private const int STATE_IS_READY = 0;
        private const int STATE_IS_RUNNING = 1;
        private const int STATE_DISPOSING = 2;
        private void SetReady() { _ = Interlocked.Exchange(ref _state, STATE_IS_READY); }
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_RUNNING, STATE_IS_READY) == STATE_IS_READY; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_RUNNING) == STATE_IS_RUNNING; } }

        private Task<object> _task;
        private CancellationToken _token;
        private CancellationTokenSource _cts;
        private CancellationTokenRegistration _ctr;

        private readonly Interpreter _interpreter;
        public ScriptExecutor(in Script script)
        {
            _interpreter = new Interpreter(in script);
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
        public Task<object> ExecuteAsync(in DataObject parameters = null, TaskCreationOptions options = TaskCreationOptions.None)
        {
            if (!CanExecute)
            {
                return null;
            }

            Configure(in parameters);

            if (options == TaskCreationOptions.None)
            {
                _task = Task.Factory.StartNew(_interpreter.Execute, _token);
            }
            else
            {
                _task = Task.Factory.StartNew(_interpreter.Execute, _token, options, TaskScheduler.Default);
            }
            
            return _task;
        }
        public ScriptStatus GetStatus()
        {
            return _interpreter.Status;
        }
        public object GetResult()
        {
            return _task?.Result;
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

                SetReady(); // instances of this class can be reused
            }
        }
        public ValueTask DisposeAsync()
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
    }
}