namespace DaJet.Scripting.Model
{
    public sealed class FromClause : SyntaxNode
    {
        public FromClause() { Token = Token.FROM; }
        public SyntaxNode Expression { get; set; }
        public bool TryGetTable(out TableReference table)
        {
            table = null;

            if (Expression is null)
            {
                return false;
            }

            return TryGetTableRecursively(Expression, out table);
        }
        private static bool TryGetTableRecursively(SyntaxNode node, out TableReference table)
        {
            table = null;

            if (node is TableJoinOperator join)
            {
                return TryGetTableRecursively(join.Expression1, out table);
            }
            else if (node is TableReference target)
            {
                table = target;
            }

            return (table is not null);
        }
    }
}