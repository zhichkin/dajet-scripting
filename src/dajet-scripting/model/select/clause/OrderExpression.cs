namespace DaJet.Scripting.Model
{
    public sealed class OrderExpression : SyntaxNode
    {
        public OrderExpression() { Token = Token.ASC; }
        public SyntaxNode Expression { get; set; }
    }
}