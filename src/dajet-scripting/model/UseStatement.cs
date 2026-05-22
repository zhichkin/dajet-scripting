namespace DaJet.Scripting.Model
{
    public sealed class UseStatement : SyntaxNode
    {
        public UseStatement() { Token = Token.USE; }
        public string Source { get; set; }
        public StatementBlock Statements { get; set; } = new();
        public override string ToString()
        {
            return $"{Source}";
        }
    }
}