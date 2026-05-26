using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_SUBSTRING : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.String();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            // SUBSTRING ( <expression>, start [, length] )

            script.Append(node.Name).Append('(');

            SyntaxNode parameter = node.Parameters[0];

            script.Append("CAST(");

            statement.Visit(in parameter, in script);

            script.Append(" AS varchar)");

            for (int i = 1; i < node.Parameters.Count; i++)
            {
                parameter = node.Parameters[i];

                script.Append(", ");

                statement.Visit(in parameter, in script);
            }

            script.Append(')');
        }
    }
}