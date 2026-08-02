namespace DaJet.Scripting.Model
{
    /// <summary>
    /// Subquery expression
    /// </summary>
    public sealed class TableExpression : SyntaxNode
    {
        public TableExpression() { Token = Token.Table; }
        public string Alias { get; set; } = string.Empty;
        public SyntaxNode Expression { get; set; }
        /// <summary>
        /// Used to validate subqueries by the following functions:<br/>
        /// <see cref="Parser.table()"/> <br/>
        /// <see cref="Parser.grouping()"/> <br/>
        /// <see cref="Parser.in_right_operand()"/>
        /// </summary>
        public void ValidateIntoClauseOrThrow()
        {
            IntoClause into = null;

            if (Expression is SelectExpression select)
            {
                into = select.Into;
            }
            else if (Expression is TableUnionOperator union && union.Expression1 is SelectExpression first)
            {
                into = first.Into;
            }

            if (into is not null)
            {
                throw new FormatException($"[SUBQUERY] INTO clause is not allowed");
            }
        }
    }
}