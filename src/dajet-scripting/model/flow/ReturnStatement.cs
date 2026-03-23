namespace DaJet.Scripting.Model
{
    public sealed class ReturnStatement : SyntaxNode
    {
        // RETURN <expression>
        public ReturnStatement() { Token = Token.RETURN; }
        public SyntaxNode Expression { get; set; } //NOTE: required !!!
    }
}