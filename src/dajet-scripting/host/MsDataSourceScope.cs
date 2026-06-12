using Microsoft.Data.SqlClient;
using System.Data;

namespace DaJet.Data
{
    public sealed class MsDataSourceScope : DataSourceScope<SqlCommand>
    {
        private bool _disposed;
        private SqlConnection _connection;
        private SqlTransaction _transaction;
        public MsDataSourceScope(string connectionString, string isolationLevel)
        {
            _connection = new SqlConnection(connectionString);

            try
            {
                _connection.Open();

                if (!string.IsNullOrEmpty(isolationLevel))
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
            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));

            SqlCommand command = _connection.CreateCommand();

            command.Connection = _connection;
            command.Transaction = _transaction;
            command.CommandType = CommandType.Text;

            return command;
        }
        public override void TxBegin()
        {
            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));

            _transaction = _connection.BeginTransaction();
        }
        public override void TxCommit()
        {
            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));

            _transaction?.Commit();
        }
        public override void TxRollback()
        {
            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));

            _transaction?.Rollback();
        }
        public override void Dispose()
        {
            if (_disposed) { return; }

            _transaction?.Dispose();
            _transaction = null;

            _connection?.Dispose();
            _connection = null;

            _disposed = true;
        }
    }
}