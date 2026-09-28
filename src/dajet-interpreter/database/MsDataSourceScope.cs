using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Data
{
    public sealed class MsDataSourceScope : DataSourceScope<SqlCommand>
    {
        private bool _disposed;
        private SqlConnection _connection;
        private SqlTransaction _transaction;
        public MsDataSourceScope(string connectionString, bool transactional = false)
        {
            _connection = new SqlConnection(connectionString);

            try
            {
                _connection.Open();

                if (transactional)
                {
                    _transaction = _connection.BeginTransaction();
                }
            }
            catch
            {
                Dispose(); throw;
            }
        }
        public override DataSourceType Type { get { return DataSourceType.SqlServer; } }
        public override SqlConnection Connection { get { return _connection; } }
        public override SqlTransaction Transaction { get { return _transaction; } }
        public override SqlCommand CreateCommand()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataSourceScope));

            SqlCommand command = _connection.CreateCommand();

            command.Connection = _connection;
            command.Transaction = _transaction;
            command.CommandType = CommandType.Text;

            return command;
        }
        public override void Commit()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataSourceScope));

            base.Synchronize(true);
            
            _transaction?.Commit();
        }
        public override void Cancel()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataSourceScope));

            try
            {
                _transaction?.Rollback();
            }
            catch
            {
                // do nothing
            }
            finally
            {
                _transaction = null;
            }

            base.Synchronize(false);
        }
        public override void Dispose()
        {
            if (_disposed) { return; }

            try
            {
                _transaction?.Dispose(); //NOTE: rolls back uncommitted transaction
                
                _connection?.Dispose();
                
                base.Disposed();
            }
            catch
            {
                // do nothing
            }
            finally
            {
                _connection = null;
                _transaction = null;
            }

            _disposed = true;
        }
    }
}