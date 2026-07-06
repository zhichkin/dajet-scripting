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
        private readonly ConcurrentDictionary<string, Script> _scripts = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ScriptSettings> _settings = new(StringComparer.OrdinalIgnoreCase);
        public string RootName { get; set; } = "scripts";
        public string RootPath { get { return Path.Combine(AppContext.BaseDirectory, RootName); } }
        public void InitializeFromFiles()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            InitializeDaJetScripts(RootPath);
        }
        private void InitializeDaJetScripts(in string catalogPath)
        {
            foreach (string scriptPath in Directory.EnumerateFiles(catalogPath, "*.djs"))
            {
                InitializeDaJetScript(in scriptPath);
            }

            foreach (string nestedCatalog in Directory.EnumerateDirectories(catalogPath))
            {
                InitializeDaJetScripts(in nestedCatalog);
            }
        }
        private void InitializeDaJetScript(in string scriptPath)
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

        public bool TryGet(in string key, out ScriptSettings entry)
        {
            return _settings.TryGetValue(key, out entry);
        }

        private readonly ConcurrentDictionary<int, AsyncExecutor> _executors = new();
        public object Run(in string key, in DataObject parameters = null)
        {
            if (!_scripts.TryGetValue(key, out Script script))
            {
                throw new FileNotFoundException("Script not found", key);
            }

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
            if (!_scripts.TryGetValue(key, out Script script))
            {
                throw new FileNotFoundException("Script not found", key);
            }

            return RunAsync(in script, in parameters, options);
        }
        public Task<object> RunAsync(in Script script, in DataObject parameters = null, TaskCreationOptions options = TaskCreationOptions.None)
        {
            AsyncExecutor executor = new(in script);

            Task<object> task = executor.ExecuteAsync(in parameters, options);

            _ = _executors.TryAdd(task.Id, executor);

            _ = task.ContinueWith(Remove);

            return task;
        }
        private void Remove(Task<object> task)
        {
            Task.Delay(TimeSpan.FromMinutes(1)).Wait(); //FIXME

            if (_executors.TryRemove(task.Id, out AsyncExecutor executor))
            {
                executor.Dispose();
            }
        }
        public ScriptStatus Cancel(int taskId)
        {
            if (_executors.TryGetValue(taskId, out AsyncExecutor executor))
            {
                executor.Cancel();

                return executor.GetStatus();
            }

            return ScriptStatus.Default;
        }

        public object GetResult(int taskId)
        {
            if (_executors.TryGetValue(taskId, out AsyncExecutor executor))
            {
                return executor.GetResult();
            }

            return null;
        }
        public ScriptStatus GetStatus(int taskId)
        {
            if (_executors.TryGetValue(taskId, out AsyncExecutor executor))
            {
                return executor.GetStatus();
            }

            return ScriptStatus.Default;
        }
        public List<ScriptStatus> GetExecutingTasks()
        {
            List<ScriptStatus> list = new();

            ScriptStatus status;

            foreach (var item in _executors)
            {
                status = item.Value.GetStatus();

                list.Add(status);
            }

            return list;
        }
    }
}