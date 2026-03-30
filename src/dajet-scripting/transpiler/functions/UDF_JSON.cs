using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class UDF_JSON
    {
        public const string Name = "JSON";
        public Type GetReturnType(in FunctionExpression node) { return typeof(string); }
        public FunctionDescriptor Transpile(in SqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name != UDF_JSON.Name)
            {
                throw new FormatException($"[JSON] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count == 0)
            {
                throw new FormatException("[JSON] parameter missing");
            }

            if (node.Parameters.Count > 1)
            {
                throw new FormatException("[JSON] too many parameters");
            }

            FunctionDescriptor descriptor;

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is VariableReference variable)
            {
                descriptor = Transpile(in transpiler, in variable, in script);
            }
            else
            {
                throw new FormatException("[JSON] invalid parameter type");
            }

            if (descriptor is not null)
            {
                descriptor.Node = node;
            }

            return descriptor;
        }
        private FunctionDescriptor Transpile(in SqlTranspiler transpiler, in VariableReference variable, in StringBuilder script)
        {
            if (variable.Binding is not TypeIdentifier type)
            {
                throw new FormatException("[JSON] invalid variable binding");
            }

            if (!(type.Token == Token.Object || type.Token == Token.Array))
            {
                throw new FormatException("[JSON] invalid variable type");
            }

            string parameterName = $"@JSON_" + variable.Identifier[1..];

            if (transpiler is PgSqlTranspiler)
            {
                script.Append("CAST(").Append(parameterName).Append(" AS mvarchar)");
            }
            else
            {
                script.Append(parameterName);
            }

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = GetReturnType(null)
            };

            return descriptor;
        }
    }
}