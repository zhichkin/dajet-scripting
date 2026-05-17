namespace DaJet.Scripting.Model
{
    public sealed class AssignmentOperator : SyntaxNode
    {
        public AssignmentOperator() { Token = Token.SET; }
        public SyntaxNode Target { get; set; } // VariableReference, MemberAccessExpression
        public SyntaxNode Initializer { get; set; } // expression, SELECT
        public override string ToString()
        {
            return $"SET {Target}";
        }
    }
}