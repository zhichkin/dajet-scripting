using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class JSON : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            DataType type = DataMapper.InferType(in parameter);

            if (type.IsObject || type.IsArray)
            {
                return DataType.String();
            }
            else if (type.IsString)
            {
                return DataType.Object;
            }

            throw new InvalidOperationException($"[JSON] Invalid parameter type");
        }
    }
}