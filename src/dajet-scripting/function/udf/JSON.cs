using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Scripting
{
    public sealed class JSON : UdfFunction
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
        internal override Type Evaluate(in ExpressionCompiler context, in FunctionExpression node, in ILGenerator IL)
        {
            foreach (SyntaxNode parameter in node.Parameters)
            {
                Type type = context.Evaluate(in parameter, in IL); // push parameter value onto stack
            }

            IL.Emit(OpCodes.Call, _method); // push return value onto stack

            return typeof(string);
        }

        private static readonly MethodInfo _method = typeof(JSON).GetMethod(nameof(JSON.Execute),
                BindingFlags.Static | BindingFlags.Public, [typeof(object)]);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Execute(object value)
        {
            return JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
        }
    }
}