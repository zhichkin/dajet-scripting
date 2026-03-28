using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class SelectStatementExtractor : IScriptVisitor
    {
        private readonly List<SelectStatement> _statements = new();
        public List<SelectStatement> Extract(in SyntaxNode node)
        {
            Visitor.Visit(in node, this);

            return _statements;
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is SelectStatement statement)
            {
                _statements.Add(statement);
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}