namespace DaJet.Scripting.Model
{
    public sealed class UseStatement : SyntaxNode
    {
        public UseStatement() { Token = Token.USE; }
        public string Source { get; set; } // static database binding
        public VariableReference DynamicSource { get; set; } // dynamic database binding
        public bool IsDynamic { get { return DynamicSource is not null; } }
        public StatementBlock Statements { get; set; } = new();
        public override string ToString()
        {
            if (IsDynamic)
            {
                return $"Dynamic {DynamicSource.Identifier} '{(Source is null ? "not bound" : Source)}'";
            }
            else
            {
                return $"Static '{Source}'";
            }
        }
    }
}