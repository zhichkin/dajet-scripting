using DaJet.Data;
using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class CreateSequenceProcessor : ProcessorBase
    {
        private readonly MsDataSourceScope _dataSource;
        private readonly CreateSequenceStatement _statement;
        public CreateSequenceProcessor(in ScriptContext context, in CreateSequenceStatement statement)
        {
            if (context.GetDataSource() is not MsDataSourceScope use)
            {
                throw new InvalidOperationException();
            }

            _dataSource = use;
            _statement = statement;

            //if (_statement.Target is not TableReference table || table.Binding is not EntityDefinition target)
            //{
            //    throw new InvalidOperationException();
            //}

            //if (_statement.Source is not SelectExpression source)
            //{
            //    throw new InvalidOperationException();
            //}

            //_target = target;
            //_source = source;
            
            //_sql = TranspileToSql();
        }
        private string TranspileToSql()
        {
            StringBuilder sql = new();

            
            
            return sql.ToString();
        }
        public override void Process()
        {
            using (SqlCommand command = _dataSource.CreateCommand())
            {
                //command.CommandText = _sql;
                
                int recordsAffected = command.ExecuteNonQuery();
            }
        }
        public override void Dispose()
        {
            // do nothing
        }
    }
}