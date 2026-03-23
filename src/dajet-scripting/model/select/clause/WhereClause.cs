namespace DaJet.Scripting.Model
{
    public sealed class WhereClause : SyntaxNode
    {
        public WhereClause() { Token = Token.WHERE; }
        public SyntaxNode Expression { get; set; }
    }
}