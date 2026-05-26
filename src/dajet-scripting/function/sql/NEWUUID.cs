using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class NEWUUID : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Uuid();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            script.Append("NEWID()");
        }
    }
}