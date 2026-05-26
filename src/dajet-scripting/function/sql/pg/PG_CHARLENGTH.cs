using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_CHARLENGTH : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Integer();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            script.Append("CHAR_LENGTH");

            script.Append('(');
            script.Append("CAST(");

            SyntaxNode expression = node.Parameters[0];

            statement.Visit(in expression, in script);

            script.Append(" AS text)");
            script.Append(')');
        }
    }
}