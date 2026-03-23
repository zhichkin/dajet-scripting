namespace DaJet.Scripting.Model
{
    public sealed class TryStatement : SyntaxNode
    {
        public TryStatement() { Token = Token.TRY; }
        public StatementBlock TRY { get; set; } = new();
        public StatementBlock CATCH { get; set; } = new();
        public StatementBlock FINALLY { get; set; } = new();
    }
}