namespace DaJet.Scripting.Model
{
    public sealed class ThrowStatement : SyntaxNode
    {
        // THROW <expression>
        public ThrowStatement() { Token = Token.THROW; }
        public SyntaxNode Expression { get; set; }
    }
}