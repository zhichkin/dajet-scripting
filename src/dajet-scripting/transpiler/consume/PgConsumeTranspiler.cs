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
            SelectExpression select = new()
            {
                Options = "FOR UPDATE" + (statement.StrictOrderRequired ? string.Empty : " SKIP LOCKED")
            };
        }
        private static string ToSqlDataType(DataType type)
        {
            if (type.IsBoolean) { return "binary(1)"; }
            else if (type.IsDecimal) { return string.Format("numeric({0},{1})", type.Precision, type.Scale); }
            else if (type.IsDateTime) { return "datetime2"; }
            else if (type.IsString) { return (type.Size == 0) ? "nvarchar(max)" : string.Format("nvarchar({0})", type.Size); }
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

            if (consume.From.TryGetTable(out TableReference table))
            {
                table.Hints = "WITH (ROWLOCK" + (consume.StrictOrderRequired ? ")" : ", READPAST)");
            }

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