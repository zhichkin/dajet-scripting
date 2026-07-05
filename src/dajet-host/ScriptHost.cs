using DaJet.Host;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using DaJet.Utilities;
using System.Collections.Concurrent;
using System.Text;

namespace DaJet.Scripting.Host
{
    public sealed class ScriptHost
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

        private readonly ConcurrentDictionary<int, ScriptExecutor> _executors = new();
        public Task<object> Run(in string key, in DataObject parameters = null, TaskCreationOptions options = TaskCreationOptions.None)
        {
            if (_scripts.TryGetValue(key, out Script script))
            {
                ScriptExecutor executor = new(in script);

                Task<object> task = executor.ExecuteAsync(in parameters, options);

                _ = _executors.TryAdd(task.Id, executor);

                _ = task.ContinueWith(Remove);

                return task;
            }

            return null;
        }
        public object GetResult(int taskId)
        {
            if (_executors.TryGetValue(taskId, out ScriptExecutor executor))
            {
                return executor.GetResult();
            }

            return null;
        }
        public ScriptStatus GetStatus(int taskId)
        {
            if (_executors.TryGetValue(taskId, out ScriptExecutor executor))
            {
                return executor.GetStatus();
            }

            return ScriptStatus.Default;
        }
        private void Remove(Task<object> task)
        {
            Task.Delay(TimeSpan.FromMinutes(1)).Wait(); //FIXME

            _ = _executors.TryRemove(task.Id, out _);
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