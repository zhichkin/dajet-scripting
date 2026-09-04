using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsConsumeTranspiler : SqlTranspiler
    {
        private MetadataProvider _provider;
        private ConsumeStatement _statement;
        public override void Visit(in SyntaxNode expression, in StringBuilder script)
        {
            throw new NotImplementedException();
        }
        public override bool TryTranspile(in SyntaxNode node, in MetadataProvider provider, out string error)
        {
            error = null;

            if (node is not ConsumeStatement consume)
            {
                throw new InvalidOperationException();
            }

            if (!string.IsNullOrWhiteSpace(consume.Target))
            {
                return true; //TODO: RabbitMQ or Apache Kafka consumer
            }

            ArgumentNullException.ThrowIfNull(provider, nameof(provider));

            _provider = provider;
            YearOffset = _provider.GetYearOffset();
            
            _statement = consume;
            _statement.Dialect = _provider.DataSource;
            _statement.YearOffset = YearOffset;

            try
            {
                Transpile(in _statement);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            _provider = null;
            _statement = null;

            return error is null;
        }
        private void Transpile(in ConsumeStatement statement)
        {
            SelectStatement select = TransformConsumeToSelect(in statement);

            if (!new MsSelectTranspiler().TryTranspile(select, in _provider, out string error))
            {
                throw new Exception(error);
            }

            bool ordered = statement.Order is not null && (statement.Order.Expressions.Count > 0);

            StringBuilder sql = new();

            if (ordered)
            {
                DeclareTableVariable(in statement, in sql); sql.AppendLine();
            }

            sql.AppendLine("WITH source AS (");

            sql.Append(select.Sql).AppendLine().Append(')').AppendLine();

            statement.Input = select.Input;
            statement.Output = select.GetIntoClause();

            if (ordered)
            {
                sql.AppendLine("DELETE source OUTPUT DELETED.* INTO @output;").AppendLine();

                sql.Append("SELECT * FROM @output ");

                OrderOutputTable(in statement, in sql);

                sql.Append(';');
            }
            else
            {
                sql.Append("DELETE source OUTPUT DELETED.*;");
            }

            statement.Sql = sql.ToString();
        }
        private static string ToSqlDataType(DataType type)
        {
            if (type.IsBoolean) { return "binary(1)"; }
            else if (type.IsDecimal) { return string.Format("numeric({0},{1})", type.Precision, type.Scale); }
            else if (type.IsDateTime) { return "datetime2"; }
            else if (type.IsString) { return (type.Size == 0) ? "nvarchar(max)" : string.Format("{0}({1})", (type.IsFixed) ? "nchar" : "nvarchar", type.Size); }
            else if (type.IsBinary) { return (type.Size == 0) ? "varbinary(max)" : string.Format("binary({0})", type.Size); }
            else if (type.IsUuid) { return "binary(16)"; }
            else if (type.IsEntity) { return "binary(16)"; }
            else if (type.IsInteger) { return (type.Size == 4) ? "int" : "bigint"; }

            throw new InvalidOperationException("Failed to map DaJet data type to SQL data type.");
        }
        private static SelectStatement TransformConsumeToSelect(in ConsumeStatement consume)
        {
            SelectStatement select = new()
            {
                Expression = new SelectExpression()
                {
                    Top = consume.Top,
                    From = consume.From,
                    Into = consume.Into,
                    Where = consume.Where,
                    Order = consume.Order,
                    Columns = consume.Columns
                }
            };
            
            if (consume.From.Expression is TableReference table)
            {
                table.Hints = "WITH (ROWLOCK" + (consume.StrictOrderRequired ? ")" : ", READPAST)");
            }
            
            return select;
        }
        private static void DeclareTableVariable(in ConsumeStatement statement, in StringBuilder sql)
        {
            sql.Append("DECLARE @output TABLE (");
            
            ColumnExpression column;
            List<ColumnExpression> columns = statement.Columns;

            int count = columns.Count;

            for (int i = 0; i < count; i++)
            {
                column = columns[i];

                if (i > 0) { sql.Append(',').Append(' '); }

                DeclareTableColumn(in column, in sql);
            }

            sql.Append(')').Append(';').AppendLine();
        }
        private static void DeclareTableColumn(in ColumnExpression node, in StringBuilder sql)
        {
            string alias = node.Alias;

            DataType type = node.InferType();

            if (node.Expression is ColumnReference column)
            {
                if (column.Binding is PropertyDefinition property)
                {
                    DeclareTableColumn(in alias, in property, in sql);
                }
                else if (column.Binding is ColumnExpression derived)
                {
                    if (derived.Source is null) // Константа, параметр, функция или выражение
                    {
                        sql.Append(node.Alias).Append(' ').Append(ToSqlDataType(type));
                    }
                    else
                    {
                        DeclareTableColumn(in alias, derived.Source, in sql);
                    }
                }
                else if (column.Binding is Entity) // enumeration
                {
                    sql.Append(node.Alias).Append(' ').Append(ToSqlDataType(type));
                }
            }
            else // Константа, параметр, функция или выражение
            {
                sql.Append(node.Alias).Append(' ').Append(ToSqlDataType(type));
            }
        }
        private static void DeclareTableColumn(in string name, in PropertyDefinition property, in StringBuilder sql)
        {
            ColumnDefinition column;

            for (int i = 0; i < property.Columns.Count; i++)
            {
                column = property.Columns[i];

                if (i > 0) { sql.Append(", "); }

                string alias = string.IsNullOrEmpty(name) ? property.Name : name;

                if (property.Columns.Count == 1) // single column
                {
                    sql.Append(alias);
                }
                else // multiple columns
                {
                    sql.Append(alias).Append('_').Append(column.Purpose.GetSuffix());
                }

                sql.Append(' ').Append(ToSqlDataType(column.Type));
            }
        }
        private static void OrderOutputTable(in ConsumeStatement statement, in StringBuilder sql)
        {
            if (statement.Order is not OrderClause clause)
            {
                return;
            }

            sql.Append("ORDER BY ");

            OrderExpression order;
            List<OrderExpression> expressions = clause.Expressions;

            for (int i = 0; i < expressions.Count; i++)
            {
                order = expressions[i];

                if (i > 0) { sql.Append(", "); }

                if (order.Expression is ColumnReference column)
                {
                    string identifier = column.ColumnName;

                    if (column.Binding is PropertyDefinition property)
                    {
                        OrderOutputColumn(in identifier, order.Token, in property, in sql);
                    }
                    else if (column.Binding is ColumnExpression derived)
                    {
                        if (derived.Source is not null)
                        {
                            OrderOutputColumn(in identifier, order.Token, derived.Source, in sql);
                        }
                        else // Константа, параметр, функция или выражение
                        {
                            // ??? sql.Append(node.Alias);
                        }
                    }
                    else if (column.Binding is Entity) // enumeration
                    {
                        // ???
                    }


                }
                else // Константа, параметр, функция или выражение
                {
                    // ??? sql.Append(node.Alias);
                }
            }
        }
        private static void OrderOutputColumn(in string identifier, Token order, in PropertyDefinition binding, in StringBuilder sql)
        {
            ColumnDefinition column;
            List<ColumnDefinition> columns = binding.Columns;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                if (i > 0) { sql.Append(", "); }

                if (binding.Columns.Count == 1) // single column
                {
                    sql.Append(identifier);
                }
                else // multiple columns
                {
                    sql.Append(identifier).Append('_').Append(column.Purpose.GetSuffix());
                }

                if (order == Token.ASC)
                {
                    sql.Append(" ASC");
                }
                else
                {
                    sql.Append(" DESC");
                }
            }
        }
    }
}

