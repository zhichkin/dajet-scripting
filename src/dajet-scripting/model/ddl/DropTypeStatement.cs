namespace DaJet.Scripting.Model
{
    public sealed class DropTypeStatement : SqlStatement
    {
        public DropTypeStatement() { Token = Token.TYPE; }
        public string Identifier { get; set; }
        public TableReference Table { get; set; }
    }
}