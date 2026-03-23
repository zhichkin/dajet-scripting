namespace DaJet.Scripting.Model
{
    public sealed class DropSequenceStatement : SyntaxNode
    {
        public DropSequenceStatement() { Token = Token.SEQUENCE; }
        public string Identifier { get; set; }
    }
}