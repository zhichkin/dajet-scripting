using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class CHARLENGTH : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            SyntaxNode expression = node.Parameters[0];

            DataType type = expression.InferType();

            if ((type.IsBinary || type.IsString) && type.Size == 0)
            {
                return DataType.Integer(8); // varbinary(max), nvarchar(max) or varchar(max)
            }

            return DataType.Integer();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            script.Append("LEN").Append('(');

            SyntaxNode expression = node.Parameters[0];

            statement.Visit(in expression, in script);

            script.Append(')');
        }
    }
}