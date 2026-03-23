namespace DaJet.Scripting.Model
{
    public sealed class OutputClause : SyntaxNode
    {
        public OutputClause() { Token = Token.OUTPUT; }
        public List<ColumnExpression> Columns { get; set; } = new();
        public IntoClause Into { get; set; }
    }
}