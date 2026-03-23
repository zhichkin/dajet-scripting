namespace DaJet.Scripting.Model
{
    public sealed class WindowFrame : SyntaxNode
    {
        public WindowFrame() { Token = Token.PRECEDING; } // PRECEDING | FOLLOWING
        public int Extent { get; set; } = -1; // UNBOUNDED = -1, CURRENT ROW = 0
    }
}