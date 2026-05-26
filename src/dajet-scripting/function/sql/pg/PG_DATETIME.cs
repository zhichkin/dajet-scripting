using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_DATETIME : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.DateTime;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Token == Token.NOW)
            {
                script.Append("NOW()::timestamp");
            }
            else if (node.Token == Token.UTC)
            {
                script.Append("NOW() AT TIME ZONE 'UTC'");
            }
        }
    }
}