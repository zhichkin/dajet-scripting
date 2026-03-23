namespace DaJet.Scripting.Model
{
    public sealed class PrintStatement : SyntaxNode
    {
        public PrintStatement() { Token = Token.PRINT; }
        public SyntaxNode Expression { get; set; }
    }
}