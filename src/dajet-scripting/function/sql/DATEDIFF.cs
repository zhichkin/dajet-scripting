using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class DATEDIFF : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Integer();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Parameters.Count != 3)
            {
                throw new InvalidOperationException($"[DATEDIFF] invalid parameters number");
            }

            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATEDIFF] the first parameter must be string");
            }

            string datepart = scalar.Literal;

            script.Append(string.Format("DATEDIFF({0}, ", datepart));

            expression = node.Parameters[1];
            statement.Visit(in expression, in script);
            script.Append(',').Append(' ');

            expression = node.Parameters[2];
            statement.Visit(in expression, in script);
            script.Append(')');
        }
    }
}