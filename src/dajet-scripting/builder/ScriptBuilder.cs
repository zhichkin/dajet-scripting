using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class ScriptBuilder
    {
        private Script _script;
        private string _root;
        private string _path;
        private string _source;
        private DataObject _parameters;
        private readonly List<Action> _steps = new(5);
        public ScriptBuilder() { }
        public ScriptBuilder(in string rootPath)
        {
            _root = rootPath; // Каталог хостинга скриптов DaJet
        }
        public string GetScriptFullPath(in string relativePath)
        {
            string fullPath = relativePath;

            if (OperatingSystem.IsWindows())
            {
                fullPath = fullPath.Replace('/', '\\');
            }

            fullPath = Path.Combine(_root, fullPath);

            return fullPath;
        }
        public string GetScriptRelativePath(in string fullPath)
        {
            if (string.IsNullOrEmpty(_root))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(fullPath))
            {
                return string.Empty;
            }

            string relativePath = Path.GetRelativePath(_root, fullPath);

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
        public ScriptBuilder FromFile(in string fullPath)
        {
            _steps.Clear();

            _path = fullPath;
            
            _steps.Add(FromFileStep);

            _steps.Add(ParseStep);

            return this;
        }
        public ScriptBuilder FromPath(in string relativePath)
        {
            string fullPath = GetScriptFullPath(in relativePath);

            return FromFile(in fullPath);
        }
        public ScriptBuilder FromSource(in string source)
        {
            _steps.Clear();

            _source = source;
            
            _steps.Add(ParseStep);

            return this;
        }
        public ScriptBuilder FromScript(in Script script)
        {
            _steps.Clear();

            if (script.IsDynamic)
            {
                //NOTE: Создание нового динамичесого скрипта
                //NOTE: по шаблону из кэша DaJet Host

                _source = script.SourceCode; // шаблон кода

                _path = GetScriptFullPath(script.Path);
                
                _steps.Add(ParseStep);
            }
            else
            {
                //NOTE: Статический скрипт, созданный программно,
                //NOTE: например, ендпоинт /query DaJet.Http.Server
                
                _script = script;
            }
            
            return this;
        }
        public ScriptBuilder Use(in DataObject parameters)
        {
            _parameters = parameters;
            
            return this;
        }
        private void FromFileStep()
        {
            if (!File.Exists(_path))
            {
                throw new InvalidOperationException($"Script is not found: {_path}");
            }

            //NOTE: Необходим монопольный доступ к файлу, чтобы прочитать исходный код без коллизий,
            //NOTE: которые могут возникнуть при одновременном его обновлении другми процессами

            using (FileStream stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (StreamReader reader = new(stream, Encoding.UTF8))
                {
                    _source = reader.ReadToEnd();
                }
            }
        }
        private void ParseStep()
        {
            if (!new Parser().TryParse(in _source, out _script, out string error))
            {
                throw new InvalidOperationException(error);
            }

            if (_script.IsDynamic)
            {
                _script.SourceCode = _source;
            }

            _script.Path = GetScriptRelativePath(in _path);
        }
        private void RegisterStep()
        {
            // import schema definitions

            List<DefineStatement> definitions = new();

            foreach (SyntaxNode node in _script.Statements)
            {
                if (node is DefineStatement definition)
                {
                    definitions.Add(definition);
                }
            }

            if (!SchemaRegistry.TryRegister(in definitions, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }
        private void DynamicBindingStep()
        {
            if (_parameters is null || _parameters.Count == 0)
            {
                throw new InvalidOperationException("[USE] Dynamic binding parameters are not provided");
            }

            List<UseStatement> statements = _script.GetUseStatements();

            string parameter;
            List<string> parameters = new();

            foreach (UseStatement use in statements)
            {
                if (!use.IsDynamic) 
                {
                    continue; // Статические команды USE остаются без изменений
                }

                // Трансформируем динамический скрипт в статический

                parameter = use.DynamicSource.Identifier.TrimStart('@');

                if (!_parameters.TryGetValue(parameter, out object value))
                {
                    throw new InvalidOperationException($"[USE] Dynamic binding parameter @{parameter} is not provided");
                }

                if (value is not string database)
                {
                    throw new InvalidOperationException($"[USE] Dynamic binding parameter @{parameter} must be a string value");
                }

                if (string.IsNullOrWhiteSpace(database))
                {
                    throw new InvalidOperationException($"[USE] Dynamic binding parameter @{parameter} empty value is not allowed");
                }

                use.Source = database;
                use.DynamicSource = null;
                parameters.Add(database);
            }

            _script.IsDynamic = false;
            _script.SourceCode = null; // Это свойство используется только кэшированными динамическими скриптами

            if (_script.IsSingleton)
            {
                if (parameters.Count > 0)
                {
                    string key = string.Join(':', parameters);

                    _script.SingletonKey = string.Format("{0}:{1}", key, _script.SingletonKey);
                }
            }
        }
        private void BindStep()
        {
            ISchemaProvider provider = new CacheableSchemaProvider();

            if (!new Binder().TryBind(in _script, in provider, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }
        }
        private void TranspileStep()
        {
            if (!new Transpiler().TryTranspile(in _script, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }
        }
        public Script Parse()
        {
            foreach (Action step in _steps)
            {
                step();
            }

            _steps.Clear(); // Учитывает последующий вызов метода Build
            
            return _script;
        }
        public Script Build()
        {
            //THINK: _steps.Add(Register); // import schema definitions

            foreach (Action step in _steps)
            {
                step(); // get source code and parse script
            }

            _steps.Clear();

            if (_script.IsDynamic)
            {
                _steps.Add(DynamicBindingStep);
            }
            
            _steps.Add(BindStep);

            _steps.Add(TranspileStep);

            foreach (Action step in _steps)
            {
                step();
            }

            Script script = _script;

            // cleanup
            _script = null;
            _root = null;
            _path = null;
            _source = null;
            _parameters = null;
            _steps.Clear();

            return script;
        }
    }
}