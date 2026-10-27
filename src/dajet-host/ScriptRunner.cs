using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Host
{
    public sealed class ScriptRunner : IDisposable, IAsyncDisposable
    {
        private static readonly TaskFactory _factory = System.Threading.Tasks.Task.Factory;

        private int _state;
        private const int STATE_IS_READY = 0;
        private const int STATE_IS_RUNNING = 1;
        private const int STATE_DISPOSING = 2;
        private bool CanExecute { get { return Interlocked.CompareExchange(ref _state, STATE_IS_RUNNING, STATE_IS_READY) == STATE_IS_READY; } }
        private bool CanDispose { get { return Interlocked.CompareExchange(ref _state, STATE_DISPOSING, STATE_IS_RUNNING) == STATE_IS_RUNNING; } }

        private int _taskId;
        private Task<object> _task;
        private readonly Script _script;
        private readonly Interpreter _interpreter;
        public ScriptRunner(in Script script)
        {
            ArgumentNullException.ThrowIfNull(script, nameof(script));

            _script = script;

            _interpreter = new Interpreter(in _script);
        }
        public Script Script { get { return _script; } }
        public Task<object> Task { get { return _task; } }
        public RunningTaskStatus Descriptor
        {
            get
            {
                Task<object> task = _task;

                string status = task is null ? "Ready" : task.Status.ToString();

                return new RunningTaskStatus(_taskId, status, _script.Path, _script.SingletonKey);
            }
        }

        public Task<object> RunAsync()
        {
            if (!CanExecute)
            {
                return null;
            }

            _task = _factory.StartNew(_interpreter.Execute, null);

            _taskId = _task.Id;

            return _task;
        }
        public Task<object> RunAsync(in DataObject parameters)
        {
            if (!CanExecute)
            {
                return null;
            }

            _task = _factory.StartNew(_interpreter.Execute, parameters);

            _taskId = _task.Id;

            return _task;
        }
        public Task<object> RunAsync(TaskCreationOptions options)
        {
            if (!CanExecute)
            {
                return null;
            }

            _task = _factory.StartNew(_interpreter.Execute, null, options);

            _taskId = _task.Id;

            return _task;
        }
        public Task<object> RunAsync(TaskCreationOptions options, in DataObject parameters)
        {
            if (!CanExecute)
            {
                return null;
            }

            _task = _factory.StartNew(_interpreter.Execute, parameters, options);

            _taskId = _task.Id;

            return _task;
        }

        public void Dispose()
        {
            if (CanDispose)
            {
                _task = null;
                _taskId = 0;

                _interpreter.Cancel();

                // instances of this class can be reused ?!
                _ = Interlocked.Exchange(ref _state, STATE_IS_READY);
            }
        }
        public void Cancel() { Dispose(); }
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