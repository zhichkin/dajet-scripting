using DaJet.Json;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Scripting
{
    internal static class FunctionInterpreter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static FunctionInterpreter()
        {
            JsonOptions.Converters.Add(new DataTypeJsonConverter());
            JsonOptions.Converters.Add(new DataObjectJsonConverter());
            JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }
        internal static object Evaluate(in ExpressionInterpreter context, in FunctionExpression node)
        {
            if (node.Name == nameof(JSON)) { return JSON(in context, in node); }
            else if (node.Name == nameof(TYPEOF)) { return TYPEOF(in context, in node); }
            else if (node.Name == nameof(UUIDOF)) { return UUIDOF(in context, in node); }
            else
            {
                return null;
            }
        }
        private static string JSON(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is not null)
            {
                return JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
            }

            return string.Empty;
        }
        private static int TYPEOF(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is Entity entity)
            {
                return entity.TypeCode;
            }
            
            return 0;
        }
        private static Guid UUIDOF(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is Entity entity)
            {
                return entity.Identity;
            }

            return Guid.Empty;
        }
    }
}