namespace DaJet.Scripting
{
    public sealed class Lexeme
    {
        public Lexeme(Token tokenType)
        {
            Token = tokenType;
        }
        public Token Token { get; private set; }
        public void Override(Token token) { Token = token; }
        public string Value { get; set; }
        public int Line { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
        public override string ToString()
        {
            return $"{Token} {{{Line}}} [{Offset}-{Offset + Length - 1}] {Value}";
        }

    }
}