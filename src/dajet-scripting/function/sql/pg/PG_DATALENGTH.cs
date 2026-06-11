using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_DATALENGTH : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Integer();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            SyntaxNode expression = node.Parameters[0];

            DataType type = expression.InferType();

            if (type.IsString)
            {
                script.Append("OCTET_LENGTH");
            }
            else
            {
                script.Append("LENGTH");
            }

            script.Append('(');

            if (type.IsString)
            {
                script.Append("CAST(");
            }

            statement.Visit(in expression, in script);

            if (type.IsString)
            {
                script.Append(" AS text)");
            }

            script.Append(')');
        }
    }
}