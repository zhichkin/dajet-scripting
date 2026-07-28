namespace DaJet.Scripting.Model
{
    public sealed class DropSequenceStatement : SqlStatement
    {
        public DropSequenceStatement() { Token = Token.SEQUENCE; }
        public string Identifier { get; set; }
    }
}