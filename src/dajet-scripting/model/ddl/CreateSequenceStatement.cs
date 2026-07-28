namespace DaJet.Scripting.Model
{
    public sealed class CreateSequenceStatement : SqlStatement
    {
        public CreateSequenceStatement() { Token = Token.SEQUENCE; }
        public string Identifier { get; set; }
        public TypeReference DataType { get; set; }
        public int StartWith { get; set; } = 1;
        public int Increment { get; set; } = 1;
        public int CacheSize { get; set; } = 0;
    }
}