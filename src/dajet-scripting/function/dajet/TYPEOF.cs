using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class TYPEOF : DaJetFunction
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Integer();
        }
    }
}