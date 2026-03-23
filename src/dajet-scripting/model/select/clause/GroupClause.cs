namespace DaJet.Scripting.Model
{
    public sealed class GroupClause : SyntaxNode
    {
        public GroupClause() { Token = Token.GROUP; }
        public List<SyntaxNode> Expressions { get; set; } = new();
    }
}