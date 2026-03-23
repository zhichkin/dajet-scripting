namespace DaJet.Scripting.Model
{
    public sealed class SetExpression : SyntaxNode
    {
        public SetExpression() { Token = Token.SET; }
        public ColumnReference Column { get; set; }
        public SyntaxNode Initializer { get; set; }
    }
}