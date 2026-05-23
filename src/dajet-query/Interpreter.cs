using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class Interpreter
    {
        private Script _script;
        private Dictionary<SyntaxNode, SqlStatement> _statements = new();
        private object _returnValue = null;
        private Dictionary<string, object> _data = new();
        private Stack<MetadataProvider> _sources = new();
        public object Execute(in string source)
        {
            object value = null;

            try
            {
                Prepare(in source);

                Execute();

                value = _returnValue;
            }
            finally
            {
                Dispose();
            }

            return value;
        }
        private void Prepare(in string source)
        {
            Parser parser = new();

            if (!parser.TryParse(in source, out _script, out string error))
            {
                throw new InvalidOperationException(error);
            }

            Binder binder = new();
            //OneDbSchemaProvider schema = new();
            CacheableSchemaProvider schema = new();

            if (!binder.TryBind(in _script, schema, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            Transpiler transpiler = new();

            if (!transpiler.TryTranspile(in _script, out List<SqlStatement> statements, out errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            foreach (SqlStatement statement in statements)
            {
                _statements.Add(statement.Node, statement);
            }
        }
        private void Execute()
        {
            foreach (SyntaxNode node in _script.Statements)
            {
                Execute(in node);
            }
        }
        private void Dispose()
        {
            _script = null;
            _statements = null;
            _returnValue = null;
        }
        
        private void Execute(in SyntaxNode node)
        {
            if (node is PrintStatement print) { Execute(in print); }
            else if (node is UseStatement use) { Execute(in use); }
            else if (node is SelectStatement select) { Execute(in select); }
            else if (node is ReturnStatement _return) { Execute(in _return); }
            else if (node is AssignmentOperator assign) { Execute(in assign); }
        }
        private void Execute(in PrintStatement statement)
        {
            ExpressionInterpreter expression = new(in _data);

            object value = expression.Evaluate(statement.Expression);

            if (value is not null)
            {
                Console.WriteLine(value.ToString());
            }
        }
        private void Execute(in ReturnStatement statement)
        {
            
        }
        private void Execute(in UseStatement statement)
        {
            MetadataProvider provider = MetadataProvider.Get(statement.Source);

            _sources.Push(provider);

            foreach (SyntaxNode node in statement.Statements.Statements)
            {
                Execute(in node);
            }

            _ = _sources.Pop();
        }
        private void Execute(in SelectStatement statement)
        {

        }
        private void Execute(in AssignmentOperator statement)
        {

        }
    }
}