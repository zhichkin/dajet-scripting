namespace DaJet.Scripting.Model
{
    public sealed class OrderClause : SyntaxNode
    {
        public OrderClause() { Token = Token.ORDER; }
        public List<OrderExpression> Expressions { get; set; } = new();
        public SyntaxNode Offset { get; set; }
        public SyntaxNode Fetch { get; set; }
    }
}