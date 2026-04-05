using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public sealed class ColumnExpression : SyntaxNode
    {
        public ColumnExpression() { Token = Token.Column; }
        public SyntaxNode Expression { get; set; }
        public string Alias { get; set; } = string.Empty;
        public PropertyDefinition Source { get; set; }
    }
}