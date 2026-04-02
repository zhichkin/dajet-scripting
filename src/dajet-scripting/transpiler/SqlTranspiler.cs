using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class SqlTranspiler
    {
        private readonly Dictionary<Token, IStatementTranspiler> _transpilers = new()
        {
            { Token.SELECT, new SelectTranspiler() }
        };

        private List<string> _errors;
        private List<SqlStatement> _statements;
        public SqlTranspiler(in string dialect, int yearOffset)
        {
            Dialect = dialect;
            YearOffset = yearOffset;

            //TODO:
            //Dictionary<Token, IStatementTranspiler> transpilers = new()
            //{
            //    { Token.SELECT, new SelectTranspiler(){ YearOffset = yearOffset } }
            //};
        }
        public string Dialect { get; private set; }
        public int YearOffset { get; private set; }
        public bool TryTranspile(in SyntaxNode node, out List<SqlStatement> statements, out List<string> errors)
        {
            _errors = new List<string>();
            _statements = new List<SqlStatement>();

            try
            {
                Visit(in node);
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
            if (node is Script script) { Visit(in script); }
            else if (node is UseStatement use) { Visit(in use); }

            else if (_transpilers.TryGetValue(node.Token, out IStatementTranspiler transpiler))
            {
                if (transpiler.TryTranspile(in node, out SqlStatement statement, out string error))
                {
                    _statements.Add(statement);
                }
                else
                {
                    _errors.Add(error);
                }
            }
        }
        private void Visit(in Script node)
        {
            foreach (SyntaxNode statement in node.Statements)
            {
                Visit(in statement);
            }
        }
        private void Visit(in UseStatement node)
        {
            foreach (SyntaxNode statement in node.Statements.Statements)
            {
                Visit(in statement);
            }
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
    }
}