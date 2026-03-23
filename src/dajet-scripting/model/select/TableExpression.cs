namespace DaJet.Scripting.Model
{
    public sealed class TableExpression : SyntaxNode
    {
        public TableExpression() { Token = Token.Table; }
        public string Alias { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; }
    }
}