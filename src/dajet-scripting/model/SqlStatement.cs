using DaJet.Data;
using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public abstract class SqlStatement : SyntaxNode
    {
        public DataSourceType Dialect { get; set; } // SqlServer | PostgreSQL
        public int YearOffset { get; set; }
        public string Sql { get; set; }
        public List<SyntaxNode> Input { get; set; } = new(); // VariableReference, MemberAccessExpression, FunctionExpression
        public SyntaxNode Output { get; set; } // INTO clause VariableReference, TableReference | OUTPUT clause
        public DefineStatement InferSchema()
        {
            List<ColumnExpression> columns = GetColumnExpressions(this);

            if (columns is null)
            {
                return null;
            }

            DefineStatement schema = new();
            ColumnExpression column;
            PropertyDefinition source;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                source = column.InferSource(); //TODO: CASE infers single entity if can be returned multiple

                DefineProperty property = new()
                {
                    Type = source.Type
                };

                // Источник данных - выражение

                if (string.IsNullOrEmpty(source.Name))
                {
                    if (!string.IsNullOrEmpty(column.Alias))
                    {
                        property.Name = column.Alias;
                    }
                    else if (column.Expression is ColumnReference field)
                    {
                        property.Name = field.ColumnName;
                    }

                    schema.Properties.Add(property);

                    if (column.Source is null)
                    {
                        // Константа, функция, параметр или выражение
                    }

                    continue; //TODO: fix it by refactoring
                }

                // Схема данных (колонки таблицы СУБД)

                if (!string.IsNullOrEmpty(column.Alias))
                {
                    property.Name = column.Alias;
                }
                else if (column.Expression is ColumnReference field)
                {
                    property.Name = field.ColumnName;
                }
                else if (!string.IsNullOrEmpty(source.Name))
                {
                    property.Name = source.Name;
                }
                else
                {
                    throw new InvalidCastException("Property name missing");
                }

                schema.Properties.Add(property);
            }

            return schema;
        }
        public EntityDefinition InferEntity()
        {
            List<ColumnExpression> columns = GetColumnExpressions(this);

            if (columns is null)
            {
                return null;
            }

            EntityDefinition entity = new();
            ColumnExpression column;
            PropertyDefinition source;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                source = column.InferSource(); //TODO: CASE infers single entity if can be returned multiple

                PropertyDefinition property = new()
                {
                    Type = source.Type,
                    Purpose = PropertyPurpose.Property
                };

                // Источник данных - выражение

                if (string.IsNullOrEmpty(source.Name))
                {
                    if (!string.IsNullOrEmpty(column.Alias))
                    {
                        property.Name = column.Alias;
                    }
                    else if (column.Expression is ColumnReference field)
                    {
                        property.Name = field.ColumnName;
                    }

                    entity.Properties.Add(property);

                    if (column.Source is null) // Константа, функция, параметр или выражение
                    {
                        property.Columns.Add(new ColumnDefinition()
                        {
                            Type = property.Type,
                            Purpose = ColumnPurpose.Value
                        });
                    }

                    continue; //FIXME: refactor this ugly if ... continue
                }

                // Схема данных (колонки таблицы СУБД)

                if (!string.IsNullOrEmpty(column.Alias))
                {
                    property.Name = column.Alias;
                }
                else if (column.Expression is ColumnReference field)
                {
                    property.Name = field.ColumnName;
                }
                else if (!string.IsNullOrEmpty(source.Name))
                {
                    property.Name = source.Name;
                }
                else
                {
                    throw new InvalidCastException("Property name missing");
                }

                //NOTE: the same column references brakes mapping output (see MsDataMapper)
                //NOTE: that is why we need to make copy of columns

                ColumnDefinition copy;
                ColumnDefinition original;

                for (int c = 0; c < source.Columns.Count; c++)
                {
                    original = source.Columns[c];

                    copy = new ColumnDefinition()
                    {
                        Name = original.Name,
                        Type = original.Type,
                        Purpose = original.Purpose
                    };

                    property.Columns.Add(copy);
                }

                entity.Properties.Add(property);
            }

            return entity;
        }
        private static List<ColumnExpression> GetColumnExpressions(in SqlStatement node)
        {
            if (node is SelectStatement select)
            {
                return GetColumnExpressions(in select);
            }
            else if (node is ConsumeStatement consume)
            {
                return consume.Columns;
            }

            return null;
        }
        private static List<ColumnExpression> GetColumnExpressions(in SelectStatement node)
        {
            if (node.Expression is SelectExpression select)
            {
                return select.Columns;
            }
            else if (node.Expression is TableUnionOperator union)
            {
                return GetColumnExpressions(in union);
            }

            return null;
        }
        private static List<ColumnExpression> GetColumnExpressions(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select)
            {
                return select.Columns;
            }

            return null;
        }

        //TODO: public List<SyntaxNode> PostProcessing { get; } = new(); // FunctionExpression
    }
}