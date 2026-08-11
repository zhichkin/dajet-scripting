using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class Transpiler
    {
        private List<string> _errors;
        private Stack<MetadataProvider> _providers = new();
        public Transpiler() { }
        public bool TryTranspile(in Script script, out List<string> errors)
        {
            _errors = new List<string>();

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

            _errors = null;

            return (errors.Count == 0);
        }
        private void Visit(in SyntaxNode node)
        {
            if (node is UseStatement use) { Visit(in use); }
            else if (node is SelectStatement select) { Visit(in select); }
            else if (node is InsertStatement insert) { Visit(in insert); }

            else if (node is CreateSequenceStatement
                || node is ApplySequenceStatement
                || node is RevokeSequenceStatement
                || node is DropSequenceStatement) { VisitSequenceStatement(in node); }

            else if (node is IfStatement _if) { Visit(in _if); }
            else if (node is ForStatement _for) { Visit(in _for); }
            else if (node is WhileStatement _while) { Visit(in _while); }
            else if (node is TryStatement _try) { Visit(in _try); }
        }
        private void Visit(in StatementBlock node)
        {
            foreach (SyntaxNode statement in node)
            {
                Visit(in statement);
            }
        }
        private void Visit(in UseStatement node)
        {
            MetadataProvider provider = MetadataProvider.Get(node.Source);

            _providers.Push(provider);

            Visit(node.Statements);
            
            _ = _providers.Pop();
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

            if (!transpiler.TryTranspile(node, in provider, out string error))
            {
                _errors.Add(error);
            }

            if (node.IsStream)
            {
                if (node.Statements is not null)
                {
                    Visit(node.Statements);
                }
            }
        }
        private void Visit(in InsertStatement node)
        {
            MetadataProvider provider = _providers.Peek();

            InsertTranspiler transpiler = new();

            if (!transpiler.TryTranspile(node, in provider, out string error))
            {
                _errors.Add(error);
            }
        }

        private void VisitSequenceStatement(in SyntaxNode node)
        {
            SqlTranspiler transpiler;

            MetadataProvider provider = _providers.Peek();

            if (provider.DataSource == DataSourceType.SqlServer)
            {
                transpiler = new MsSequenceTranspiler();
            }
            else if (provider.DataSource == DataSourceType.PostgreSql)
            {
                transpiler = new PgSequenceTranspiler();
            }
            else
            {
                _errors.Add($"Unsupported data provider: {provider.DataSource}"); return;
            }

            if (!transpiler.TryTranspile(in node, in provider, out string error))
            {
                _errors.Add(error);
            }
        }

        private void Visit(in IfStatement node)
        {
            if (node.THEN is not null)
            {
                Visit(node.THEN);
            }

            if (node.ELSE is not null)
            {
                Visit(node.ELSE);
            }
        }
        private void Visit(in ForStatement node)
        {
            Visit(node.Statements);
        }
        private void Visit(in WhileStatement node)
        {
            Visit(node.Statements);
        }
        private void Visit(in TryStatement node)
        {
            if (node.TRY is not null)
            {
                Visit(node.TRY);
            }

            if (node.CATCH is not null)
            {
                Visit(node.CATCH);
            }

            if (node.FINALLY is not null)
            {
                Visit(node.FINALLY);
            }
        }
    }
}