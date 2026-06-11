using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class ANALYTIC : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            DataType type = parameter.InferType();

            return type;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            // LAG | LEAD | LAST_VALUE | FIRST_VALUE

            script.Append(node.Name).Append('(');

            SyntaxNode parameter;

            List<SyntaxNode> parameters = node.Parameters;

            for (int i = 0; i < parameters.Count; i++)
            {
                parameter = parameters[i];

                if (i > 0) { script.Append(", "); }

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