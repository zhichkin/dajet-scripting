namespace DaJet.Scripting.Model
{
    public sealed class RevokeSequenceStatement : SqlStatement
    {
        // REVOKE SEQUENCE <sequence> ON <table>
        public RevokeSequenceStatement() { Token = Token.SEQUENCE; }
        public string Identifier { get; set; }
        public TableReference Table { get; set; }
    }
}