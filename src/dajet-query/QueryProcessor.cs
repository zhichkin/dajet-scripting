using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Data
{
    public sealed class QueryProcessor
    {
        private readonly string _source;

        private string _database;
        private SqlStatement _statement;
        public QueryProcessor(in string script)
        {
            _source = script; Prepare();
        }
        public List<DataObject> Execute()
        {
            List<DataObject> table = null;

            MetadataProvider provider = MetadataProvider.Get(_database);

            if (provider.DataSource == DataSourceType.SqlServer)
            {
                table = new MsQueryProcessor(provider.ConnectionString, in _statement).Execute();
            }
            else if (provider.DataSource == DataSourceType.PostgreSql)
            {
                table = new PgQueryProcessor(provider.ConnectionString, in _statement).Execute();
            }
            else
            {
                throw new InvalidOperationException($"Unsupported data source {provider.DataSource}");
            }

            return table;
        }
        private void Prepare()
        {
            Parser parser = new();

            if (!parser.TryParse(in _source, out Script script, out string error))
            {
                throw new InvalidOperationException(error);
            }

            foreach (SyntaxNode statement in script.Statements)
            {
                if (statement is UseStatement use)
                {
                    _database = use.Source;
                }
            }

            Binder binder = new();
            //OneDbSchemaProvider schema = new();
            CacheableSchemaProvider schema = new();

            if (!binder.TryBind(in script, schema, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            Transpiler transpiler = new();

            if (!transpiler.TryTranspile(in script, out List<SqlStatement> statements, out errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            _statement = statements[0];
        }
    }
}