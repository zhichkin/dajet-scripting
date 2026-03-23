namespace DaJet.Scripting.Model
{
    public abstract class SyntaxNode
    {
        public Token Token { get; set; } = Token.Ignore;
        public override string ToString()
        {
            return $"{Token}: {GetType()}";
        }
    }
}