//WITH queue AS
//(SELECT TOP (@MessageCount)
//  МоментВремени, Идентификатор, ДатаВремя,
//  Отправитель, Получатели, Заголовки,
//  ТипОперации, ТипСообщения, ТелоСообщения
//FROM
//  {TABLE_NAME} WITH (ROWLOCK, READPAST)
//ORDER BY
//  МоментВремени ASC,
//  Идентификатор ASC
//)
//DELETE queue OUTPUT
//  deleted.МоментВремени, deleted.Идентификатор, deleted.ДатаВремя,
//  deleted.Отправитель, deleted.Получатели, deleted.Заголовки,
//  deleted.ТипОперации, deleted.ТипСообщения, deleted.ТелоСообщения
//;

//DECLARE @result TABLE(id binary(16));
//WITH changes AS 
//(SELECT TOP (10)
//Изменения._NodeTRef AS УзелОбмена_TRef, Изменения._NodeRRef AS УзелОбмена_RRef,
//Изменения._IDRRef AS Ссылка
//FROM _ReferenceChngR1253 AS Изменения WITH (ROWLOCK, READPAST)
//ORDER BY _IDRRef DESC
//)
//DELETE target
//OUTPUT
//changes.Ссылка
//INTO @result
//FROM _ReferenceChngR1253 AS target INNER JOIN changes ON target._IDRRef = changes.Ссылка
//;
//SELECT * FROM @result ORDER BY id ASC;
//;