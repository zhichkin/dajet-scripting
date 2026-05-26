using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_VECTOR : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Decimal(15); //TODO: DataType.Integer(8)
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            script.Append("nextval('");

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is ScalarExpression scalar)
            {
                script.Append(scalar.Literal);
            }

            script.Append("')");
        }
    }
}