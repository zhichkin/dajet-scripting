using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;
using System.Xml.Linq;

namespace DaJet.Scripting
{
    public sealed class MsConsumeTranspiler : SqlTranspiler
    {
        private static readonly BooleanClauseTransformer _transformer = new();
        private MetadataProvider _provider;
        private ConsumeStatement _statement;
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
            _statement.Output = consume.Into.Value;

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
        public override void Visit(in SyntaxNode expression, in StringBuilder script)
        {
            throw new NotImplementedException();
        }
        //private string GetOrdinalParameterName()
        //{
        //    string parameterName;

        //    if (_statement.Dialect == Data.DataSourceType.PostgreSql)
        //    {
        //        parameterName = string.Format("${0}", _parameterOrdinal);
        //    }
        //    else
        //    {
        //        parameterName = string.Format("@p{0}", _parameterOrdinal);
        //    }

        //    _parameterOrdinal++;

        //    return parameterName;
        //}
        private void Transpile(in ConsumeStatement statement)
        {
            StringBuilder sql = new();

            sql.Append("WITH queue AS");

            sql.AppendLine().Append("WHERE ");

            _transformer.Transform(statement.Where);

            //Visit(node.Expression, in script);

            statement.Sql = sql.ToString();
        }
        private SelectStatement TransformConsumeToSelect(in ConsumeStatement consume)
        {
            SelectStatement select = new()
            {
                Expression = new SelectExpression()
                {
                    Top = consume.Top,
                    From = consume.From,
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