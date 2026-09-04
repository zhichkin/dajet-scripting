using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ColumnReferenceExtractor : IScriptVisitor
    {
        private readonly List<ColumnReference> _columns = new();
        public List<ColumnReference> Extract(in SyntaxNode node)
        {
            Visitor.Visit(in node, this);

            return _columns;
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is ColumnReference column)
            {
                _columns.Add(column);
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}