using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class ERROR_MESSAGE : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.String();
        }
    }
}