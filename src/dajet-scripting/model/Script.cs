using DaJet.TypeSystem;
using System.Text.Json.Nodes;

namespace DaJet.Scripting.Model
{
    public sealed class Script : SyntaxNode
    {
        public Script() { Token = Token.Script; }
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

        //{
        //  "type": "object",
        //  "properties": {
        //    "test": {
        //      "type": "array",
        //      "items": { "type": "string" }
        //    }
        //  },
        //  "required": [ "test" ]
        //}

        //{
        //  "type": "array",
        //  "items": {
        //    "type": "object",
        //    "properties": {
        //      "test": {
        //        "type": "array",
        //        "items": { "type": "string" }
        //      }
        //    },
        //    "required": [ "test" ]
        //  }
        //}

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

                    string name = declare.Identifier.TrimStart('@');

                    if (type.IsBoolean)
                    {
                        required.Add(name);
                        properties.Add(name, new JsonObject() { ["type"] = "boolean" });
                    }
                    else if (type.IsInteger || type.IsDecimal)
                    {
                        required.Add(name);
                        properties.Add(name, new JsonObject() { ["type"] = "number" });
                    }
                    else if (type.IsDateTime)
                    {
                        required.Add(name);
                        properties.Add(name, new JsonObject() { ["type"] = "string", ["format"] = "date-time" });
                    }
                    else if (type.IsString)
                    {
                        required.Add(name);
                        properties.Add(name, new JsonObject() { ["type"] = "string" });
                    }
                    else if (type.IsUuid)
                    {
                        required.Add(name);
                        properties.Add(name, new JsonObject() { ["type"] = "string", ["format"] = "uuid" });
                    }
                    else if (type.IsEntity) // {integer:uuid}
                    {
                        required.Add(name);
                        properties.Add(name, new JsonObject() { ["type"] = "string", ["pattern"] = "^{\\d+:[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}}$" });
                    }
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

                if (type.IsBoolean)
                {
                    required.Add(name);
                    properties.Add(name, new JsonObject() { ["type"] = "boolean" });
                }
                else if (type.IsInteger || type.IsDecimal)
                {
                    required.Add(name);
                    properties.Add(name, new JsonObject() { ["type"] = "number" });
                }
                else if (type.IsDateTime)
                {
                    required.Add(name);
                    properties.Add(name, new JsonObject() { ["type"] = "string", ["format"] = "date-time" });
                }
                else if (type.IsString)
                {
                    required.Add(name);
                    properties.Add(name, new JsonObject() { ["type"] = "string" });
                }
                else if (type.IsUuid)
                {
                    required.Add(name);
                    properties.Add(name, new JsonObject() { ["type"] = "string", ["format"] = "uuid" });
                }
                else if (type.IsEntity) // {integer:uuid}
                {
                    required.Add(name);
                    properties.Add(name, new JsonObject() { ["type"] = "string", ["pattern"] = "^{\\d+:[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}}$" });
                }
                else if (type.IsUnion)
                {
                    //TODO: "name": { "oneOf": [
                    // { "type": "string", "format": "date-time" },
                    // { "type": "string", "format": "uuid" },
                    // { "type": "string", "pattern": "^{\\d+:[0-9A-Fa-f]{8}-([0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}}$" },
                    // { "type": "string", },
                    // { "type": "boolean" },
                    // { "type": "number"  }
                    //]
                }
            }

            return output;
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

            if (type.IsObject || type.IsArray)
            {
                return declare.Binding; // complex type
            }

            DefineStatement schema = new(); // simple type

            DefineProperty property = new() { Name = "value" };

            if (type.IsBoolean)
            {
                property.Type = DataType.Boolean;
            }
            else if (type.IsInteger)
            {
                property.Type = DataType.Integer();
            }
            else if (type.IsDecimal)
            {
                property.Type = DataType.Decimal();
            }
            else if (type.IsDateTime)
            {
                property.Type = DataType.DateTime;
            }
            else if (type.IsString)
            {
                property.Type = DataType.String();
            }
            else if (type.IsUuid)
            {
                property.Type = DataType.Uuid();
            }
            else if (type.IsEntity)
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