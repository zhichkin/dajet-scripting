namespace DaJet.Scripting.Model
{
    public sealed class TopClause : SyntaxNode
    {
        public TopClause() { Token = Token.TOP; }
        public SyntaxNode Expression { get; set; }
    }
}