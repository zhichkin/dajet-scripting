namespace DaJet.Scripting.Model
{
    public sealed class IntoClause : SyntaxNode
    {
        public IntoClause() { Token = Token.INTO; }
        public TableReference Table { get; set; }
        public VariableReference Value { get; set; }
    }
}