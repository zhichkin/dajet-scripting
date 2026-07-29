using DaJet.Data;
using DaJet.Scripting.Model;
using Npgsql;

namespace DaJet.Scripting
{
    public sealed class PgCreateSequenceProcessor : ProcessorBase // NonQueryProcessor
    {
        private readonly PgDataSourceScope _dataSource;
        private readonly CreateSequenceStatement _statement;
        public PgCreateSequenceProcessor(in ScriptContext context, in CreateSequenceStatement statement)
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
            using (NpgsqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _statement.Sql;
                
                int rows_affected = command.ExecuteNonQuery();
            }
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}