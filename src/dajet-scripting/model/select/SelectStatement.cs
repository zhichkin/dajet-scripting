namespace DaJet.Scripting.Model
{
    public sealed class SelectStatement : SqlStatement
    {
        public SelectStatement() { Token = Token.SELECT; }
        public SyntaxNode Expression { get; set; }
        public CommonTableExpression CommonTables { get; set; }
        public bool IsStream { get; set; } // STREAM statement
        public StatementBlock Statements { get; set; } // STREAM statement's data processor
        public IntoClause GetIntoClause()
        {
            if (Expression is SelectExpression select)
            {
                return select.Into;
            }
            
            if (Expression is TableUnionOperator union && union.Expression1 is SelectExpression first)
            {
                return first.Into;
            }

            return null;
        }
    }
}