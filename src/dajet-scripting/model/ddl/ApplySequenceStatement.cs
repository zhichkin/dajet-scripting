namespace DaJet.Scripting.Model
{
    public sealed class ApplySequenceStatement : SqlStatement
    {
        // APPLY SEQUENCE <sequence> ON <table>(<column>) [RECALCULATE]
        public ApplySequenceStatement() { Token = Token.SEQUENCE; }
        public string Identifier { get; set; }
        public TableReference Table { get; set; }
        public ColumnReference Column { get; set; }
        public bool ReCalculate { get; set; }
    }
}