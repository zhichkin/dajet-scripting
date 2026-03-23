namespace DaJet.Scripting.Model
{
    public sealed class PartitionClause : SyntaxNode
    {
        public PartitionClause() { Token = Token.PARTITION; }
        public List<SyntaxNode> Columns { get; set; } = new();
    }
}