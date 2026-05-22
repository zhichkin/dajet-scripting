using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public abstract class DaJetFunction
    {
        public abstract DataType GetReturnType(in FunctionExpression node);
    }
}