using DaJet.TypeSystem;

namespace DaJet.Scripting.Model
{
    public sealed class SelectStatement : SqlStatement
    {
        public SelectStatement() { Token = Token.SELECT; }
        public SyntaxNode Expression { get; set; }
        public CommonTableExpression CommonTables { get; set; }
        public bool IsStream { get; set; } // STREAM statement
        public IntoClause GetIntoClause()
        {
            if (Expression is SelectExpression select)
            {
                return select.Into;
            }
            
            if (Expression is TableUnionOperator union &&
                union.Expression1 is SelectExpression first)
            {
                
                return first.Into;
            }

            return null;
        }

        public DefineStatement InferSchema()
        {
            DefineStatement schema = new();

            List<ColumnExpression> columns = GetColumnExpressions(this);

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
                    else if (column.Expression is ColumnReference derived)
                    {
                        property.Name = derived.Identifier;
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
                else if (column.Expression is ColumnReference derived)
                {
                    property.Name = derived.Identifier;
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
            EntityDefinition entity = new();

            List<ColumnExpression> columns = GetColumnExpressions(this);

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
                    else if (column.Expression is ColumnReference derived)
                    {
                        property.Name = derived.Identifier;
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
                else if (column.Expression is ColumnReference derived)
                {
                    property.Name = derived.Identifier;
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
    }
}