namespace DaJet.Scripting.Model
{
    public sealed class InsertStatement : SqlStatement
    {
        public InsertStatement() { Token = Token.INSERT; }
        public CommonTableExpression CommonTables { get; set; }
        public TableReference Target { get; set; } // database table
        public List<ColumnExpression> Values { get; set; } = new(); // data mapping
        public bool TryGetMapping(in string alias, out ColumnExpression expression)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(alias, nameof(alias));

            expression = null;

            foreach (ColumnExpression column in Values)
            {
                if (column.Alias == alias)
                {
                    expression = column;

                    return true; // success
                }
            }

            return false; // not found
        }
        public OrderClause Order { get; set; } // used by bulk insert (optional)
        public VariableReference Source { get; set; } // bulk insert batch source
    }
}