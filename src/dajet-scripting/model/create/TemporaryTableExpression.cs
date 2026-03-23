namespace DaJet.Scripting.Model
{
    public sealed class TemporaryTableExpression : SyntaxNode
    {
        public TemporaryTableExpression() { Token = Token.Table; }
        public string Name { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; }
    }
}