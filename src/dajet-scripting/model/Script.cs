using DaJet.TypeSystem;
using System.Text.Json.Nodes;

namespace DaJet.Scripting.Model
{
    public sealed class Script : SyntaxNode
    {
        public Script() { Token = Token.Script; }
        public string Path { get; set; } = string.Empty;
        public bool RunAtStartup { get; internal set; }
        public bool IsLongRunning { get; internal set; }
        public bool IsSingleton { get; internal set; }
        public string SingletonKey { get; internal set; } = string.Empty;
        public List<SyntaxNode> Statements { get; } = new();

        ///<summary>Get the definition of a variable by its name, including the leading @ symbol.</summary>
        public DeclareStatement GetVariableByName(in string name)
        {
            foreach (SyntaxNode node in Statements)
            {
                if (node is DeclareStatement declare && declare.Identifier == name)
                {
                    return declare;
                }
            }

            return null;
        }
        
        public List<SqlStatement> GetSqlStatements()
        {
            List<SqlStatement> statements = new();

            foreach (SyntaxNode node in Statements)
            {
                if (node is SqlStatement statement)
                {
                    statements.Add(statement);
                }
                else if (node is UseStatement use)
                {
                    Extract(in use, in statements);
                }
            }

            return statements;
        }
        private static void Extract(in UseStatement use, in List<SqlStatement> statements)
        {
            foreach (SyntaxNode node in use.Statements)
            {
                if (node is SqlStatement statement)
                {
                    statements.Add(statement);
                }
            }
        }

        public JsonObject GetInputJsonSchema()
        {
            JsonArray required = new();
            JsonObject properties = new();

            JsonObject input = new()
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false
            };

            foreach (SyntaxNode node in Statements)
            {
                if (node is DeclareStatement declare && !declare.IsPrivate)
                {
                    DataType type = declare.Type;

                    if (type.IsObject)
                    {
                        continue; // object input parameters is not supported ?!
                    }

                    string name = declare.Identifier.TrimStart('@');

                    if (declare.Initializer is null)
                    {
                        required.Add(name);
                    }

                    JsonObject jsonType = GetTypeJsonSchema(type);

                    properties.Add(name, jsonType);
                }
            }

            return input;
        }
        public JsonObject GetOutputJsonSchema()
        {
            JsonArray required = new();
            JsonObject properties = new();

            // { "type": "object" }
            // { "type": "object", "additionalProperties": false }

            JsonObject output = new() // $schema
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false
            };

            DefineStatement schema = GetOutputSchema();

            if (schema is null)
            {
                return output;
            }
            
            foreach (DefineProperty property in schema.Properties)
            {
                string name = property.Name;
                DataType type = property.Type;

                required.Add(name);
                properties.Add(name, GetTypeJsonSchema(type));
            }

