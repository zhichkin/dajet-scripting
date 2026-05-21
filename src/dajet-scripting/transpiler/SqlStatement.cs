using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class SqlStatement
    {
        public SqlStatement(in SyntaxNode node)
        {
            Node = node;
        }
        public SyntaxNode Node { get; private set; }
        public DataSourceType Dialect { get; set; } // SqlServer | PostgreSQL
        
        public string Sql { get; set; }
        public List<SyntaxNode> Input { get; } = new(); // VariableReference, MemberAccessExpression
        public SyntaxNode Output { get; set; } // INTO clause VariableReference, TableReference
        
        //public List<SyntaxNode> PostProcessing { get; } = new(); // FunctionExpression
    }
}