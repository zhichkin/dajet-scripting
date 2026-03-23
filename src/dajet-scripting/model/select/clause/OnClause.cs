namespace DaJet.Scripting.Model
{
    public sealed class OnClause : SyntaxNode
    {
        public OnClause() { Token = Token.ON; }
        public SyntaxNode Expression { get; set; }
    }
}