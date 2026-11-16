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
            SyntaxNode parameter = node.Parameters[0];

            if (parameter is not ColumnReference column)
            {
                throw new InvalidOperationException($"[UUID1C] The first parameter must be a database column reference.");
            }

            ColumnDefinition source = null;
            ColumnReference identity = null;

            if (column.Binding is PropertyDefinition property) // Прямой источник данных
            {
                source = property.GetColumnByPurpose(ColumnPurpose.Value);

                source ??= property.GetColumnByPurpose(ColumnPurpose.Identity);

                if (source is null)
                {
                    // Что-то пошло не так ...
                }

                PropertyDefinition binding = new() { Columns = [source] };

                identity = new ColumnReference()
                {
                    Binding = binding,
                    Identifier = column.Identifier
                };
            }
            else if (column.Binding is ColumnExpression derived) // Наследуемый источник данных
            {
                if (derived.Source is not null) 
                {
                    property = derived.Source;

                    source = property.GetColumnByPurpose(ColumnPurpose.Value);

                    identity = CreateSingleColumnReference(in column, in source);
                }
                else
                {
                    // Константа, параметр, функция или выражение
                }
            }
            else if (column.Binding is Entity)
            {
                // enumeration value
            }

            if (identity is null)
            {
                throw new InvalidOperationException("[UUID1C] Failed to infer identity column reference.");
            }
            
            script.Append("CAST(REVERSE(SUBSTRING(");
            
            statement.Visit(identity, in script); // identity column reference

            script.Append(", 9, 8)) AS binary(8)) + SUBSTRING(");

            statement.Visit(identity, in script); // identity column reference

            script.Append(", 1, 8)");
        }
        private static ColumnReference CreateSingleColumnReference(in ColumnReference column, in ColumnDefinition source)
        {
            string columnName = string.Format("{0}_{1}", column.ColumnName, source.Purpose.GetSuffix());

            string identifier = string.Format("{0}_{1}", column.Identifier, source.Purpose.GetSuffix());

            PropertyDefinition binding = new()
            {
                Columns =
                [
                    new ColumnDefinition()
                    {
                        Name = columnName,
                        Type = source.Type,
                        Purpose = source.Purpose
                    }
                ]
            };

            return new ColumnReference()
            {
                Binding = binding,
                Identifier = identifier
            };
        }
    }
}