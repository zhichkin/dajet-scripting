using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class MS_UUID1C : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Uuid();
        }
        public override PropertyDefinition InferProperty(in FunctionExpression node)
        {
            PropertyDefinition property = new()
            {
                Type = GetReturnType(in node),
                Purpose = PropertyPurpose.Property
            };

            property.Columns.Add(new ColumnDefinition()
            {
                Type = DataType.Binary(16),
                Purpose = ColumnPurpose.Value
            });

            return property;
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            List<SyntaxNode> parameters = node.Parameters;

            if (parameters is null || parameters.Count == 0)
            {
                throw new InvalidOperationException("[MS_UUID1C] Parameter is missing.");
            }

            if (parameters.Count > 1)
            {
                throw new InvalidOperationException("[MS_UUID1C] Too many parameters.");
            }

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is not ColumnReference column)
            {
                throw new InvalidOperationException("[MS_UUID1C] The function parameter must be a database column reference.");
            }
            
            ColumnDefinition source;
            ColumnReference identity = null;

            //TODO: identity = column.ToSingleColumnReference(ColumnPurpose.Identity);

            if (column.Binding is PropertyDefinition property) // Прямой источник данных
            {
                source = property.GetColumnByPurpose(ColumnPurpose.Value);

                source ??= property.GetColumnByPurpose(ColumnPurpose.Identity);

                if (source is not null)
                {
                    PropertyDefinition binding = new() { Columns = [source] };

                    identity = new ColumnReference()
                    {
                        Binding = binding,
                        Identifier = column.Identifier
                    };
                }
            }
            else if (column.Binding is ColumnExpression derived) // Наследуемый источник данных
            {
                if (derived.Source is not null) 
                {
                    property = derived.Source;

                    source = property.GetColumnByPurpose(ColumnPurpose.Value);

                    source ??= property.GetColumnByPurpose(ColumnPurpose.Identity);

                    if (source is not null)
                    {
                        identity = column.ToSingleColumnReference(in source);
                    }
                }
                else // Константа, параметр, функция или выражение
                {
                    throw new InvalidOperationException("[MS_UUID1C] The function parameter must be a database column reference.");
                }
            }
            else if (column.Binding is Entity) // enumeration value
            {
                identity = column; // Will be transpiled to binary(16) constant value.
            }

            if (identity is null)
            {
                throw new InvalidOperationException($"[MS_UUID1C] Database column '{column.Identifier}' must be of either 'entity' or 'uuid' data type.");
            }
            
            script.Append("CAST(REVERSE(SUBSTRING(");
            
            statement.Visit(identity, in script); // identity column reference

            script.Append(", 9, 8)) AS binary(8)) + SUBSTRING(");

            statement.Visit(identity, in script); // identity column reference

            script.Append(", 1, 8)");
        }
    }
}