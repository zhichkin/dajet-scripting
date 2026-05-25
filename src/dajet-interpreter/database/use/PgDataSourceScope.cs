using DaJet.Data.PostgreSql;
using Npgsql;
using System.Data;

namespace DaJet.Data
{
    public sealed class PgDataSourceScope : DataSourceScope
    {
        private bool _disposed;
        private NpgsqlConnection _connection;
        private NpgsqlTransaction _transaction;
        public PgDataSourceScope(string connectionString, string isolationLevel)
        {
            _connection = PgDataSourceFactory.CreateConnection(in connectionString);

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
        public override DataSourceType Type { get { return DataSourceType.PostgreSql; } }
        public NpgsqlCommand CreateCommand()
        {
            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));

            NpgsqlCommand command = _connection.CreateCommand();

            command.Connection = _connection;
            command.Transaction = _transaction;
            command.CommandType = CommandType.Text;

            return command;
        }
        public void TxBegin()
        {
            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));

            _transaction = _connection.BeginTransaction();
        }
        public void TxCommit()
        {
            // ObjectDisposedException.ThrowIf(_disposed, typeof(MsDataContext));

            _transaction?.Commit();
        }
        public void TxRollback()
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