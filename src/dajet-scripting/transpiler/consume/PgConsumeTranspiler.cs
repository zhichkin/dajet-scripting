using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgConsumeTranspiler : SqlTranspiler
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
            if (statement.From.Expression is not TableReference table)
            {
                throw new InvalidOperationException();
            }

            if (table.Binding is not EntityDefinition source)
            {
                throw new InvalidOperationException();
            }

            List<PropertyDefinition> index = GetPrimaryOrUniqueIndex(in table);

            SelectStatement filter = TransformConsumeToFilter(in statement, in index);

            if (!new PgSelectTranspiler().TryTranspile(filter, in _provider, out string error))
            {
                throw new Exception(error);
            }

            StringBuilder sql = new();

            sql.Append("WITH filter AS (");

            sql.Append(filter.Sql).Append(')').Append(',').AppendLine();

            sql.AppendLine("source AS (").Append("DELETE FROM ").Append(source.DbName.ToLowerInvariant()).AppendLine(" AS t USING filter");

            sql.Append("WHERE ");

            bool first = true;

            foreach (PropertyDefinition property in index)
            {
                foreach (ColumnDefinition column in property.Columns)
                {
                    if (!first) { sql.Append(" AND "); }

                    sql.Append(string.Format("t.{0} = filter.{0}", column.Name.ToLowerInvariant()));
                    
                    first = false;
                }
            }

            sql.AppendLine().AppendLine("RETURNING");

            first = true;

            foreach (PropertyDefinition property in source.Properties)
            {
                foreach (ColumnDefinition column in property.Columns)
                {
                    if (!first) { sql.Append(','); }

                    sql.AppendLine(string.Format("t.{0}", column.Name));

                    first = false;
                }
            }
            
            sql.AppendLine().Append(')').AppendLine();

            SelectStatement select = TransformConsumeToSelect(in statement);

            source.DbName = "source";

            if (!new PgSelectTranspiler().TryTranspile(select, in _provider, out error))
            {
                throw new Exception(error);
            }

            sql.Append(select.Sql);

            foreach (SyntaxNode parameter in filter.Input)
            {
                statement.Input.Add(parameter);
            }

            //TODO: copy parameters from final SELECT : statement.Input = select.Input;

            statement.Output = select.GetIntoClause();

            statement.Sql = sql.ToString();
        }
        private static string ToSqlDataType(DataType type)
        {
            if (type.IsBoolean) { return "bytea"; }
            else if (type.IsDecimal) { return string.Format("numeric({0},{1})", type.Precision, type.Scale); }
            else if (type.IsDateTime) { return "timestamp"; }
            else if (type.IsString) { return (type.Size == 0) ? "mvarchar" : string.Format("{0}({1})", (type.IsFixed) ? "mchar" : "mvarchar", type.Size); }
            else if (type.IsBinary) { return "bytea"; }
            else if (type.IsUuid) { return "bytea"; }
            else if (type.IsEntity) { return "bytea"; }
            else if (type.IsInteger) { return (type.Size == 4) ? "integer" : "bigint"; }

            throw new InvalidOperationException("Failed to map DaJet data type to SQL data type.");
        }
        private IndexInfo GetPrimaryOrUniqueIndex(in string tableName)
        {
            List<IndexInfo> indexes = new PgSqlHelper(_provider.ConnectionString).GetIndexes(in tableName);

            foreach (IndexInfo index in indexes)
            {
                if (index.IsPrimary) { return index; }
            }

            foreach (IndexInfo index in indexes)
            {
                if (index.IsUnique && index.IsClustered) { return index; }
            }

            foreach (IndexInfo index in indexes)
            {
                if (index.IsUnique) { return index; }
            }

            return null;
        }
        private List<PropertyDefinition> GetPrimaryOrUniqueIndex(in TableReference table)
        {
            if (table.Binding is not EntityDefinition source)
            {
                throw new InvalidOperationException("[TRANSPILER] [CONSUME] target table is not bound to metadata.");
            }

            string tableName = source.DbName.ToLowerInvariant();

            IndexInfo index = GetPrimaryOrUniqueIndex(in tableName);

            List<PropertyDefinition> filter = new();

            foreach (IndexColumnInfo field in index.Columns)
            {
                PropertyDefinition property = source.GetPropertyByColumnName(field.Name);

                if (property is not null && !filter.Contains(property))
                {
                    filter.Add(property);
                }
            }

            return filter;
        }
        private static SelectStatement TransformConsumeToFilter(in ConsumeStatement consume, in List<PropertyDefinition> filter)
        {
            SelectExpression select = new()
            {
                Top = consume.Top,
                From = consume.From,
                Where = consume.Where,
                Order = consume.Order,
                Options = "FOR UPDATE" + (consume.StrictOrderRequired ? string.Empty : " SKIP LOCKED")
            };
            
            foreach (PropertyDefinition property in filter)
            {
                select.Columns.Add(new ColumnExpression()
                {
                    Alias = property.Name,
                    Source = property,
                    Expression = new ColumnReference()
                    {
                        Binding = property,
                        Identifier = property.Name
                    }
                });
            }

            return new SelectStatement() { Expression = select };
        }
        private static SelectStatement TransformConsumeToSelect(in ConsumeStatement consume)
        {
            SelectStatement select = new()
            {
                Expression = new SelectExpression()
                {
                    From = consume.From,
                    Into = consume.Into,
                    Order = consume.Order,
                    Columns = consume.Columns
                }
            };

            return select;
        }
    }
}

//WITH filter AS
//(SELECT
//  МоментВремени,
//  Идентификатор
//FROM
//  {TABLE_NAME}
//ORDER BY
//  МоментВремени ASC,
//  Идентификатор ASC
//LIMIT
//  @MessageCount
//FOR UPDATE SKIP LOCKED
//),

//queue AS(
//DELETE FROM {TABLE_NAME} t USING filter
//WHERE t.МоментВремени = filter.МоментВремени
//  AND t.Идентификатор = filter.Идентификатор
//RETURNING
//  t.МоментВремени, t.Идентификатор, t.ДатаВремя,
//  t.Отправитель, t.Получатели, t.Заголовки,
//  t.ТипОперации, t.ТипСообщения, t.ТелоСообщения
//)

//SELECT
//  queue.МоментВремени, queue.Идентификатор, queue.ДатаВремя,
//  CAST(queue.Заголовки     AS text)    AS "Заголовки",
//  CAST(queue.Отправитель   AS varchar) AS "Отправитель",
//  CAST(queue.Получатели    AS text)    AS "Получатели",
//  CAST(queue.ТипОперации   AS varchar) AS "ТипОперации",
//  CAST(queue.ТипСообщения  AS varchar) AS "ТипСообщения",
//  CAST(queue.ТелоСообщения AS text)    AS "ТелоСообщения"
//FROM
//  queue
//ORDER BY
//  queue.МоментВремени ASC,
//  queue.Идентификатор ASC
//;