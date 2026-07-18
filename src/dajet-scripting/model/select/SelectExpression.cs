using DaJet.TypeSystem;

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
        public bool TryGetColumn(in string alias, out ColumnExpression expression)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(alias, nameof(alias));

            expression = null;

            string propertyName = null;

            foreach (ColumnExpression column in Columns)
            {
                if (!string.IsNullOrEmpty(column.Alias))
                {
                    propertyName = column.Alias;
                }
                else if (column.Expression is ColumnReference field)
                {
                    propertyName = field.ColumnName;
                }
                else if (column.Source is PropertyDefinition property)
                {
                    propertyName = property.Name;
                }

                if (propertyName == alias)
                {
                    expression = column; return true; // success
                }
            }

            return false; // not found
        }

        // PG = FOR UPDATE SKIP LOCKED
        // MS = WITH (ROWLOCK, READPAST)
        public string Options { get; set; }
    }
}