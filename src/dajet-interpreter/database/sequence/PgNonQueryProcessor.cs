using DaJet.Data;
using DaJet.Scripting.Model;
using Npgsql;

namespace DaJet.Scripting
{
    public sealed class PgNonQueryProcessor : ProcessorBase
    {
        private readonly PgDataSourceScope _dataSource;
        private readonly SqlStatement _statement;
        public PgNonQueryProcessor(in ScriptContext context, in SqlStatement statement)
        {
            if (context.GetDataSource() is not PgDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _statement = statement;
        }
        public override void Process()
        {
            int rows_affected;

            //FIXME: commands splitter
            string[] commands = _statement.Sql.Split("GO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            using (NpgsqlCommand command = _dataSource.CreateCommand())
            {
                foreach (string sql in commands)
                {
                    command.CommandText = sql;

                    rows_affected = command.ExecuteNonQuery();
                }
            }
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}