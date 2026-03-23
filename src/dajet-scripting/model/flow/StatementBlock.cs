namespace DaJet.Scripting.Model
{
    public sealed class StatementBlock : SyntaxNode
    {
        public StatementBlock() { Token = Token.BEGIN; }
        public List<SyntaxNode> Statements { get; set; } = new();
    }
}