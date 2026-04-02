using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public sealed class DefineProperty : SyntaxNode
    {
        public DefineProperty() { Token = Token.PROPERTY; }
        public string Name { get; set; } = string.Empty;
        public DataType Type { get; set; }
        public string Schema { get; set; } = string.Empty;
        public override string ToString()
        {
            return $"[{Token}: {Name} {Type} OF {Schema}]";
        }
    }
}