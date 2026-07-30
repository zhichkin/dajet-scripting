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

            _transaction?.Commit();
        }
        public override void Rollback()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataSourceScope));

            try
            {
                _transaction?.Rollback();
            }
            finally
            {
                _transaction = null;
            }
        }
        public override void Dispose()
        {
            if (_disposed) { return; }

            _transaction?.Dispose(); //NOTE: rolls back uncommitted transaction
            _transaction = null;

            _connection?.Dispose();
            _connection = null;

            _disposed = true;
        }
    }
}