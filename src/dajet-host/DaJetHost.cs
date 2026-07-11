using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using System.Collections.Concurrent;
using System.Text;

namespace DaJet.Host
{
    public sealed class DaJetHost
    {
        public static DaJetHost Create()
        {
            return new DaJetHost();
        }
        public static DaJetHost Create(in string catalog)
        {
            return new DaJetHost(in catalog);
        }
        private static readonly ScriptBuilder _builder = new();

        private readonly object _scripts_lock = new();
        private readonly ConcurrentDictionary<string, Script> _scripts = new(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher _scriptFileWatcher;
        public DaJetHost() { }
        public DaJetHost(in string rootName) { RootName = rootName; }
        public string RootName { get; } = "scripts";
        public string RootPath { get { return Path.Combine(AppContext.BaseDirectory, RootName); } }
        public string GetKeyFromFileName(in string fullPath)
        {
            string relativePath = Path.GetRelativePath(RootPath, fullPath);

            if (OperatingSystem.IsWindows())
            {
                relativePath = relativePath.Replace('\\', '/');
            }

            if (relativePath.StartsWith('/'))
            {
                relativePath = relativePath.TrimStart('/');
            }

            return relativePath.ToLower();
        }
        public DaJetHost Run()
        {
            Startup();

            InitializeScriptFileWatcher();
            
            return this;
        }
        private void Startup()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            StartupScripts(RootPath);
        }
        private void StartupScripts(in string catalogPath)
        {
            foreach (string scriptPath in Directory.EnumerateFiles(catalogPath, "*.djs"))
            {
                StartupScript(in scriptPath);
            }

            foreach (string nestedCatalog in Directory.EnumerateDirectories(catalogPath))
            {
                StartupScripts(in nestedCatalog);
            }
        }
        private void StartupScript(in string scriptPath)
        {
            string key = GetKeyFromFileName(in scriptPath);

            try
            {
                string sourceCode = null;

                using (StreamReader reader = new(scriptPath, Encoding.UTF8))
                {
                    sourceCode = reader.ReadToEnd();
                }

                Script script = _builder.FromSource(in sourceCode).Parse();

                if (script.RunAtStartup)
                {
                    script = _builder.FromScript(in script).Build();

                    AddOrUpdate(in key, in script);

                    _ = RunAsync(in script);

                    FileLogger.Default.Write($"[STARTUP][SUCCESS] {key}");
                }
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"[STARTUP][ERROR] {key}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }
        }

        #region "SCRIPT FILE WATCHER"
        private void InitializeScriptFileWatcher()
        {
            bool success = false;

            try
            {
                _scriptFileWatcher = new FileSystemWatcher(RootPath, "*.djs")
                {
                    InternalBufferSize = 65536,
                    IncludeSubdirectories = true,
                    NotifyFilter =
                    NotifyFilters.CreationTime
                    | NotifyFilters.LastWrite
                    | NotifyFilters.FileName
                };
                _scriptFileWatcher.Created += CreateScriptFileEvent;
                _scriptFileWatcher.Changed += ChangeScriptFileEvent;
                _scriptFileWatcher.Deleted += DeleteScriptFileEvent;
                _scriptFileWatcher.Renamed += RenameScriptFileEvent;
                _scriptFileWatcher.Error += ResetScriptFileWatcher;
                _scriptFileWatcher.EnableRaisingEvents = true;

                success = true;
            }
            catch (PlatformNotSupportedException unsupported)
            {
                FileLogger.Default.Write(unsupported);
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(error);
            }

            if (!success)
            {
                DisposeScriptFileWatcher();
            }

            FileLogger.Default.Write($"Script file watcher initialized on {RootName}");
        }
        private void DisposeScriptFileWatcher()
        {
            try
            {
                if (_scriptFileWatcher is not null)
                {
                    _scriptFileWatcher.Created -= CreateScriptFileEvent;
                    _scriptFileWatcher.Changed -= ChangeScriptFileEvent;
                    _scriptFileWatcher.Deleted -= DeleteScriptFileEvent;
                    _scriptFileWatcher.Renamed -= RenameScriptFileEvent;
                    _scriptFileWatcher.Error -= ResetScriptFileWatcher;
                    _scriptFileWatcher.Dispose();
                }
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(error);
            }
            finally
            {
                _scriptFileWatcher = null;
            }
        }
        private void ResetScriptFileWatcher(object sender, ErrorEventArgs args)
        {
            FileLogger.Default.Write("Trying to reset script file watcher ...");

            Exception error = args.GetException();

            if (error is not null)
            {
                FileLogger.Default.Write($"Reason: {ExceptionHelper.GetErrorMessage(error)}");
            }
            else
            {
                FileLogger.Default.Write("Reason: undefined");
            }
            
            DisposeScriptFileWatcher();

            InitializeScriptFileWatcher();
        }
        private void CreateScriptFileEvent(object sender, FileSystemEventArgs args)
        {
            if (!File.Exists(args.FullPath))
            {
                return;
            }

            string keyToCreate = GetKeyFromFileName(args.FullPath);

            try
            {
                Script script = CreateScriptFromFile(args.FullPath);

                AddOrUpdate(in keyToCreate, in script);

                FileLogger.Default.Write($"Created: {keyToCreate}");
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"Created: failed to process {keyToCreate}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
        }
        private void ChangeScriptFileEvent(object sender, FileSystemEventArgs args)
        {
            if (!File.Exists(args.FullPath))
            {
                return;
            }

            string keyToUpdate = GetKeyFromFileName(args.FullPath);

            try
            {
                Script script = CreateScriptFromFile(args.FullPath);

                AddOrUpdate(in keyToUpdate, in script);

                FileLogger.Default.Write($"Changed: {keyToUpdate}");
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"Changed: failed to process {keyToUpdate}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
        }
        private void DeleteScriptFileEvent(object sender, FileSystemEventArgs args)
        {
            string keyToDelete = GetKeyFromFileName(args.FullPath);

            _ = _scripts.TryRemove(keyToDelete, out _);

            FileLogger.Default.Write($"Deleted: {keyToDelete}");
        }
        private void RenameScriptFileEvent(object sender, FileSystemEventArgs args)
        {
            if (args is not RenamedEventArgs renamed)
            {
                return;
            }

            if (!File.Exists(renamed.FullPath))
            {
                return;
            }

            string keyToCreate = GetKeyFromFileName(renamed.FullPath);
            string keyToRemove = GetKeyFromFileName(renamed.OldFullPath);
            
            try
            {
                Script script = CreateScriptFromFile(args.FullPath);

                _ = _scripts.TryRemove(keyToRemove, out _);

                AddOrUpdate(in keyToCreate, in script);

                FileLogger.Default.Write($"Renamed: {keyToRemove} > {keyToCreate}");
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"Renamed: failed to process {keyToRemove}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
        }
        #endregion

        private Script CreateScript(in string key)
        {
            string filePath = Path.Combine(RootPath, key);
            
            return CreateScriptFromFile(in filePath);
        }
        private Script CreateScriptFromFile(in string filePath)
        {
            return _builder.FromFile(in filePath).Build();
        }
        private Script GetOrCreate(in string key)
        {
            Script script;

            if (_scripts.TryGetValue(key, out script))
            {
                return script;
            }

            bool locked = false;

            try
            {
                Monitor.Enter(_scripts_lock, ref locked);

                if (_scripts.TryGetValue(key, out script))
                {
                    return script; // double-checking
                }

                // long path - create resource

                script = CreateScript(in key);

                if (string.IsNullOrEmpty(script.Name))
                {
                    script.Name = key;
                }

                _ = _scripts.TryAdd(key, script); // add resource to the cache
            }
            finally
            {
                if (locked)
                {
                    Monitor.Exit(_scripts_lock);
                }
            }

            return script;
        }
        private void AddOrUpdate(in string key, in Script script)
        {
            bool locked = false;

            try
            {
                Monitor.Enter(_scripts_lock, ref locked);

                if (!_scripts.TryAdd(key, script))
                {
                    _scripts[key] = script;
                }

                if (string.IsNullOrEmpty(script.Name))
                {
                    script.Name = key;
                }
            }
            finally
            {
                if (locked)
                {
                    Monitor.Exit(_scripts_lock);
                }
            }
        }
        public bool TryGetOrCreate(in string key, out Script script, out string error)
        {
            error = null;
            script = null;

            try
            {
                script = GetOrCreate(in key);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return script is not null;
        }

        private readonly ConcurrentDictionary<string, int> _singletons = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<int, AsyncExecutor> _runningTasks = new();
        public object Run(in string key, in DataObject parameters = null)
        {
            Script script = GetOrCreate(in key);

            return Run(in script, in parameters);
        }
        public object Run(in Script script, in DataObject parameters = null)
        {
            Interpreter interpreter = new(in script);

            if (parameters is null)
            {
                return interpreter.Execute();
            }
            else
            {
                return interpreter.Execute(in parameters);
            }
        }
        public Task<object> RunAsync(in string key, in DataObject parameters = null)
        {
            Script script = GetOrCreate(in key);
            
            return RunAsync(in script, in parameters);
        }
        public Task<object> RunAsync(in Script script, in DataObject parameters = null)
        {
            if (script.IsSingleton)
            {
                return RunSingleton(in script, in parameters);
            }

            if (script.IsLongRunning)
            {
                return RunLongTask(in script, in parameters);
            }

            return RunTask(in script, in parameters);
        }
        public Task<object> RunTask(in Script script, in DataObject parameters = null)
        {
            AsyncExecutor executor = new(in script);

            Task<object> task;

            if (parameters is null)
            {
                task = executor.ExecuteAsync();
            }
            else
            {
                task = executor.ExecuteAsync(in parameters);
            }

            if (_runningTasks.TryAdd(task.Id, executor))
            {
                _ = task.ContinueWith(DisposeExecutor);
            }
            
            return task;
        }
        public Task<object> RunLongTask(in string key, in DataObject parameters = null)
        {
            Script script = GetOrCreate(in key);
            
            return RunLongTask(in script, in parameters);
        }
        public Task<object> RunLongTask(in Script script, in DataObject parameters = null)
        {
            AsyncExecutor executor = new(in script);

            Task<object> task;

            if (parameters is null)
            {
                task = executor.ExecuteAsync(TaskCreationOptions.LongRunning);
            }
            else
            {
                task = executor.ExecuteAsync(in parameters, TaskCreationOptions.LongRunning);
            }

            if (_runningTasks.TryAdd(task.Id, executor))
            {
                _ = task.ContinueWith(DisposeExecutor);
            }
            
            return task;
        }
        private Task<object> RunSingleton(in Script script, in DataObject parameters = null)
        {
            if (!_singletons.TryAdd(script.SingletonKey, 0))
            {
                string message = $"Duplicate singleton run: [{script.Name}] {{{script.SingletonKey}}}";

                return Task.FromException<object>(new InvalidOperationException(message));
            }

            Task<object> task;

            if (script.IsLongRunning)
            {
                task = RunLongTask(in script, in parameters);
            }
            else
            {
                task = RunTask(in script, in parameters);
            }

            _ = task.ContinueWith(RemoveSingleton, script.SingletonKey);

            _singletons[script.SingletonKey] = task.Id;

            return task;
        }
        private void DisposeExecutor(Task<object> task)
        {
            if (_runningTasks.TryRemove(task.Id, out AsyncExecutor executor))
            {
                executor.Dispose();
            }
        }
        private void RemoveSingleton(Task<object> task, object state)
        {
            if (state is string key)
            {
                _ = _singletons.TryRemove(key, out _);
            }
        }
        public void Cancel(int task)
        {
            if (_runningTasks.TryRemove(task, out AsyncExecutor executor))
            {
                executor.Cancel();
            }
        }
        public List<RunningTaskInfo> GetRunningTasks()
        {
            List<RunningTaskInfo> display = new(_runningTasks.Count);

            foreach (var item in _runningTasks)
            {
                display.Add(item.Value.Descriptor);
            }

            return display;
        }
    }
}