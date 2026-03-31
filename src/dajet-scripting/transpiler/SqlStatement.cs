using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class SqlStatement
    {
        public SqlStatement(in SyntaxNode node)
        {
            Node = node;
        }
        public SyntaxNode Node { get; private set; }
        public string Sql { get; set; }
        public string Dialect { get; set; } // SqlServer | PostgreSQL
        public int YearOffset { get; set; }
        public Dictionary<string, SyntaxNode> Input { get; } = new(); // VariableReference, MemberAccessExpression
        public SyntaxNode Output { get; set; } // INTO clause : VariableReference, MemberAccessExpression
        public List<ColumnExpression> Schema { get; set; } // {SELECT,STREAM,CONSUME} SELECT | {INSERT,UPDATE,DELETE} OUTPUT

        //public List<SyntaxNode> PostProcessing { get; } = new(); // FunctionExpression
    }
}