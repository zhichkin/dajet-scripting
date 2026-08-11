using DaJet.Data;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;

namespace DaJet.Scripting
{
    public sealed class MsNonQueryProcessor : ProcessorBase
    {
        private readonly MsDataSourceScope _dataSource;
        private readonly SqlStatement _statement;
        public MsNonQueryProcessor(in ScriptContext context, in SqlStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _statement = statement;
        }
        public override ExitCode Process()
        {
            using (SqlCommand command = _dataSource.CreateCommand())
            {
                command.CommandText = _statement.Sql;
                
                int rows_affected = command.ExecuteNonQuery();
            }

            return ExitCode.Success;
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}