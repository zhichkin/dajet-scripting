namespace DaJet.Scripting.Model
{
    public sealed class SleepStatement : SyntaxNode
    {
        public SleepStatement() { Token = Token.SLEEP; }
        public int Timeout { get; set; } = 0; // seconds
        public override string ToString()
        {
            return $"SLEEP {Timeout}";
        }
    }
}