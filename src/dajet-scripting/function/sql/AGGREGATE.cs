using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class AGGREGATE : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            DataType type = parameter.InferType();

            return type;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            // AVG | MIN | MAX | SUM

            script.Append(node.Name).Append('(');

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is StarExpression)
            {
                script.Append('*');
            }
            else
            {
                statement.Visit(in parameter, in script);
            }

            script.Append(')');

            if (node.Over is not null)
            {
                script.Append(' ');

                statement.Visit(node.Over, in script);
            }
        }
    }
}