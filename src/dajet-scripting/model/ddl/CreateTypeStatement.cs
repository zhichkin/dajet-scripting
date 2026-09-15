using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public sealed class CreateTypeStatement : SqlStatement
    {
        public CreateTypeStatement() { Token = Token.TYPE; }
        public string Identifier { get; set; }
        public TableReference Table { get; set; }
        public List<ColumnDefinition> Columns { get; } = new();
    }
}