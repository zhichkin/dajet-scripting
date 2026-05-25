using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class MIN : SqlFunction
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            DataType type = DataMapper.InferType(parameter);

            return type;
        }
        public override void Visit(in FunctionExpression node, in StringBuilder script, in SqlTranspiler statement)
        {
            script.Append(node.Name);

            script.Append('(');

            SyntaxNode parameter;

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                parameter = node.Parameters[i];

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