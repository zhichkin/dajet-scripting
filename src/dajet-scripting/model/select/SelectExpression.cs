namespace DaJet.Scripting.Model
{
    public sealed class SelectExpression : SyntaxNode
    {
        public SelectExpression() { Token = Token.SELECT; }
        /// <summary>
        /// Correlation flag: true if select expression is correlated subquery.
        /// </summary>
        public bool IsCorrelated { get; set; } = true;
        public bool IsUnionSubordinate { get; set; }
        public FromClause From { get; set; }
        public List<ColumnExpression> Columns { get; set; } = new();
        public bool Distinct { get; set; }
        public TopClause Top { get; set; }
        public IntoClause Into { get; set; }
        public WhereClause Where { get; set; }
        public GroupClause Group { get; set; }
        public HavingClause Having { get; set; }
        public OrderClause Order { get; set; }
        public bool IsIntoScalar()
        {
            if (Into is IntoClause into)
            {
                if (into.Value is VariableReference variable)
                {
                    if (variable.Binding is DeclareStatement declare)
                    {
                        return !(declare.Type.IsArray || declare.Type.IsObject);
                    }
                }
            }

            return false;
        }

        // PG = FOR UPDATE SKIP LOCKED
        // MS = WITH (ROWLOCK, READPAST)
        public string Options { get; set; }
    }
}