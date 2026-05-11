using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class SUM : SqlFunction
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Integer(); //TODO: infer parameter data type
        }
        public override void Visit(in FunctionExpression node, in StringBuilder script, in IStatementTranspiler statement)
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