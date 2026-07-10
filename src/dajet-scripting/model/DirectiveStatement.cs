namespace DaJet.Scripting.Model
{
    public sealed class DirectiveStatement : SyntaxNode
    {
        public DirectiveStatement() { Token = Token.Sharp; }
        public override string ToString()
        {
            return $"{Token}";
        }
    }
}