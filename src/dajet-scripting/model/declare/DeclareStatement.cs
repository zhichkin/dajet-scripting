using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public sealed class DeclareStatement : SyntaxNode
    {
        // DECLARE @variable string = 'string value'
        // DECLARE @variable object OF Справочник.Номенклатура
        public DeclareStatement() { Token = Token.DECLARE; }
        public bool IsPrivate { get; set; } // @variable visibility scope
        public string Identifier { get; set; } // @variable
        public DataType Type { get; set; } // DataType
        public string Schema { get; set; } // [optional] keyword OF is used to define data schema of object or array types
        public DefineStatement Binding { get; set; } // schema binding (inferred from script)
        public SyntaxNode Initializer { get; set; } // [optional] for example ScalarExpression
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
    }
}