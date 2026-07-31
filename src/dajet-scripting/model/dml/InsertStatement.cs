namespace DaJet.Scripting.Model
{
    public sealed class InsertStatement : SqlStatement
    {
        public InsertStatement() { Token = Token.INSERT; }
        public CommonTableExpression CommonTables { get; set; }
        public SyntaxNode Source { get; set; } // SelectExpression
        public TableReference Target { get; set; }
    }
}