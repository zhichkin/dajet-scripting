namespace DaJet.Scripting.Model
{
    public sealed class VariableReference : SyntaxNode
    {
        public VariableReference() { Token = Token.Variable; }
        public string Identifier { get; set; } = string.Empty;
        public DeclareStatement Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}