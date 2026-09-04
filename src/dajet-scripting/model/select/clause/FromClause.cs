namespace DaJet.Scripting.Model
{
    public sealed class FromClause : SyntaxNode
    {
        public FromClause() { Token = Token.FROM; }
        public SyntaxNode Expression { get; set; }
    }
}