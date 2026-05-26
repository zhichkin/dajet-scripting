using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class EXISTS : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Boolean;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            //NOTE: The function has only one parameter - a subquery (TableExpression).

            script.Append("EXISTS");

            SyntaxNode expression = node.Parameters[0];

            statement.Visit(in expression, in script);
        }
    }
}