using DaJet.Utilities;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace DaJet.Scripting.Host
{
    public sealed class ScriptHost
    {
        private readonly ConcurrentDictionary<string, ScriptSettings> _scripts = new(StringComparer.OrdinalIgnoreCase);
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

            ScriptSettings entry = GetScriptSettings(in settingsPath);

            try
            {
                string script = null;

                using (StreamReader reader = new(scriptPath, Encoding.UTF8))
                {
                    script = reader.ReadToEnd();
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

                _scripts.TryAdd(relativePath, entry);
            }
            catch (Exception error)
            {
                FileLogger.Default.Write($"[HOST][ERROR] Failed to load {fileName}");
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }
        }
        private static ScriptSettings GetScriptSettings(in string settingsPath)
        {
            if (!File.Exists(settingsPath))
            {
                return ScriptSettings.Default;
            }

            ScriptSettings settings = null;

            try
            {
                using (StreamReader reader = new(settingsPath, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();

                    settings = JsonSerializer.Deserialize<ScriptSettings>(json);
                }
            }
            catch (Exception exception)
            {
                FileLogger.Default.Write(ExceptionHelper.GetErrorMessageAndStackTrace(exception));
            }

            return settings is null ? ScriptSettings.Default : settings;
        }

        public bool TryGet(string key, out ScriptSettings entry)
        {
            return _scripts.TryGetValue(key, out entry);
        }
    }
}