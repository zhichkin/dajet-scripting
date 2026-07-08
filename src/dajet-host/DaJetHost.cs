using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

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
        private readonly ConcurrentDictionary<string, ScriptSettings> _settings = new(StringComparer.OrdinalIgnoreCase);
        public DaJetHost() { }
        public DaJetHost(in string catalog) { RootName = catalog; }
        public string RootName { get; } = "scripts";
        public string RootPath { get { return Path.Combine(AppContext.BaseDirectory, RootName); } }

        private Channel<FileSystemEventArgs> _events;
        private CancellationToken _cancellationToken;
        private FileSystemWatcher _fileSystemWatcher;
        public DaJetHost Run()
        {
            if (TryInitializeFileSystemWatcher())
            {
                _events = Channel.CreateBounded<FileSystemEventArgs>(128);

                _ = Task.Factory.StartNew(ObserveScriptCatalog, TaskCreationOptions.LongRunning);
            }

            return this;
        }

        #region "FILE SYSTEM WATCHER"
        private bool TryInitializeFileSystemWatcher()
        {
            bool success = false;

            try
            {
                _fileSystemWatcher = new FileSystemWatcher(RootPath, string.Empty)
                {
                    InternalBufferSize = 65536,
                    IncludeSubdirectories = true,
                    NotifyFilter =
                    NotifyFilters.CreationTime
                    | NotifyFilters.LastWrite
                    | NotifyFilters.FileName
                };
                _fileSystemWatcher.Created += FileSystemEvent;
                _fileSystemWatcher.Changed += FileSystemEvent;
                _fileSystemWatcher.Deleted += FileSystemEvent;
                _fileSystemWatcher.Renamed += FileSystemEvent;
                _fileSystemWatcher.Error += ResetFileSystemWatcher;
                _fileSystemWatcher.EnableRaisingEvents = true;

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
                DisposeFileSystemWatcher();
            }

            return success;
        }
        private void DisposeFileSystemWatcher()
        {
            try
            {
                if (_fileSystemWatcher is not null)
                {
                    _fileSystemWatcher.Created -= FileSystemEvent;
                    _fileSystemWatcher.Changed -= FileSystemEvent;
                    _fileSystemWatcher.Deleted -= FileSystemEvent;
                    _fileSystemWatcher.Renamed -= FileSystemEvent;
                    _fileSystemWatcher.Error -= ResetFileSystemWatcher;
                    _fileSystemWatcher.Dispose();
                }
            }
            catch (Exception error)
            {
                FileLogger.Default.Write(error);
            }
            finally
            {
                _fileSystemWatcher = null;
            }
        }
        private void ResetFileSystemWatcher(object sender, ErrorEventArgs args)
        {
            FileLogger.Default.Write("Trying to reset file system watcher ...");

            Exception error = args.GetException();

            if (error is not null)
            {
                FileLogger.Default.Write($"Reason: {ExceptionHelper.GetErrorMessage(error)}");
            }
            else
            {
                FileLogger.Default.Write("Reason: undefined");
            }
            
            DisposeFileSystemWatcher();

            if (TryInitializeFileSystemWatcher())
            {
                FileLogger.Default.Write("File system watcher is reset successfully.");
            }
            else
            {
                FileLogger.Default.Write("Failed to reset file system watcher.");
            }
        }
        private void FileSystemEvent(object sender, FileSystemEventArgs args)
        {
            _ = _events.Writer.TryWrite(args);
        }
        private async Task ObserveScriptCatalog()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScriptCatalogEvents();
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(ExceptionHelper.GetErrorMessageAndStackTrace(error));
                }

                try
                {
                    FileLogger.Default.Write($"Script catalog watcher delay 30 seconds ...");

                    await Task.Delay(TimeSpan.FromSeconds(30), _cancellationToken);
                }
                catch // (OperationCanceledException)
                {
                    // do nothing - host shutdown requested
                }
            }
        }
        private async ValueTask ProcessScriptCatalogEvents()
        {
            FileLogger.Default.Write("Waiting for file system events ...");

            while (await _events.Reader.WaitToReadAsync(_cancellationToken))
            {
                FileLogger.Default.Write("Processing file system events ...");

                while (_events.Reader.TryRead(out FileSystemEventArgs _event))
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        return; // Сервис остановлен принудительно
                    }

                    if (_event.ChangeType == WatcherChangeTypes.Created)
                    {
                        FileLogger.Default.Write($"Created: {_event.FullPath}");
                    }
                    else if (_event.ChangeType == WatcherChangeTypes.Changed)
                    {
                        FileLogger.Default.Write($"Changed: {_event.FullPath}");
                    }
                    else if (_event.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        FileLogger.Default.Write($"Deleted: {_event.FullPath}");
                    }
                    else if (_event.ChangeType == WatcherChangeTypes.Renamed)
                    {
                        FileLogger.Default.Write($"Renamed: {_event.FullPath}");
                    }

                    FileLogger.Default.Write($"Processed {_event.FullPath} successfully");
                }
            }
        }
        #endregion

        private void InitializeFromFiles()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            InitializeScripts(RootPath);
        }
        private void InitializeScripts(in string catalogPath)
        {
            foreach (string scriptPath in Directory.EnumerateFiles(catalogPath, "*.djs"))
            {
                InitializeScript(in scriptPath);
            }

            foreach (string nestedCatalog in Directory.EnumerateDirectories(catalogPath))
            {
                InitializeScripts(in nestedCatalog);
            }
        }
        private void InitializeScript(in string scriptPath)
        {
            string fileName = Path.GetFileName(scriptPath);

            string settingsPath = Path.ChangeExtension(scriptPath, "json");

            try
            {
                ScriptSettings entry = ScriptSettings.Create(in settingsPath);

                string sourceCode = null;

                using (StreamReader reader = new(scriptPath, Encoding.UTF8))
                {
                    sourceCode = reader.ReadToEnd();
                }

                string relativePath = Path.GetRelativePath(RootPath, scriptPath);

                if (OperatingSystem.IsWindows())
                {
                    relativePath = relativePath.Replace('\\', '/');

                    if (relativePath.StartsWith('/'))
                    {
                        relativePath = relativePath.TrimStart('/');
                    }
                }

                Script script = new ScriptBuilder().FromSource(in sourceCode).Build();

                _scripts.TryAdd(relativePath, script);

                _settings.TryAdd(relativePath, entry);
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"[HOST][ERROR] Failed to load {fileName}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }
        }

        private Script CreateScript(in string key)
        {
            //string settingsPath = Path.Combine(RootPath, key, ".json");

            //ScriptSettings settings = ScriptSettings.Create(in settingsPath);

            string scriptPath = Path.Combine(RootPath, key);

            if (!Path.HasExtension(scriptPath))
            {
                scriptPath = Path.Combine(scriptPath, ".djs");
            }
            
            return new ScriptBuilder().FromFile(in scriptPath).Build();
        }
        public Script GetOrCreate(in string key)
        {
            Script script;

            if (_scripts.TryGetValue(key, out script))
            {
                return script;
            }

            bool locked = false;

            try
            {
                Monitor.Enter(_cache_lock, ref locked);

                if (_scripts.TryGetValue(key, out script))
                {
                    return script; // double-checking
                }

                // long path - create new initialized provider

                script = CreateScript(in key);

                _ = _scripts.TryAdd(key, script); // add provider to the cache
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
        public Script AddOrUpdate(in string key)
        {
            bool locked = false;

            try
            {
                Monitor.Enter(_cache_lock, ref locked);

                Script script = CreateScript(in key);

                if (!_scripts.TryAdd(key, script))
                {
                    _scripts[key] = script;
                }

                return script;
            }
            finally
            {
                if (locked)
                {
                    Monitor.Exit(_cache_lock);
                }
            }
        }

        public bool TryGet(in string key, out Script script)
        {
            return _scripts.TryGetValue(key, out script);
        }
        public bool TryGet(in string key, out ScriptSettings settings)
        {
            return _settings.TryGetValue(key, out settings);
        }

        private readonly ConcurrentDictionary<int, AsyncExecutor> _executors = new();
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
        public Task<object> RunAsync(in string key, in DataObject parameters = null, TaskCreationOptions options = TaskCreationOptions.None)
        {
            Script script = GetOrCreate(in key);

            return RunAsync(in script, in parameters, options);
        }
        public Task<object> RunAsync(in Script script, in DataObject parameters = null, TaskCreationOptions options = TaskCreationOptions.None)
        {
            AsyncExecutor executor = new(in script);

            Task<object> task = executor.ExecuteAsync(in parameters, options);

            _ = _executors.TryAdd(task.Id, executor);

            _ = task.ContinueWith(DisposeExecutor);

            return task;
        }
        private void DisposeExecutor(Task<object> task)
        {
            if (_executors.TryRemove(task.Id, out AsyncExecutor executor))
            {
                executor.Dispose();
            }
        }
        public void Cancel(int task)
        {
            if (_executors.TryRemove(task, out AsyncExecutor executor))
            {
                executor.Cancel();
            }
        }
    }
}