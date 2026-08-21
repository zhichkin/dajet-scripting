using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_DATEDIFF : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Integer();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Token == Token.DATEDIFF)
            {
                script.Append("NOW()::timestamp");
            }
        }
    }
}