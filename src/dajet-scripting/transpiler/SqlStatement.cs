using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class SqlStatement
    {
        public SyntaxNode Node { get; set; }
        public StringBuilder Script { get; } = new();
        public string Sql { get; set; }
        public int YearOffset { get; set; }
        public List<SyntaxNode> Input { get; } = new(); // VariableReference, MemberAccessExpression
        public EntityDefinition Output { get; set; } // {SELECT,STREAM,CONSUME} INTO | {INSERT,UPDATE,DELETE} OUTPUT


        //public List<SyntaxNode> PostProcessing { get; } = new(); // FunctionExpression
    }
}