            return output;
        }
        private static JsonObject GetTypeJsonSchema(DataType type)
        {
            JsonObject json = new();

            if (type.IsObject)
            {
                json.Add("type", "object");
            }
            else if (type.IsUnion)
            {
                if (type.IsEntityUnion)
                {
                    json.Add("type", "string");
                    json.Add("pattern", "^{\\d+:[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}}$");
                }
                else
                {
                    JsonArray union = new();

                    if (type.IsDateTime)
                    {
                        union.Add(new JsonObject() { ["type"] = "string", ["format"] = "date-time" });
                    }

                    if (type.IsEntity)
                    {
                        union.Add(new JsonObject()
                        {
                            ["type"] = "string",
                            ["pattern"] = "\"^{\\\\d+:[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}}$\""
                        });
                    }

                    if (type.IsString)
                    {
                        union.Add(new JsonObject() { ["type"] = "string" });
                    }

                    if (type.IsBoolean)
                    {
                        union.Add(new JsonObject() { ["type"] = "boolean" });
                    }

                    if (type.IsDecimal)
                    {
                        union.Add(new JsonObject() { ["type"] = "number" });
                    }

                    json.Add("oneOf", union);
                }
            }
            else if (type.IsBoolean)
            {
                json.Add("type", "boolean");
            }
            else if (type.IsDecimal || type.IsInteger)
            {
                json.Add("type", "number");
            }
            else if (type.IsDateTime)
            {
                json.Add("type", "string");
                json.Add("format", "date-time");
            }
            else if (type.IsString)
            {
                json.Add("type", "string");
            }
            else if (type.IsBinary)
            {
                json.Add("type", "string");
                json.Add("contentEncoding", "base64");
            }
            else if (type.IsUuid)
            {
                json.Add("type", "string");
                json.Add("format", "uuid");
            }
            else if (type.IsEntity) // {integer:uuid}
            {
                json.Add("type", "string");
                json.Add("pattern", "^{\\d+:[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}}$");
            }

            if (type.IsArray)
            {
                json = new JsonObject()
                {
                    ["type"] = "array",
                    ["items"] = json
                };
            }
            
            return json;
        }

        public DefineStatement GetInputSchema()
        {
            DefineStatement input = new();

            List<DefineProperty> properties = input.Properties;

            foreach (SyntaxNode node in Statements)
            {
                if (node is DeclareStatement declare
                    && !declare.IsPrivate
                    && !declare.Type.IsArray
                    && !declare.Type.IsObject)
                {
                    string name = declare.Identifier.TrimStart('@');

                    properties.Add(new DefineProperty()
                    {
                        Name = name,
                        Type = declare.Type,
                        Schema = declare.Schema
                    });
                }
            }

            return input;
        }
        public DefineStatement GetOutputSchema()
        {
            ReturnStatement _return = GetReturnStatement();

            if (_return is null) { return null; }

            DefineStatement schema = GetOutputSchema(_return.Expression);

            return schema;
        }
        private ReturnStatement GetReturnStatement()
        {
            int count = Statements.Count - 1;

            for (int i = count; i >= 0; i--)
            {
                SyntaxNode node = Statements[i];

                if (node is ReturnStatement _return)
                {
                    return _return;
                }
            }

            return null;
        }
        private static DefineStatement GetOutputSchema(in SyntaxNode expression)
        {
            if (expression is ScalarExpression scalar)
            {
                return GetOutputSchema(in scalar);
            }
            else if (expression is VariableReference variable)
            {
                return GetOutputSchema(in variable);
            }
            else if (expression is MemberAccessExpression member)
            {
                return GetOutputSchema(in member);
            }
            else if (expression is FunctionExpression function)
            {
                return GetOutputSchema(in function);
            }

            return null;
        }
        private static DefineStatement GetOutputSchema(in ScalarExpression expression)
        {
            if (expression is null)
            {
                return null;
            }

            DefineStatement schema = new();

            DefineProperty property = new() { Name = "value" };

            if (expression.Token == Token.Boolean)
            {
                property.Type = DataType.Boolean;
            }
            else if (expression.Token == Token.Integer)
            {
                property.Type = DataType.Integer();
            }
            else if (expression.Token == Token.Decimal)
            {
                property.Type = DataType.Decimal();
            }
            else if (expression.Token == Token.DateTime)
            {
                property.Type = DataType.DateTime;
            }
            else if (expression.Token == Token.String)
            {
                property.Type = DataType.String();
            }
            else if (expression.Token == Token.Uuid)
            {
                property.Type = DataType.Uuid();
            }
            else if (expression.Token == Token.Entity)
            {
                property.Type = DataType.Entity();
            }
            else
            {
                property.Type = DataType.String();
            }

            schema.Properties.Add(property);

            return schema;
        }
        private static DefineStatement GetOutputSchema(in VariableReference expression)
        {
            if (expression.Binding is not DeclareStatement declare)
            {
                return null; // critical error - unbound variable
            }

            DataType type = declare.Type;

            if (type.IsObject)
            {
                return declare.Binding; // complex type
            }

            DefineStatement schema = new(); // simple type

            DefineProperty property = new()
            {
                Name = type.IsArray ? "array" : "value",
                Type = type
            };

            schema.Properties.Add(property);

            return schema;
        }
        private static DefineStatement GetOutputSchema(in MemberAccessExpression expression)
        {
            if (expression.Binding is not DeclareStatement declare)
            {
                return null; // critical error - unbound variable
            }

            List<string> members = expression.GetAccessMembers();

            string member = members[0];

            if (declare.Binding is not DefineStatement schema)
            {
                return null; // critical error - unbound schema
            }

            DefineProperty property = schema.GetPropertyByName(in member);

            if (property is null)
            {
                return null; // critical error - unbound member access
            }

            DefineStatement output = new();

            output.Properties.Add(new DefineProperty()
            {
                Name = "value",
                Type = property.Type,
                Schema = property.Schema
            });

            return schema;
        }
        private static DefineStatement GetOutputSchema(in FunctionExpression expression)
        {
            if (expression is FunctionExpression function
                && function.Name == nameof(JSON)
                && function.Parameters[0] is VariableReference parameter
                && parameter.Binding is DeclareStatement declare)
            {
                return declare.Binding;
            }

            return null;
        }
    }
}