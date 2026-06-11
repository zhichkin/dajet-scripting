using DaJet.Data;

namespace DaJet.Scripting.Model
{
    public abstract class SqlStatement : SyntaxNode
    {
        public DataSourceType Dialect { get; set; } // SqlServer | PostgreSQL
        public int YearOffset { get; set; }
        public string Sql { get; set; }
        public List<SyntaxNode> Input { get; } = new(); // VariableReference, MemberAccessExpression
        public SyntaxNode Output { get; set; } // INTO clause VariableReference, TableReference
        
        //TODO: public List<SyntaxNode> PostProcessing { get; } = new(); // FunctionExpression
    }
}