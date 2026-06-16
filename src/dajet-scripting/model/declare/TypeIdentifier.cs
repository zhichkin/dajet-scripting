using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public sealed class TypeReference : SyntaxNode
    {
        public TypeReference() { Token = Token.Type; }
        public DataType Type { get; set; }
        public string Schema { get; set; } = string.Empty;
        public MetadataEntry Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Type} {{{Schema}}}]";
        }
    }
}