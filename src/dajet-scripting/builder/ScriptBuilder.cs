using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class ScriptBuilder
    {
        private Script _script;
        private string _path;
        private string _source;
        private readonly List<Action> _steps = new(5);
        public ScriptBuilder FromFile(in string path)
        {
            _path = path; _steps.Add(FromFileStep); return this;
        }
        public ScriptBuilder FromSource(in string source)
        {
            _source = source; return this;
        }
        private void FromFileStep()
        {
            if (!File.Exists(_path))
            {
                throw new InvalidOperationException("Script is not found");
            }

            using (StreamReader reader = new(_path, Encoding.UTF8))
            {
                _source = reader.ReadToEnd();
            }
        }
        private void Parse()
        {
            if (!new Parser().TryParse(in _source, out _script, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }
        private void Register()
        {
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
        private void Bind()
        {
            ISchemaProvider provider = new CacheableSchemaProvider();

            if (!new Binder().TryBind(in _script, in provider, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }
        }
        private void Transpile()
        {
            if (!new Transpiler().TryTranspile(in _script, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }
        }
        public Script Build()
        {
            _steps.Add(Parse);
            //THINK: _steps.Add(Register); // import schema definitions
            _steps.Add(Bind);
            _steps.Add(Transpile);

            foreach (Action step in _steps)
            {
                step();
            }
            
            return _script;
        }
    }
}