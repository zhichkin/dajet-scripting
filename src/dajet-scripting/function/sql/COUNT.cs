using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class COUNT : SqlFunction
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Integer();
        }
        public override void Visit(in FunctionExpression node, in StringBuilder script, in SqlTranspiler statement)
        {
            script.Append(node.Name);

            script.Append('(');

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