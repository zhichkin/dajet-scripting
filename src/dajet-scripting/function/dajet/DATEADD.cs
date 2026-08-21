using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class DATEADD : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.DateTime;
        }
    }
}