namespace DaJet.Scripting.Model
{
    public sealed class Script : SyntaxNode
    {
        public Script() { Token = Token.Script; }
        public List<SyntaxNode> Statements { get; } = new();
    }
}