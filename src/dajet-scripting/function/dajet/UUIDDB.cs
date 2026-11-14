using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class UUIDDB : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Uuid();
        }
    }
}