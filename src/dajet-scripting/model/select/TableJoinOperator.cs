namespace DaJet.Scripting.Model
{
    public sealed class TableJoinOperator : SyntaxNode
    {
        public TableJoinOperator() { Token = Token.JOIN; }
        public OnClause On { get; set; }
        public SyntaxNode Expression1 { get; set; }
        public SyntaxNode Expression2 { get; set; }
        public Token Modifier { get; set; } = Token.Array; // APPEND operator
    }
}