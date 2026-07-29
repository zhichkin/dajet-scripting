using DaJet.Data;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;

namespace DaJet.Scripting
{
    public sealed class MsApplySequenceProcessor : ProcessorBase // NonQueryProcessor
    {
        private readonly MsDataSourceScope _dataSource;
        private readonly ApplySequenceStatement _statement;
        public MsApplySequenceProcessor(in ScriptContext context, in ApplySequenceStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _statement = statement;
        }
        public override void Process()
        {
            using (SqlCommand command = _dataSource.CreateCommand())
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