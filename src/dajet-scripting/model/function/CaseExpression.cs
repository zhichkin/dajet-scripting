namespace DaJet.Scripting.Model
{
    public sealed class CaseExpression : SyntaxNode
    {
        public CaseExpression() { Token = Token.CASE; }
        public List<WhenClause> CASE { get; set; } = new();
        public SyntaxNode ELSE { get; set; }
    }
}