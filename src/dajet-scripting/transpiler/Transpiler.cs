using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class Transpiler
    {
        private List<string> _errors;
        private List<SqlStatement> _statements;
        private Stack<MetadataProvider> _providers = new();
        public Transpiler() { }
        public bool TryTranspile(in Script script, out List<SqlStatement> statements, out List<string> errors)
        {
            _errors = new List<string>();
            _statements = new List<SqlStatement>();

            try
            {
                foreach (SyntaxNode statement in script.Statements)
                {
                    Visit(in statement);
                }
            }
            catch (Exception exception)
            {
                _errors.Add(ExceptionHelper.GetErrorMessage(exception));
            }

            errors = _errors;
            statements = _statements;

            _errors = null;
            _statements = null;

            return (errors.Count == 0);
        }
        private void Visit(in SyntaxNode node)
        {
            if (node is UseStatement use) { Visit(in use); }
            else if (node is SelectStatement select) { Visit(in select); }
        }
        private void Visit(in UseStatement node)
        {
            MetadataProvider provider = MetadataProvider.Get(node.Source);

            _providers.Push(provider);

            foreach (SyntaxNode statement in node.Statements)
            {
                Visit(in statement);
            }

            _ = _providers.Pop();
        }
        private void Visit(in IfStatement node)
        {
            if (node.THEN is not null)
            {

            }

            if (node.ELSE is not null)
            {

            }
        }
        private void Visit(in CaseStatement node)
        {
            if (node.CASE is not null)
            {
                foreach (WhenClause when in node.CASE)
                {
                    if (when.THEN is not null)
                    {
                        Visit(when.THEN);
                    }
                }
            }

            if (node.ELSE is not null)
            {

            }
        }
        private void Visit(in ForStatement node)
        {
            if (node.Statements is not null)
            {

            }
        }
        private void Visit(in WhileStatement node)
        {
            if (node.Statements is not null)
            {

            }
        }
        private void Visit(in TryStatement node)
        {
            if (node.TRY is not null)
            {

            }

            if (node.CATCH is not null)
            {

            }

            if (node.FINALLY is not null)
            {

            }
        }

        private void Visit(in SelectStatement node)
        {
            SqlTranspiler transpiler;

            MetadataProvider provider = _providers.Peek();

            if (provider.DataSource == DataSourceType.SqlServer)
            {
                transpiler = new MsSelectTranspiler();
            }
            else if (provider.DataSource == DataSourceType.PostgreSql)
            {
                transpiler = new PgSelectTranspiler();
            }
            else
            {
                _errors.Add($"Unsupported data provider: {provider.DataSource}"); return;
            }

            if (transpiler.TryTranspile(in provider, node, out SqlStatement statement, out string error))
            {
                _statements.Add(statement);
            }
            else
            {
                _errors.Add(error);
            }
        }
    }
}