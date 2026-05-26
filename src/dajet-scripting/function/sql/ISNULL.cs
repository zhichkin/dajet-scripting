using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class ISNULL : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            DataType type = DataMapper.InferType(parameter);

            return type;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            script.Append(node.Name).Append('(');

            SyntaxNode parameter;

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                parameter = node.Parameters[i];

                if (i > 0) { script.Append(", "); }

                statement.Visit(in parameter, in script);
            }

            script.Append(')');
        }
    }
}