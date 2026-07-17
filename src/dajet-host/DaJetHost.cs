using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using System.Collections.Concurrent;

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
        
        private readonly object _cache_lock = new();
        private readonly ConcurrentDictionary<string, Script> _scripts = new(StringComparer.OrdinalIgnoreCase);
        public DaJetHost() { }
        public DaJetHost(in string rootName) { RootName = rootName; }
        public string RootName { get; } = "scripts";
        public string RootPath { get { return Path.Combine(AppContext.BaseDirectory, RootName); } }
        public string GetScriptRelativePath(in string fullPath)
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

            return relativePath;
        }
        public ScriptBuilder CreateScriptBuilder()
        {
            return new ScriptBuilder(RootPath);
        }
        private Script GetOrCreate(in string path)
        {
            Script script;

            if (_scripts.TryGetValue(path, out script))
            {
                return script;
            }

            bool locked = false;

            try
            {
                Monitor.Enter(_cache_lock, ref locked);

                if (_scripts.TryGetValue(path, out script))
                {
                    return script;
                }

                // create script from source code and store it to the cache

                ScriptBuilder factory = CreateScriptBuilder();

                script = factory.FromPath(in path).Parse();
                
                if (!script.IsDynamic)
                {
                    script = factory.Build(); // build static script
                }
                
                _ = _scripts.TryAdd(path, script); // add script to the cache
            }
            finally
            {
                if (locked)
                {
                    Monitor.Exit(_cache_lock);
                }
            }

            return script;
        }
        private void AddOrUpdate(in string path, in Script script)
        {
            bool locked = false;

            try
            {
                Monitor.Enter(_cache_lock, ref locked);

                script.Path = path;

                if (!_scripts.TryAdd(path, script))
                {
                    _scripts[path] = script;
                }
            }
            finally
            {
                if (locked)
                {
                    Monitor.Exit(_cache_lock);
                }
            }
        }
        private Script GetScriptFromCache(in string path, in DataObject parameters)
        {
            Script script = GetOrCreate(in path);

            if (!script.IsDynamic)
            {
                return script;
            }

            return CreateScriptBuilder().FromScript(in script).Use(in parameters).Build();
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
            try
            {
                ScriptBuilder factory = CreateScriptBuilder();

                Script script = factory.FromFile(in scriptPath).Parse();

                if (script.RunAtStartup && !script.IsDynamic)
                {
                    //NOTE: Dynamic scripts are not allowed to run at startup.
                    //NOTE: Parameters to use are not available at the moment.

                    script = factory.Build();

                    AddOrUpdate(script.Path, in script);

                    _ = RunAsync(in script);

                    FileLogger.Default.Write($"[STARTUP][SUCCESS] {script.Path}");
                }
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"[STARTUP][ERROR] {scriptPath}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
        }

        #region "SCRIPT FILE WATCHER"
        private FileSystemWatcher _scriptFileWatcher;
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

            FileLogger.Default.Write($"Script file watcher initialized on '{RootName}'");
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

            try
            {
                ScriptBuilder factory = CreateScriptBuilder();

                Script script = factory.FromFile(args.FullPath).Parse();

                if (!script.IsDynamic)
                {
                    script = factory.Build();
                }

                AddOrUpdate(script.Path, in script);

                FileLogger.Default.Write($"Created: {script.Path}");
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"[WATCHER][CREATED][ERROR] {args.FullPath}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
        }
        private void ChangeScriptFileEvent(object sender, FileSystemEventArgs args)
        {
            if (!File.Exists(args.FullPath))
            {
                return;
            }

            try
            {
                ScriptBuilder factory = CreateScriptBuilder();

                Script script = factory.FromFile(args.FullPath).Parse();

                if (!script.IsDynamic)
                {
                    script = factory.Build();
                }

                AddOrUpdate(script.Path, in script);

                FileLogger.Default.Write($"Changed: {script.Path}");
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"[WATCHER][CHANGED][ERROR] {args.FullPath}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
        }
        private void DeleteScriptFileEvent(object sender, FileSystemEventArgs args)
        {
            string pathToDelete = GetScriptRelativePath(args.FullPath);

            _ = _scripts.TryRemove(pathToDelete, out _);

            FileLogger.Default.Write($"Deleted: {pathToDelete}");
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

            string pathToCreate = GetScriptRelativePath(renamed.FullPath);
            string pathToRemove = GetScriptRelativePath(renamed.OldFullPath);
            
            try
            {
                ScriptBuilder factory = CreateScriptBuilder();

                Script script = factory.FromFile(args.FullPath).Parse();

                if (!script.IsDynamic)
                {
                    script = factory.Build();
                }

                _ = _scripts.TryRemove(pathToRemove, out _);

                AddOrUpdate(in pathToCreate, in script);

                FileLogger.Default.Write($"Renamed: {pathToRemove} > {pathToCreate}");
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"[WATCHER][RENAMED][ERROR] {renamed.OldFullPath}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessage(error));
            }
        }
        #endregion
        
        private readonly ConcurrentDictionary<string, int> _singletons = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<int, AsyncExecutor> _runningTasks = new();
        public object Run(in string path, in DataObject parameters = null)
        {
            Script script = GetScriptFromCache(in path, in parameters);

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
        public Task<object> RunAsync(in string path, in DataObject parameters = null)
        {
            Script script = null;
            Task<object> task = null;
            
            try
            {
                script = GetScriptFromCache(in path, in parameters);
            }
            catch (Exception error)
            {
                task = Task.FromException<object>(error);
            }

            if (script is null)
            {
                return task;
            }
            
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
        public Task<object> RunLongTask(in string path, in DataObject parameters = null)
        {
            Script script = null;
            Task<object> task = null;

            try
            {
                script = GetScriptFromCache(in path, in parameters);
            }
            catch (Exception error)
            {
                task = Task.FromException<object>(error);
            }

            if (script is null)
            {
                return task;
            }

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
                string message = $"Duplicate singleton run: [{script.Path}] {{{script.SingletonKey}}}";

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
        public void Cancel(int taskId)
        {
            if (_runningTasks.TryRemove(taskId, out AsyncExecutor executor))
            {
                executor.Cancel();
            }
        }
        public void Cancel(in string path)
        {
            List<AsyncExecutor> executors = new();

            AsyncExecutor executor;

            foreach (var item in _runningTasks)
            {
                executor = item.Value;

                if (executor.Script.Path == path)
                {
                    executors.Add(executor);
                }
            }

            for (int i = 0; i < executors.Count; i++)
            {
                executor = executors[i];

                executor.Cancel();
            }
        }
        public RunningTaskStatus GetRunningTask(int taskId)
        {
            if (_runningTasks.TryGetValue(taskId, out AsyncExecutor executor))
            {
                return executor.Descriptor;
            }

            return RunningTaskStatus.Default; // Not found
        }
        public List<RunningTaskStatus> GetRunningTasks()
        {
            List<RunningTaskStatus> list = new(_runningTasks.Count);

            AsyncExecutor executor;

            foreach (var item in _runningTasks)
            {
                executor = item.Value;

                list.Add(executor.Descriptor);
            }

            return list;
        }
        public List<RunningTaskStatus> GetRunningTasks(in string path)
        {
            List<RunningTaskStatus> list = new(_runningTasks.Count);

            AsyncExecutor executor;

            foreach (var item in _runningTasks)
            {
                executor = item.Value;

                if (executor.Script.Path == path)
                {
                    list.Add(executor.Descriptor);
                }
            }

            return list;
        }
    }
}