namespace DaJet.Scripting.Model
{
    public sealed class WhileStatement : SyntaxNode
    {
        public WhileStatement() { Token = Token.WHILE; }
        public SyntaxNode Condition { get; set; }
        public StatementBlock Statements { get; set; } = new();
    }
}